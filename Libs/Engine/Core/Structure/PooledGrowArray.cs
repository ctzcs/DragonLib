using System.Buffers;

namespace Engine;

public sealed class PooledGrowArray<T> : IDisposable
{
    private T[] _data;
    private readonly ArrayPool<T> _pool;
    public int Count { get; private set; }

    public PooledGrowArray(int capacity = 4, ArrayPool<T>? pool = null)
    {
        _pool = pool ?? ArrayPool<T>.Shared;
        _data = _pool.Rent(Math.Max(1, capacity));
    }

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
        var next = _pool.Rent(newCap);
        Array.Copy(_data, next, Count);
        _pool.Return(_data, clearArray: true);
        _data = next;
    }

    public void Dispose()
    {
        _pool.Return(_data, clearArray: true);
        _data = Array.Empty<T>();
        Count = 0;
    }
}