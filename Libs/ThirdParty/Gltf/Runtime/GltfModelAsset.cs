using System.Numerics;
using Engine.Assets;
using Foster.Framework;

namespace DragonLib.Gltf;

/// <summary>
/// 一个 glTF 材质的引擎侧记录：贴图（已解码为 GPU Texture）+ PBR 数值参数。
/// 渲染时由 Standard3D 着色器消费（albedo/normal 采样 + factor）。
/// </summary>
public sealed class GltfMaterial
{
    public Texture? AlbedoTexture;
    public Texture? NormalTexture;
    public Vector4 BaseColorFactor = Vector4.One;
    public float Metallic;
    public float Roughness = 1f;
    public bool DoubleSided;

    public bool HasNormalMap => NormalTexture != null;
}

/// <summary>
/// 一个可绘制单元：一个 Foster Mesh（顶点格式 PositionNormalUvVertex）+ 其材质。
/// </summary>
public sealed class GltfPrimitive
{
    public required Mesh Mesh { get; init; }
    public required GltfMaterial Material { get; init; }
}

/// <summary>
/// 一个 glTF 模型资产。加载时已把场景图的节点变换烘焙进顶点（静态模型，动画/蒙皮不在本期范围），
/// 因此 Primitives 中的 mesh 都在模型局部空间中，可直接用 Transform3DComp 的世界矩阵绘制。
/// </summary>
public sealed class GltfModelAsset : IAsset, IDisposable
{
    public AssetId Id { get; set; }
    public string Name { get; set; } = string.Empty;

    private readonly List<GltfPrimitive> _primitives = [];
    private readonly List<Texture> _textures = [];

    public IReadOnlyList<GltfPrimitive> Primitives => _primitives;

    internal void Add(GltfPrimitive primitive) => _primitives.Add(primitive);

    internal void TrackTexture(Texture texture) => _textures.Add(texture);

    public void Dispose()
    {
        foreach (var primitive in _primitives)
            primitive.Mesh.Dispose();
        _primitives.Clear();

        foreach (var texture in _textures)
            texture.Dispose();
        _textures.Clear();
    }
}
