using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using DCFApixels.DragonECS;
using Engine.Assets;

namespace Engine.ECS;

/// <summary>磁盘上一个实例的形状，对应 Noel 关卡 JSON 里的 { type, id, fields }。</summary>
public sealed class EntityData
{
    /// <summary>预制体 AssetId（Noel 的 "type"）。</summary>
    public AssetId Def { get; set; }
    /// <summary>实例身份（Noel 的 "id"）。</summary>
    public SpawnId Id { get; set; }
    /// <summary>组件类型名 → 覆盖值。只存相对预制体默认值的差量（值不同、或预制体没有的组件）；与默认值一致的组件不存，加载时继承预制体。</summary>
    public Dictionary<string, JsonElement> Fields { get; set; } = new();
    /// <summary>预制体提供、但本实例上被移除的组件类型名。null = 无（旧格式文件没有此字段）。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Removed { get; set; }
}

/// <summary>一个关卡文件：一组被摆放的实例。</summary>
public sealed class LevelData
{
    public string Name { get; set; } = "";
    public List<EntityData> Entities { get; set; } = new();
    /// <summary>该关卡声明的编辑层（PatternWorld 编辑器使用；空层也靠它存活）。运行时不消费。</summary>
    public List<string> Layers { get; set; } = new();
}

/// <summary>
/// 整关卡的存 / 读。单体进出复用 <see cref="PrefabSerializer"/>，这里额外负责：
/// ① 遍历筛选出"关卡对象"（带 SpawnIdComp + PrefabRefComp 的实体）；
/// ② EntityRef 交叉引用的两趟回填（见 Assets/README.md 的 SpawnId 一节）。
/// </summary>
public static class LevelSerializer
{
    /// <summary>
    /// 加载后保留的 SpawnId → 实体 映射。运行时用它把 EntityRef 解析成真实实体。
    /// 挂在 world 上（world-scoped 单例），下次加载会被覆盖重建。
    /// </summary>
    public struct LevelRuntime
    {
        public Dictionary<SpawnId, entlong> Map;

        public bool TryResolve(EntityRef r, out entlong ent)
        {
            if (Map != null && r.IsValid && Map.TryGetValue(r.Target, out ent)) return true;
            ent = default;
            return false;
        }
    }

    // ---- Save ----------------------------------------------------------

    public static LevelData Save(EcsWorld world, string name, AssetDatabase assets)
    {
        var level = new LevelData { Name = name };
        var spawnPool = world.GetPool<SpawnIdComp>();
        var prefabPool = world.GetPool<PrefabRefComp>();
        var pools = world.AllPools;

        foreach (int e in world.Entities)
        {
            // 只有同时带身份组件的才算"关卡对象"，纯逻辑实体跳过。
            if (!spawnPool.Has(e) || !prefabPool.Has(e)) continue;

            var data = new EntityData
            {
                Def = prefabPool.Get(e).Prefab,
                Id = spawnPool.Get(e).Id,
            };

            var prefab = assets.Get<Prefab>(data.Def);
            if (prefab != null)
            {
                // 差量保存：只存相对预制体的覆盖与移除，未覆盖的组件加载时继承默认。
                var removed = new List<string>();
                DiffAgainstPrefab(world, e, prefab, data.Fields, removed);
                if (removed.Count > 0)
                    data.Removed = removed;
            }
            else
            {
                // 预制体解析不到：退化为全量快照，保证实例能原样加载回来（不丢数据）。
                foreach (int cid in world.GetComponentTypeIDsFor(e))
                {
                    var pool = pools[cid];
                    var type = pool.ComponentType;
                    if (EcsSerialization.IsExcluded(type)) continue;

                    object raw = pool.GetRaw(e);
                    data.Fields[type.Name] =
                        JsonSerializer.SerializeToElement(raw, type, PrefabSerializer.Options);
                }
            }
            level.Entities.Add(data);
        }
        return level;
    }

    /// <summary>
    /// 计算实体相对预制体的差量：与默认值不同（或预制体没有）的组件进 overrides；
    /// 预制体有而实体没有的组件类型名进 removed。Save 与编辑器的预制体传播共用。
    /// </summary>
    public static void DiffAgainstPrefab(EcsWorld world, int entity, Prefab prefab,
        Dictionary<string, JsonElement> overrides, List<string>? removed)
    {
        var pools = world.AllPools;
        HashSet<string>? present = removed == null ? null : new HashSet<string>();

        foreach (int cid in world.GetComponentTypeIDsFor(entity))
        {
            var pool = pools[cid];
            var type = pool.ComponentType;
            if (EcsSerialization.IsExcluded(type)) continue;

            object raw = pool.GetRaw(entity);
            var json = JsonSerializer.SerializeToElement(raw, type, PrefabSerializer.Options);
            present?.Add(type.Name);
            if (prefab.Components.TryGetValue(type.Name, out var defaultJson)
                && ComponentJsonEquals(defaultJson, json, type))
                continue;   // 与预制体默认值一致：不存，继承默认。
            overrides[type.Name] = json;
        }

        if (removed == null) return;
        foreach (var typeName in prefab.Components.Keys)
            if (!present!.Contains(typeName))
                removed.Add(typeName);
    }

    /// <summary>
    /// JsonElement 的原文带着来源的格式（磁盘文件的缩进层级 vs 内存序列化的碎片），
    /// 直接比文本不可靠。各自过一遍同类型的反序列化 + 序列化得到规范形再比。
    /// </summary>
    private static bool ComponentJsonEquals(JsonElement a, JsonElement b, Type type)
        => Canonicalize(a, type) == Canonicalize(b, type);

    private static string Canonicalize(JsonElement json, Type type)
        => JsonSerializer.SerializeToElement(json.Deserialize(type, PrefabSerializer.Options), type, PrefabSerializer.Options).GetRawText();

    // ---- Load ----------------------------------------------------------

    /// <summary>
    /// 加载关卡到 world。两趟：先建全部实体 + SpawnId→entlong 映射，再回填 EntityRef。
    /// 返回本次加载的映射（也写进 world 的 <see cref="LevelRuntime"/> 单例）。
    /// </summary>
    public static Dictionary<SpawnId, entlong> Load(EcsWorld world, LevelData level, AssetDatabase assets)
    {
        var map = new Dictionary<SpawnId, entlong>(level.Entities.Count);

        // 第 1 趟：实例化所有实体（复用 Instantiate 的"套默认 + 套覆盖 + 打身份"）。
        foreach (var data in level.Entities)
        {
            var prefab = assets.Get<Prefab>(data.Def);
            if (prefab == null)
            {
                EcsDebug.PrintWarning($"Level: 找不到预制体 {data.Def.ToHex()}，实例 {data.Id.ToHex()} 已跳过。");
                continue;
            }
            int e = PrefabSerializer.Instantiate(world, prefab, data.Fields, data.Id);

            // 差量格式的另一半：实例上被移除的预制体组件，实例化后删掉。
            if (data.Removed != null)
                foreach (var typeName in data.Removed)
                {
                    var removedType = EcsPoolUtil.ResolveComponentType(typeName);
                    var removedPool = removedType == null ? null : EcsPoolUtil.GetOrCreatePoolFor(world, removedType);
                    if (removedPool == null)
                    {
                        EcsDebug.PrintWarning($"Level: 未知组件类型 '{typeName}'，移除已跳过。");
                        continue;
                    }
                    if (removedPool.Has(e))
                        removedPool.Del(e);
                }

            map[data.Id] = world.GetEntityLong(e);
        }

        // 第 2 趟：所有实体都在了，回填组件里的 EntityRef 字段。
        RebindEntityRefs(world, map);

        // 保留映射供运行时解析。
        world.Get<LevelRuntime>().Map = map;
        return map;
    }

    // 缓存每个组件类型里"类型为 EntityRef 的字段"，避免每次加载都反射扫全字段。
    private static readonly Dictionary<Type, FieldInfo[]> _entityRefFields = new();

    private static FieldInfo[] GetEntityRefFields(Type componentType)
    {
        if (_entityRefFields.TryGetValue(componentType, out var cached)) return cached;
        var list = new List<FieldInfo>();
        foreach (var f in componentType.GetFields(BindingFlags.Public | BindingFlags.Instance))
            if (f.FieldType == typeof(EntityRef))
                list.Add(f);
        var arr = list.ToArray();
        _entityRefFields[componentType] = arr;
        return arr;
    }

    /// <summary>
    /// 扫描每个关卡对象的每个组件，把 EntityRef 字段里存的 SpawnId 校验为有效实体。
    /// 当前只做"验证 + 警告失效引用"；运行期解析走 <see cref="LevelRuntime.TryResolve"/> 查 map。
    /// （EntityRef 本身存的就是 SpawnId，不需要改写字段，故这里不回写组件。）
    /// </summary>
    private static void RebindEntityRefs(EcsWorld world, Dictionary<SpawnId, entlong> map)
    {
        var spawnPool = world.GetPool<SpawnIdComp>();
        var pools = world.AllPools;

        foreach (int e in world.Entities)
        {
            if (!spawnPool.Has(e)) continue;

            foreach (int cid in world.GetComponentTypeIDsFor(e))
            {
                var pool = pools[cid];
                var type = pool.ComponentType;
                var fields = GetEntityRefFields(type);
                if (fields.Length == 0) continue;

                object raw = pool.GetRaw(e);
                for (int i = 0; i < fields.Length; i++)
                {
                    var refValue = (EntityRef)fields[i].GetValue(raw)!;
                    if (refValue.IsValid && !map.ContainsKey(refValue.Target))
                        EcsDebug.PrintWarning(
                            $"Level: 实体 {e} 的 {type.Name}.{fields[i].Name} 指向不存在的实例 {refValue.Target.ToHex()}。");
                }
            }
        }
    }
}
