using System.Numerics;
using System.Text.Json;
using DCFApixels.DragonECS;
using Engine.Assets;
using Engine.ECS;
using Xunit;

namespace Game0.EntitiesTests;

/// <summary>
/// Transform3DSystem 的层级矩阵传播 + 3D 组件经 prefab 差量序列化的往返。
/// 纯 CPU 逻辑，不需要 GraphicsDevice。
/// </summary>
public sealed class Transform3DTests
{
    private static (EcsDefaultWorld World, int Entity) Spawn(
        EcsDefaultWorld world, SpawnId id, Vector3 position, EntityRef parent = default)
    {
        var e = world.NewEntity();
        world.GetPool<Transform3DComp>().Add(e) = new Transform3DComp(position, Quaternion.Identity, Vector3.One);
        if (parent.IsValid)
            world.GetPool<Parent3DComp>().Add(e) = new Parent3DComp { Parent = parent };
        world.GetPool<SpawnIdComp>().Add(e) = new SpawnIdComp { Id = id };
        return (world, e);
    }

    [Fact]
    public void RootTransformBecomesWorldMatrix()
    {
        var world = new EcsDefaultWorld();
        var (_, e) = Spawn(world, new SpawnId(1), new Vector3(1f, 2f, 3f));

        Transform3DSystem.Run(world);

        var value = world.GetPool<LocalToWorldComp>().Get(e).Value;
        Assert.Equal(new Vector3(1f, 2f, 3f), value.Translation);
    }

    [Fact]
    public void ChildComposesParentWorldMatrix()
    {
        var world = new EcsDefaultWorld();
        var parentId = new SpawnId(11);
        var (_, parent) = Spawn(world, parentId, new Vector3(10f, 0f, 0f));
        var (_, child) = Spawn(world, new SpawnId(12), new Vector3(0f, 5f, 0f), new EntityRef(parentId));

        Transform3DSystem.Run(world);

        var childWorld = world.GetPool<LocalToWorldComp>().Get(child).Value;
        Assert.Equal(new Vector3(10f, 5f, 0f), childWorld.Translation);
    }

    [Fact]
    public void ThreeLevelChainComposesInOrder()
    {
        var world = new EcsDefaultWorld();
        var rootId = new SpawnId(21);
        var middleId = new SpawnId(22);

        Spawn(world, rootId, new Vector3(1f, 0f, 0f));
        var (_, middle) = Spawn(world, middleId, new Vector3(0f, 2f, 0f), new EntityRef(rootId));
        var (_, leaf) = Spawn(world, new SpawnId(23), new Vector3(0f, 0f, 4f), new EntityRef(middleId));

        Transform3DSystem.Run(world);

        var middleWorld = world.GetPool<LocalToWorldComp>().Get(middle).Value;
        Assert.Equal(new Vector3(1f, 2f, 0f), middleWorld.Translation);
        var leafWorld = world.GetPool<LocalToWorldComp>().Get(leaf).Value;
        Assert.Equal(new Vector3(1f, 2f, 4f), leafWorld.Translation);
    }

    [Fact]
    public void RotationAndScaleApplyInTrsOrder()
    {
        var world = new EcsDefaultWorld();
        var e = world.NewEntity();
        world.GetPool<Transform3DComp>().Add(e) = new Transform3DComp(
            new Vector3(5f, 0f, 0f),
            Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2f),
            new Vector3(2f, 2f, 2f));

        Transform3DSystem.Run(world);

        var worldMatrix = world.GetPool<LocalToWorldComp>().Get(e).Value;
        // v * (S * R * T)：先缩放，再绕 Z 旋转 90°，再平移。
        var corner = Vector3.Transform(new Vector3(1f, 0f, 0f), worldMatrix);
        AssertEx.ApproxEquals(new Vector3(5f, 2f, 0f), corner);
    }

    [Fact]
    public void CycleDoesNotHangAndDegradesToRoot()
    {
        var world = new EcsDefaultWorld();
        var aId = new SpawnId(31);
        var bId = new SpawnId(32);
        Spawn(world, aId, new Vector3(1f, 0f, 0f), new EntityRef(bId));
        Spawn(world, bId, new Vector3(0f, 1f, 0f), new EntityRef(aId));

        Transform3DSystem.Run(world); // 不抛异常、不死循环即通过

        var ltw = world.GetPool<LocalToWorldComp>();
        foreach (int e in world.Entities)
            Assert.True(ltw.Has(e));
    }

    [Fact]
    public void MissingParentIsTreatedAsRoot()
    {
        var world = new EcsDefaultWorld();
        var (_, e) = Spawn(world, new SpawnId(41), new Vector3(2f, 0f, 0f), new EntityRef(new SpawnId(999)));

        Transform3DSystem.Run(world);

        Assert.Equal(new Vector3(2f, 0f, 0f), world.GetPool<LocalToWorldComp>().Get(e).Value.Translation);
    }

    [Fact]
    public void PrefabCaptureExcludesDerivedLocalToWorld()
    {
        var world = new EcsDefaultWorld();
        var (_, e) = Spawn(world, new SpawnId(51), new Vector3(1f, 1f, 1f));
        Transform3DSystem.Run(world);

        var prefab = PrefabSerializer.Capture(world, e, "test/model");

        Assert.True(prefab.Components.ContainsKey("Transform3DComp"));
        Assert.False(prefab.Components.ContainsKey("LocalToWorldComp"));
        Assert.False(prefab.Components.ContainsKey("SpawnIdComp"));
    }

    [Fact]
    public void LevelDiffRoundTripsTransform3DAndParent()
    {
        var assets = new AssetDatabase();
        var world = new EcsDefaultWorld();

        // 预制体：默认位于原点。
        var captureWorld = new EcsDefaultWorld();
        var (_, template) = Spawn(captureWorld, new SpawnId(61), Vector3.Zero);
        var prefab = PrefabSerializer.Capture(captureWorld, template, "Prefabs/model3d");
        assets.Register("Prefabs/model3d", prefab);

        // 实例化两个实体：一个根、一个子（父引用 + 位置覆盖）。
        var rootId = new SpawnId(71);
        var childId = new SpawnId(72);
        var root = PrefabSerializer.Instantiate(world, prefab, spawnId: rootId);
        world.GetPool<Transform3DComp>().Get(root).Position = new Vector3(3f, 0f, 0f);

        var child = PrefabSerializer.Instantiate(world, prefab, spawnId: childId);
        world.GetPool<Transform3DComp>().Get(child).Position = new Vector3(0f, 2f, 0f);
        world.GetPool<Parent3DComp>().Add(child) = new Parent3DComp { Parent = new EntityRef(rootId) };

        // 差量保存：只有 Transform3DComp / Parent3DComp 与默认不同。
        var level = LevelSerializer.Save(world, "roundtrip", assets);
        Assert.Equal(2, level.Entities.Count);

        // LocalToWorldComp 若未被排除，会被当作"预制体没有的组件"写进 Fields。
        foreach (var entityData in level.Entities)
            Assert.DoesNotContain(entityData.Fields, kv => kv.Key == "LocalToWorldComp");

        // 加载回一个新 world，验证差量与父引用。
        var reloaded = new EcsDefaultWorld();
        var map = LevelSerializer.Load(reloaded, level, assets);
        Assert.Equal(2, map.Count);

        var rootEntity = (int)map[rootId];
        var childEntity = (int)map[childId];
        Assert.Equal(new Vector3(3f, 0f, 0f), reloaded.GetPool<Transform3DComp>().Get(rootEntity).Position);
        Assert.Equal(new EntityRef(rootId), reloaded.GetPool<Parent3DComp>().Get(childEntity).Parent);

        // 加载后跑一遍系统，父子层级应正确组合。
        Transform3DSystem.Run(reloaded);
        var childWorld = reloaded.GetPool<LocalToWorldComp>().Get(childEntity).Value;
        Assert.Equal(new Vector3(3f, 2f, 0f), childWorld.Translation);
    }

    [Fact]
    public void MeshRendererCompSerializesAsAssetId()
    {
        var comp = new MeshRendererComp(AssetId.FromName("Models/testscene"));
        var json = JsonSerializer.SerializeToElement(comp, PrefabSerializer.Options);
        var round = json.Deserialize<MeshRendererComp>(PrefabSerializer.Options);

        Assert.Equal(comp.Model, round.Model);
        Assert.Equal(-1, round.MeshIndex);
    }

    [Fact]
    public void QuaternionSerializationHasNoDerivedPropertyNoise()
    {
        var comp = new Transform3DComp(
            Vector3.One,
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.5f),
            Vector3.One);
        var json = JsonSerializer.SerializeToElement(comp, PrefabSerializer.Options);

        // IsIdentity 是只读派生属性，不该出现在存盘 JSON 里（历史 bug：差量保存会带出噪声）。
        Assert.False(json.GetProperty("Rotation").TryGetProperty("IsIdentity", out _));

        var round = json.Deserialize<Transform3DComp>(PrefabSerializer.Options);
        AssertEx.ApproxEquals(new Vector3(0f, MathF.Sin(0.25f), 0f), new Vector3(round.Rotation.X, round.Rotation.Y, round.Rotation.Z), 1e-6f);
    }

    private static class AssertEx
    {
        public static void ApproxEquals(Vector3 expected, Vector3 actual, float epsilon = 1e-4f)
        {
            Assert.True(MathF.Abs(expected.X - actual.X) < epsilon &&
                        MathF.Abs(expected.Y - actual.Y) < epsilon &&
                        MathF.Abs(expected.Z - actual.Z) < epsilon,
                $"expected {expected}, got {actual}");
        }
    }
}
