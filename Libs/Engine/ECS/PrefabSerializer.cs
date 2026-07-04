using System.Collections.Generic;
using System.Text.Json;
using DCFApixels.DragonECS;
using Engine.Assets;

namespace Engine.ECS;

/*
 //实例化预制体
var elevatorPrefab = assets.Get<Prefab>(AssetId.FromName("prefabs/elevator"));
int e = PrefabSerializer.Instantiate(world, elevatorPrefab);   // 自动发 SpawnId
//覆盖实例化
int e = PrefabSerializer.Instantiate(world, elevatorPrefab, overrides);
//从实体生成预制体
// prefab 再存成 Content/Prefabs/elevator.json,并 assets.Register 进库
var prefab = PrefabSerializer.Capture(world, selectedEntity, AssetId.FromName("prefabs/elevator"), "Elevator");


override是level概念，覆盖实体的数据存到Level中
 */
public static class PrefabSerializer
{
    // 组件序列化共用的 STJ 配置。IncludeFields 因为 ECS 组件通常是公开字段的 struct。
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        IncludeFields = true,
    };

    /// <summary>
    /// 把一个预制体实例化成实体：套用预制体的默认组件，可选再套用一组字段覆盖，
    /// 最后打上身份组件（PrefabRefComp + 新的 SpawnIdComp）。返回新实体 id。
    /// </summary>
    /// <param name="overrides">相对预制体默认值的覆盖（关卡记录里的 fields）。可为 null。</param>
    /// <param name="spawnId">指定实例身份；默认发一个新号。</param>
    public static int Instantiate(
        EcsWorld world,
        Prefab prefab,
        IReadOnlyDictionary<string, JsonElement>? overrides = null,
        SpawnId spawnId = default)
    {
        int e = world.NewEntity();

        // 1. 套预制体默认组件
        foreach (var (typeName, defaultVal) in prefab.Components)
            ApplyComponent(world, e, typeName, defaultVal);

        // 2. 套字段覆盖（同名组件整体覆盖默认值）
        if (overrides != null)
            foreach (var (typeName, overrideVal) in overrides)
                ApplyComponent(world, e, typeName, overrideVal);

        // 3. 打身份
        world.GetPool<PrefabRefComp>().Add(e).Prefab = prefab.Id;
        world.GetPool<SpawnIdComp>().Add(e).Id = spawnId.IsValid ? spawnId : SpawnId.New();

        return e;
    }

    /// <summary>
    /// 从一个实体反向生成预制体定义（编辑器"把选中对象存成预制体"用）。
    /// 导出实体上除身份组件外的所有组件作为默认值。
    /// </summary>
    public static Prefab Capture(EcsWorld world, int entity, AssetId prefabId, string name)
    {
        var prefab = new Prefab { Id = prefabId, Name = name };
        var pools = world.AllPools;

        foreach (int cid in world.GetComponentTypeIDsFor(entity))
        {
            var pool = pools[cid];
            var type = pool.ComponentType;
            if (type == typeof(SpawnIdComp) || type == typeof(PrefabRefComp)) continue;

            object raw = pool.GetRaw(entity);
            prefab.Components[type.Name] =
                JsonSerializer.SerializeToElement(raw, type, Options);
        }
        return prefab;
    }

    /// <summary>把一个组件的 JSON 值反序列化并写回实体对应 pool。</summary>
    private static void ApplyComponent(EcsWorld world, int entity, string typeName, JsonElement value)
    {
        var type = EcsPoolUtil.ResolveComponentType(typeName);
        if (type == null)
        {
            EcsDebug.PrintWarning($"Prefab: 未知组件类型 '{typeName}'，已跳过。");
            return;
        }

        var pool = EcsPoolUtil.GetOrCreatePoolFor(world, type);
        if (pool == null)
        {
            EcsDebug.PrintWarning($"Prefab: 无法为 '{typeName}' 解析 pool，已跳过。");
            return;
        }

        if (!pool.Has(entity)) pool.AddEmpty(entity);

        // Tag 组件没有字段，AddEmpty 即完成，无需反序列化。
        object boxed = value.Deserialize(type, Options)!;
        pool.SetRaw(entity, boxed);
    }
}

