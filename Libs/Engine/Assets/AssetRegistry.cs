namespace Engine.Assets;

/// <summary>
/// 运行期的 AssetId → object 映射表。只负责存取，不关心资源从哪来。
/// 加载 / 扫描等逻辑在 <see cref="AssetDatabase"/> 里。
/// </summary>
public sealed class AssetRegistry
{
    private readonly System.Collections.Generic.Dictionary<AssetId, object> _map = new();

    public void Register(AssetId id, object target) => _map[id] = target;
    public bool TryResolve(AssetId id, out object target) => _map.TryGetValue(id, out target!);
    public void Unregister(AssetId id) => _map.Remove(id);
    public void Clear() => _map.Clear();

    public int Count => _map.Count;
    public System.Collections.Generic.IReadOnlyDictionary<AssetId, object> All => _map;
}
