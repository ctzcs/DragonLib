using System.Runtime.CompilerServices;

namespace Engine;

public struct Handle:IEquatable<Handle>
{
    public int Idx;
    public int Gen;

    public bool Equals(Handle other)
    {
        return Idx == other.Idx && Gen == other.Gen;
    }

    public override bool Equals(object? obj)
    {
        return obj is Handle other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Idx, Gen);
    }

    public static bool operator ==(Handle self,Handle other)
    {
        return self.Equals(other);
    }

    public static bool operator !=(Handle self, Handle other)
    {
        return !(self == other);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsNone()
    {
        return Idx == 0 && Gen == 0;
    }
}


public struct Item<T>
{
    public T Value;
    public Handle Handle;
}

public class FixedHandleArray<T>(int capacity)
{
    private Item<T>[] _slots = new Item<T>[capacity];
    private Stack<Handle> _freehandles = new();
    private int _aliveCount = 0;
    private static Item<T> NONE = default;
    
    
    public Span<Item<T>> Data => _slots.AsSpan();
    
    public ref Item<T> this[Handle handle]
    {
        get
        {
#if UNITY_EDITOR 
            if (!IsValid(handle)) return ref NONE;
#endif
            return ref _slots[handle.Idx];
        }
    }

    public ref Item<T> Add(Item<T> item)
    {
        Handle handle;
        if (_freehandles.Count > 0)
        {
            handle = _freehandles.Pop();
            handle.Gen+=1;
            item.Handle = handle;
            _slots[handle.Idx] = item;
            return ref _slots[handle.Idx];
        }
        
        handle = new Handle()
        {
            Idx = _aliveCount,
            Gen = 1,
        };
        _aliveCount++;
        item.Handle = handle;
        _slots[handle.Idx] = item;
        return ref _slots[handle.Idx];

    }

    public void Remove(Handle handle)
    {
#if UNITY_EDITOR
        if(!IsValid(handle)) return;
#endif
        if (_slots[handle.Idx].Handle != handle)
        {
            throw new NullReferenceException("This Handle Is not Valid");
            return;
        }
        handle.Gen++;
        _freehandles.Push(handle);
        _slots[handle.Idx] = default;
        _aliveCount--;
    }

    public void Clear()
    {
        _freehandles.Clear();
        _aliveCount = 0;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsValid(Handle handle)
    {
        return !handle.IsNone()
               && handle.Idx >= 0
               && handle.Idx < capacity;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref Item<T> Get(Handle h)
    {
        if (!IsValid(h)) return ref NONE;
        return ref _slots[h.Idx];
    }
}


public static class FixedHandleArrayExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValid<T>(this ref Item<T> item) where T : struct
    {
        return item.Handle.Gen != 0;
    }
}