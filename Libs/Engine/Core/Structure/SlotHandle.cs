namespace Engine;

/// <summary>
/// 索引 + 代数（generation）构成的稳定句柄，用于替代"需要长期持有的指针"：
/// 槽位被删除、复用后 Gen 递增，旧句柄自动失效，因此能识别悬挂引用。
/// 约定对齐 olib/handle：
/// <list type="bullet">
/// <item>idx == 0 永远是 dummy 槽，零句柄（<see cref="None"/>）始终无效；</item>
/// <item>句柄只属于创建它的容器实例，跨容器（即使元素类型相同）互用是未定义行为；</item>
/// <item>Gen 为 32 位，回绕后不保证历史句柄仍然无效。</item>
/// </list>
/// </summary>
public readonly record struct SlotHandle(int Idx, int Gen)
{
    /// <summary>无效句柄，<c>default(SlotHandle)</c> 与之相等。</summary>
    public static readonly SlotHandle None = default;

    /// <summary>是否为无效句柄。只要 idx == 0（dummy 槽）即无效。</summary>
    public bool IsNone => Idx == 0;

    public override string ToString() => IsNone ? "none" : $"{Idx}:{Gen}";
}

/// <summary>
/// 槽位：值 + 容器回写的句柄。<see cref="Item{T}.Handle"/> 由容器维护，调用方不应修改。
/// 遍历原始槽位（<see cref="FixedHandleArray{T}.Data"/>）时用 <see cref="IsLive"/> 跳过空槽。
/// </summary>
public struct Item<T>
{
    public T Value;
    public SlotHandle Handle;

    /// <summary>该槽当前是否持有活跃元素。空槽的 idx 为 0，但可能保留 Gen 供复用递增。</summary>
    public bool IsLive => Handle.Idx != 0;
}
