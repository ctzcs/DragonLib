namespace Engine;

/// <summary>
/// 定容句柄槽位池（对应 olib/handle/fixed）：容量在构造时确定，满后
/// <see cref="SlotMap{T}.TryAdd"/> 返回 false、<see cref="SlotMap{T}.Add"/> 抛异常，
/// 适合有硬上限的池（粒子、弹幕等）。其余语义（idx 0 dummy、复用 Gen +1、
/// Remove/Clear/Reset 生命周期、destroy 回调）与 <see cref="SlotMap{T}"/> 完全一致。
/// <para>
/// 说明：olib 的 fixed 因 Odin 内联数组免堆分配而存在；C# 的槽位数组本来就在堆上，
/// 这里的"定容"意义在于上限约束而非省分配。<see cref="SlotMap{T}.EnsureCapacity"/> 是 no-op。
/// </para>
/// </summary>
public sealed class FixedSlotMap<T>(int capacity) : SlotMap<T>(capacity)
{
    protected override bool GrowSlots(int needed) => needed <= Capacity + 1;
}
