using System.Numerics;
using DCFApixels.DragonECS;
using Engine.Assets;
using Engine.Assets.Dasset;
using Engine.Rendering;

namespace Engine.ECS;

public readonly record struct MeshRenderRequest(AssetId Model, int MeshIndex, Matrix4x4 World, Matrix4x4[]? Palette);

/// <summary>
/// 只排队 draw；游戏负责 Begin/End、相机、渲染目标和光照。材质由注入的 <see cref="IModelMaterials3D"/> 提供，
/// 用标准画风时注入 MaterialCache。
/// </summary>
public sealed class MeshRenderSystem : IRenderSystem
{
    [DI] private EcsDefaultWorld _world = null!;
    [DI] private AssetDatabase _assets = null!;
    [DI] private Renderer3D _renderer = null!;
    [DI] private IModelMaterials3D _materials = null!;

    public void Render() => Submit(_world, _assets, _renderer, _materials);

    /// <summary>筛选与排队入口独立于 GPU，供关卡和单元测试检查提交数据。</summary>
    public static void Collect(EcsWorld world, Action<MeshRenderRequest> enqueue)
    {
        var meshPool = world.GetPool<MeshRendererComp>();
        var transformPool = world.GetPool<LocalToWorldComp>();
        var palettePool = world.GetPool<SkinPaletteComp>();
        foreach (int e in world.Entities)
        {
            if (!meshPool.Has(e) || !transformPool.Has(e))
                continue;
            var mesh = meshPool.Get(e);
            enqueue(new MeshRenderRequest(mesh.Model, mesh.MeshIndex, transformPool.Get(e).Value,
                palettePool.Has(e) ? palettePool.Get(e).Matrices : null));
        }
    }

    public static void Submit(EcsWorld world, AssetDatabase assets, Renderer3D renderer, IModelMaterials3D materials)
    {
        if (!renderer.IsActive)
            throw new InvalidOperationException("MeshRenderSystem requires an active Renderer3D pass.");
        Collect(world, request =>
        {
            var model = assets.Get<DassetModelAsset>(request.Model);
            if (model != null)
                renderer.DrawModel(model, materials.Get(model), request.World, request.Palette, request.MeshIndex);
        });
    }
}
