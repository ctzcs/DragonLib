namespace Engine.Assets;

/// <summary>
/// 内容资产的运行时门面：把资源按"相对路径名"登记进来，用 <see cref="AssetId.FromName"/>
/// 得到的哈希 id 建立 id→资产 映射。同时维护 id→名字 的反向表，供编辑器 UI 显示与下拉选择。
///
/// 权威是名字：运行期加载资源时凭路径重建映射，磁盘上只存哈希 long。
/// </summary>
public sealed class AssetDatabase
{
    private readonly AssetRegistry _registry = new();
    // id → 规范化后的名字。仅编辑器需要（哈希单向，无法从 id 反算名字）。
    private readonly Dictionary<AssetId, string> _names = new();
    // 每种资产类型下所有名字的排序缓存，给 AssetRefDrawer 的下拉用。type → sorted names
    private readonly Dictionary<Type, List<string>> _namesByType = new();

    public AssetRegistry Registry => _registry;

    /// <summary>
    /// 登记一个已构造好的资产。name 是相对路径（如 "objects/lift_outskirts"，扩展名可有可无）。
    /// 若归一化后的名字与已存在的不同资产哈希碰撞，抛异常——宁可启动即崩，也不要静默覆盖。
    /// </summary>
    public AssetId Register(string name, object asset)
    {
        var norm = AssetId.Normalize(name);
        var id = AssetId.FromName(name);

        if (_names.TryGetValue(id, out var existing) && existing != norm)
            throw new InvalidOperationException(
                $"AssetId 哈希碰撞: '{norm}' 与 '{existing}' 得到同一 id {id.ToHex()}。请改名其一。");

        _registry.Register(id, asset);
        _names[id] = norm;
        _namesByType.Remove(asset.GetType()); // 该类型的名字缓存失效，下次按需重建
        return id;
    }

    /// <summary>类型化解析。找不到或类型不符返回 null。</summary>
    public T? Get<T>(AssetId id) where T : class =>
        _registry.TryResolve(id, out var o) ? o as T : null;

    public T? Get<T>(AssetRef<T> r) where T : class => r.Resolve(_registry);

    public T? Get<T>(string name) where T : class => Get<T>(AssetId.FromName(name));

    /// <summary>由 id 取回名字（编辑器显示用）。找不到返回 null。</summary>
    public string? GetName(AssetId id) => _names.TryGetValue(id, out var n) ? n : null;

    public bool Contains(AssetId id) => _registry.TryResolve(id, out _);

    /// <summary>某资产类型下所有已登记名字（排序），供编辑器下拉列表使用。</summary>
    public IReadOnlyList<string> NamesOf<T>() where T : class => NamesOf(typeof(T));

    public IReadOnlyList<string> NamesOf(Type assetType)
    {
        if (_namesByType.TryGetValue(assetType, out var cached)) return cached;

        var list = new List<string>();
        foreach (var (id, obj) in _registry.All)
            if (assetType.IsInstanceOfType(obj) && _names.TryGetValue(id, out var n))
                list.Add(n);
        list.Sort(StringComparer.Ordinal);
        _namesByType[assetType] = list;
        return list;
    }

    public void Clear()
    {
        _registry.Clear();
        _names.Clear();
        _namesByType.Clear();
    }
}
