using System.Numerics;
using DCFApixels.DragonECS;

namespace Engine.ECS;

/// <summary>
/// 每帧把 <see cref="Transform3DComp"/>（含 <see cref="Parent3DComp"/> 层级）传播成
/// <see cref="LocalToWorldComp"/>。矩阵为 System.Numerics 行向量约定（v * M），
/// 与 Renderer3D 的 WorldViewProjection 组合方式一致。
///
/// 父引用按 SpawnId 解析（SpawnIdComp → 实体），对关卡加载与运行时生成的实体一视同仁。
/// 每帧重建映射与排序——实体规模在数百级时开销可忽略；更大规模时可改为按 pool 变更做脏标记。
/// </summary>
public sealed class Transform3DSystem : IUpdateSystem
{
    [DI] private EcsDefaultWorld _world = null!;

    public void Update()
        => Run(_world);

    /// <summary>核心逻辑独立成静态方法，供单元测试在无 pipeline 的情况下直接调用。</summary>
    public static void Run(EcsWorld world)
    {
        var transformPool = world.GetPool<Transform3DComp>();
        var parentPool = world.GetPool<Parent3DComp>();
        var spawnPool = world.GetPool<SpawnIdComp>();
        var localToWorldPool = world.GetPool<LocalToWorldComp>();

        // SpawnId → 实体 映射（含运行时生成、带 SpawnIdComp 的实体）。
        var spawnToEntity = new Dictionary<long, int>();
        foreach (int e in world.Entities)
        {
            if (!spawnPool.Has(e)) continue;
            spawnToEntity[spawnPool.Get(e).Id.Value] = e;
        }

        // 局部矩阵、已解析父实体、层级深度（根 = 0）。失效/自指的父引用按根处理
        // （LevelSerializer 加载时已对失效引用告警，这里不重复刷屏）。
        var locals = new Dictionary<int, Matrix4x4>();
        var parents = new Dictionary<int, int>();
        var depths = new Dictionary<int, int>();
        var onPath = new HashSet<int>();

        foreach (int e in world.Entities)
        {
            if (!transformPool.Has(e)) continue;
            locals[e] = ComposeLocal(transformPool.Get(e));

            if (parentPool.Has(e))
            {
                var parentRef = parentPool.Get(e).Parent;
                if (parentRef.IsValid && spawnToEntity.TryGetValue(parentRef.Target.Value, out var parent) && parent != e)
                    parents[e] = parent;
            }

            depths[e] = ComputeDepth(e, parents, onPath);
        }

        // 按深度升序处理：父的世界矩阵先于子算好，子只需 local * parentWorld。
        var order = new List<int>(locals.Keys);
        order.Sort((a, b) => depths[a].CompareTo(depths[b]));

        var worlds = new Dictionary<int, Matrix4x4>(locals.Count);
        foreach (int e in order)
        {
            var worldMatrix = locals[e];
            if (parents.TryGetValue(e, out var parent) && worlds.TryGetValue(parent, out var parentWorld))
                worldMatrix *= parentWorld;
            worlds[e] = worldMatrix;

            if (localToWorldPool.Has(e))
                localToWorldPool.Get(e) = new LocalToWorldComp { Value = worldMatrix };
            else
                localToWorldPool.Add(e) = new LocalToWorldComp { Value = worldMatrix };
        }
    }

    private static Matrix4x4 ComposeLocal(in Transform3DComp transform)
        => Matrix4x4.CreateScale(transform.Scale)
            * Matrix4x4.CreateFromQuaternion(transform.Rotation)
            * Matrix4x4.CreateTranslation(transform.Position);

    private static int ComputeDepth(int entity, Dictionary<int, int> parents, HashSet<int> onPath)
    {
        var depth = 0;
        var current = entity;
        while (parents.TryGetValue(current, out var parent) && parent != current)
        {
            if (!onPath.Add(current))
                break; // 环：按根处理，避免死循环。
            current = parent;
            depth++;
        }
        onPath.Clear();
        return depth;
    }
}
