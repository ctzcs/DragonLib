using Foster.Framework;

namespace Engine.Assets.Dasset;

/// <summary>
/// 一个可绘制单元（GPU 侧）：一个 Foster Mesh（顶点格式 PositionNormalUvVertex）+ 其材质。
/// 材质沿用 cook 产物的 <see cref="DassetMaterial"/>（贴图是索引，查 <see cref="DassetModelAsset.Textures"/>）。
/// </summary>
public sealed class DassetMeshPrimitive
{
    public required Mesh Mesh { get; init; }
    public required DassetMaterial Material { get; init; }

    /// <summary>模型局部空间的 AABB（cook 时算出），视锥剔除用：由 Renderer3D 随世界矩阵变换后测试。
    /// 蒙皮 primitive 是 bind pose 的静态包围盒，动画姿态可能超出（保守剔除的已知误差）。</summary>
    public required DassetBounds Bounds { get; init; }

    /// <summary>蒙皮 primitive 的骨架下标（查 <see cref="DassetModelAsset.Skeletons"/>）；静态为 -1。</summary>
    public int SkinIndex { get; init; } = -1;

    public bool IsSkinned => SkinIndex >= 0;
}

/// <summary>
/// 一个 .dasset 模型资产。静态 primitive 的节点变换 cook 时已烘焙进顶点（可直接用
/// Transform3DComp 的世界矩阵绘制）；蒙皮 primitive（<see cref="DassetMeshPrimitive.SkinIndex"/> ≥ 0）
/// 顶点在 mesh bind 空间，渲染时需随 draw 传骨骼 palette（由 AnimationSystem 每帧算出）。
/// </summary>
public sealed class DassetModelAsset : IAsset, IDisposable
{
    public AssetId Id { get; set; }
    public string Name { get; set; } = string.Empty;

    private readonly List<DassetMeshPrimitive> _primitives = [];
    private readonly List<Texture> _textures = [];
    private readonly Dictionary<int, Texture> _colorTextures = [];

    public IReadOnlyList<DassetMeshPrimitive> Primitives => _primitives;
    /// <summary>仅 loader 开启 retainCpuGeometry 时保留，避免普通模型常驻两份几何数据。</summary>
    public IReadOnlyList<DassetPrimitive>? CpuGeometry { get; internal set; }

    /// <summary>GPU 贴图表，下标与 <see cref="DassetMaterial.AlbedoTextureIndex"/> 等一一对应。</summary>
    public IReadOnlyList<Texture> Textures => _textures;

    /// <summary>骨架表（纯数据）；无蒙皮的模型为空。</summary>
    public IReadOnlyList<DassetSkeleton> Skeletons => _skeletons;
    private readonly List<DassetSkeleton> _skeletons = [];

    /// <summary>动画剪辑表（纯数据）。</summary>
    public IReadOnlyList<DassetAnimationClip> Clips => _clips;
    private readonly List<DassetAnimationClip> _clips = [];

    internal void Add(DassetMeshPrimitive primitive) => _primitives.Add(primitive);

    internal void AddTexture(Texture texture) => _textures.Add(texture);

    internal void AddColorTexture(int index, Texture texture) => _colorTextures.Add(index, texture);

    /// <summary>同一源图兼作颜色和数据贴图时，颜色用途拥有独立 sRGB 上传，避免法线被 gamma 解码。</summary>
    public Texture? GetTexture(int index, bool color = false)
    {
        if (index < 0 || index >= _textures.Count) return null;
        return color && _colorTextures.TryGetValue(index, out var texture) ? texture : _textures[index];
    }

    internal void AddSkeleton(DassetSkeleton skeleton) => _skeletons.Add(skeleton);

    internal void AddClip(DassetAnimationClip clip) => _clips.Add(clip);

    public void Dispose()
    {
        foreach (var primitive in _primitives)
            primitive.Mesh.Dispose();
        _primitives.Clear();
        CpuGeometry = null;

        foreach (var texture in _textures)
            texture.Dispose();
        _textures.Clear();
        foreach (var texture in _colorTextures.Values)
            texture.Dispose();
        _colorTextures.Clear();
    }
}
