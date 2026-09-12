using System.Runtime.CompilerServices;

namespace Engine;

/// <summary>
/// 句柄槽位池（可扩容，对应 olib/handle/array）：<see cref="SlotHandle"/> → 元素的稳定映射。
/// 槽位满时倍增扩容（Array.Resize）；句柄只含 idx+gen，扩容后全部照常有效，
/// 但旧的 ref / Span / 枚举器会指向旧数组，扩容后必须重新解析句柄。
/// idx 0 是 dummy 槽，实际可存 <see cref="Capacity"/> = 槽位数 - 1 个元素。
/// 扩容时一并预留空闲链容量，把可失败的分配集中在 Add，Remove/Clear 永不分配（olib 同款约定）。
/// 容器非线程安全；迭代期间不要结构性修改（增删、Clear、Reset、触发扩容的 Add）。
/// <para>
/// 生命周期（对齐 olib）：
/// <list type="table">
/// <item><term>Remove</term><description>移除单个元素，槽位可复用，旧句柄立即失效；对无效句柄是幂等的 no-op。</description></item>
/// <item><term>Clear</term><description>移除全部活跃元素，保留槽位、代数历史和容量；后续 Add 复用空闲槽，旧句柄继续失效（直到代数回绕）。</description></item>
/// <item><term>Reset</term><description>开始新生命周期，清零槽位历史但保留容量；调用方必须丢弃所有旧句柄。</description></item>
/// </list>
/// Remove/Clear/Reset 可传 destroy 回调对活跃元素逐项清理；回调内不得结构性修改本容器。
/// 容器不自动释放 T 内部持有的资源，未提供回调时由调用方处理。
/// </para>
/// </summary>
public class SlotMap<T>
{
    private Item<T>[] _slots;
    // 空闲槽索引栈；对应槽位的 Handle.Idx 已归零，但 Gen 留在槽里供复用时 +1。
    private int[] _freeIdx;
    private int _freeCount;
    // 已触及的槽位数（含 idx 0 dummy），即历史最高水位。
    private int _touched;
    private int _alive;

    // ref-return 的无效落点。不能声明为 readonly（readonly 字段禁止 ref 返回），且必须保持 default。
    private static Item<T> NONE = default;

    /// <param name="capacity">初始槽位容量（实际可存 capacity - 1 个元素），满时自动倍增。</param>
    public SlotMap(int capacity = 8)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _slots = new Item<T>[Math.Max(capacity, 2)];
        _freeIdx = new int[4];
    }

    /// <summary>当前槽位容量（会随扩容增长），可存元素数为此值减一。</summary>
    public int Capacity => _slots.Length - 1;

    /// <summary>当前活跃元素数。</summary>
    public int Count => _alive;

    /// <summary>历史最高水位（含 dummy 与空闲槽）。遍历时跳过 <see cref="Item{T}.IsLive"/> 为 false 的槽。</summary>
    public int Touched => _touched;

    /// <summary>原始槽位视图，含空槽。只在需要 SIMD/批量扫描时使用，常规遍历用 foreach；扩容后旧视图失效。</summary>
    public Span<Item<T>> Data => _slots.AsSpan();

    /// <summary>按句柄取槽位。无效句柄返回 default 落点（Handle 为 None），不抛异常。</summary>
    public ref Item<T> this[SlotHandle handle] => ref Get(handle);

    /// <summary>添加一个元素；可扩容实现下仅内存耗尽才会失败。</summary>
    public SlotHandle Add(T value) =>
        TryAdd(value, out var handle) ? handle : throw new InvalidOperationException("SlotMap is full.");

    /// <summary>添加一个元素；定容实现满时返回 false 且不改动容器。</summary>
    public bool TryAdd(T value, out SlotHandle handle)
    {
        if (_freeCount > 0)
        {
            int idx = _freeIdx[--_freeCount];
            handle = new SlotHandle(idx, _slots[idx].Handle.Gen + 1);
            _slots[idx] = new Item<T> { Value = value, Handle = handle };
            _alive++;
            return true;
        }

        if (_touched == 0) _touched = 1; // idx 0 占 dummy
        if (_touched >= _slots.Length && !GrowSlots(_touched + 1))
        {
            handle = SlotHandle.None;
            return false;
        }

        handle = new SlotHandle(_touched, 1);
        _slots[_touched] = new Item<T> { Value = value, Handle = handle };
        _touched++;
        _alive++;
        return true;
    }

    /// <summary>预留容量，使 Capacity 至少为 capacity；定容实现是 no-op。</summary>
    public void EnsureCapacity(int capacity) => GrowSlots(capacity + 1);

    /// <summary>按句柄取槽位。无效句柄返回 default 落点（Handle 为 None），不抛异常。</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref Item<T> Get(SlotHandle handle)
    {
        if (IsValid(handle)) return ref _slots[handle.Idx];
        return ref NONE;
    }

    /// <summary>按句柄取值副本并报告句柄是否有效（对应 olib 的 get_value）。</summary>
    public bool TryGet(SlotHandle handle, out T value)
    {
        if (IsValid(handle))
        {
            value = _slots[handle.Idx].Value!;
            return true;
        }
        value = default!;
        return false;
    }

    /// <summary>完整校验：idx 在高水位内且与槽位句柄（含 Gen）完全一致。</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsValid(SlotHandle handle) =>
        handle.Idx > 0
        && handle.Idx < _touched
        && _slots[handle.Idx].Handle == handle;

    /// <summary>移除一个活跃元素；句柄无效时是 no-op。返回是否真正移除。</summary>
    public bool Remove(SlotHandle handle) => Remove(handle, null);

    /// <summary>移除一个活跃元素并可选清理资源；句柄无效时是 no-op。返回是否真正移除。</summary>
    public bool Remove(SlotHandle handle, Action<T>? destroy)
    {
        if (!IsValid(handle)) return false;
        ref var slot = ref _slots[handle.Idx];
        destroy?.Invoke(slot.Value!);
        int gen = slot.Handle.Gen; // 先存，回调可能改写槽位
        slot = default;
        slot.Handle = new SlotHandle(0, gen); // 空槽：idx 归零，Gen 留给下次复用 +1
        PushFree(handle.Idx);
        _alive--;
        return true;
    }

    /// <summary>移除全部活跃元素，保留槽位、代数历史与容量（旧句柄继续失效）。</summary>
    public void Clear(Action<T>? destroy = null)
    {
        for (int i = 1; i < _touched; i++)
        {
            ref var slot = ref _slots[i];
            if (slot.Handle.Idx == 0) continue; // 已空闲的槽不重复入链
            destroy?.Invoke(slot.Value!);
            int gen = slot.Handle.Gen;
            slot = default;
            slot.Handle = new SlotHandle(0, gen);
            PushFree(i);
        }
        _alive = 0;
    }

    /// <summary>开始新生命周期：清零槽位与代数历史，保留容量。调用方必须丢弃全部旧句柄。</summary>
    public void Reset(Action<T>? destroy = null)
    {
        if (destroy != null)
        {
            for (int i = 1; i < _touched; i++)
            {
                if (_slots[i].Handle.Idx != 0) destroy.Invoke(_slots[i].Value!);
            }
        }
        Array.Clear(_slots);
        _freeCount = 0;
        _touched = 0;
        _alive = 0;
    }

    /// <summary>遍历活跃元素，自动跳过空槽。foreach 拿到的是副本，需要原地修改时用手动循环取 ref。</summary>
    public Enumerator GetEnumerator() => new(_slots, _touched);

    /// <summary>
    /// 扩容槽数组到至少 needed 个槽，并预留空闲链容量，保证之后的 Remove/Clear 不再分配。
    /// 定容子类覆写为拒绝扩容（返回 false）。返回是否满足 needed。
    /// </summary>
    protected virtual bool GrowSlots(int needed)
    {
        if (needed <= _slots.Length) return true;
        int newLen = Math.Max(needed, _slots.Length * 2);
        Array.Resize(ref _slots, newLen);
        if (_freeIdx.Length < newLen - 1)
            Array.Resize(ref _freeIdx, Math.Max(newLen - 1, _freeIdx.Length * 2));
        return true;
    }

    // 正常路径下容量已被 GrowSlots 预留好，这里的兜底扩容几乎不会触发。
    private void PushFree(int idx)
    {
        if (_freeCount == _freeIdx.Length)
            Array.Resize(ref _freeIdx, Math.Max(_freeIdx.Length * 2, 8));
        _freeIdx[_freeCount++] = idx;
    }

    /// <summary>foreach (var item in map) { ... } 跳过空槽；struct 值类型为 T 时 item 是副本。</summary>
    public struct Enumerator
    {
        private readonly Item<T>[] _slots;
        private readonly int _touched;
        private int _i;
        private int _cur;

        internal Enumerator(Item<T>[] slots, int touched)
        {
            _slots = slots;
            _touched = touched;
            _i = 1;
            _cur = 0;
        }

        public readonly ref Item<T> Current => ref _slots[_cur];

        public bool MoveNext()
        {
            while (_i < _touched)
            {
                int i = _i++;
                if (_slots[i].Handle.Idx != 0)
                {
                    _cur = i;
                    return true;
                }
            }
            return false;
        }
    }
}
