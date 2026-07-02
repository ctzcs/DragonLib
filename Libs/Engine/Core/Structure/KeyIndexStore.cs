namespace Engine;

/// <summary>
/// 一种将Key映射到index的容器，主要用于稳定资源的访问，查询一次key，存储index
/// 该容器只能删除重建
/// </summary>
/// <param name="capacity"></param>
/// <typeparam name="TKey"></typeparam>
/// <typeparam name="TValue"></typeparam>
public class KeyIndexStore<TKey, TValue>(int capacity)
    where TKey : IEquatable<TKey>
{
    private readonly Dictionary<TKey,int> map = new(capacity);
    private readonly PooledGrowArray<TValue> values = new(capacity);
    public int Count => values.Count;
    
    public bool TryGetIndex(TKey key, out int value)
    {
        return map.TryGetValue(key, out value);
    }
    
    public ref TValue this[int index] => ref values[index];
    
    public TValue Get(int index) => values[index];
    public ref TValue GetRef(int index) => ref values[index];
    
    public int Add(TKey key, TValue value)
    {
        values.Add(value);
        int index = Count;
        map.Add(key, index);
        return index;
    }

    public void Dispose()
    {
        map.Clear();
        values.Dispose();
    }
    
}