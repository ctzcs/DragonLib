namespace Engine;

public sealed class GrowArray<T>
{
    private T[] _data;
    public int Count { get; private set; }

    public GrowArray(int capacity = 4) => _data = new T[Math.Max(1, capacity)];

    public ref T this[int index] => ref _data[index];

    public int Add(in T value)
    {
        EnsureCapacity(Count + 1);
        _data[Count] = value;
        return Count++;
    }

    public void EnsureCapacity(int needed)
    {
        if (_data.Length >= needed) return;
        int newCap = Math.Max(needed, _data.Length * 2);
        Array.Resize(ref _data, newCap);
    }
}