using System.Numerics;
using Engine.Assets.Dasset;
using Engine.World;

namespace Engine.Rendering;

/// <summary>一个实例的多级模型引用和 LOD 状态；不拥有模型 GPU 资源，也不生成简化网格。</summary>
public sealed class ModelLod3D
{
    private readonly DassetModelAsset[] _models;
    public LodSelector3D Selector { get; }
    /// <summary>所有级别的共同局部包围盒；动画使用者应覆盖为保守动画包围盒。</summary>
    public DassetBounds Bounds { get; set; }

    public ModelLod3D(params (DassetModelAsset Model, float MinimumScreenHeight)[] levels)
    {
        ArgumentNullException.ThrowIfNull(levels);
        var thresholds = new float[levels.Length];
        for (var i = 0; i < levels.Length; i++) thresholds[i] = levels[i].MinimumScreenHeight;
        Selector = new LodSelector3D(thresholds);
        _models = new DassetModelAsset[levels.Length];
        var bounds = DassetBounds.Empty;
        for (var i = 0; i < levels.Length; i++)
        {
            ArgumentNullException.ThrowIfNull(levels[i].Model);
            _models[i] = levels[i].Model;
            foreach (var primitive in _models[i].Primitives) bounds.Encapsulate(primitive.Bounds);
        }
        Bounds = bounds;
    }

    public DassetModelAsset Select(Camera3D camera, in Matrix4x4 world)
        => _models[Selector.Select(camera, Bounds, world)];
}
