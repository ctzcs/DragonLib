using System.Numerics;
using Engine.Rendering;
using Foster.Framework;

namespace Engine.Assets.Dasset;

/// <summary>轴对齐包围盒（min/max）。cook 时由烘焙后的顶点算出，运行时供视锥剔除等使用。</summary>
public struct DassetBounds
{
    public Vector3 Min;
    public Vector3 Max;

    /// <summary>反转极值的"空"盒：Encapsulate 任意点后即为正常盒。</summary>
    public static DassetBounds Empty => new()
    {
        Min = new Vector3(float.PositiveInfinity),
        Max = new Vector3(float.NegativeInfinity),
    };

    public void Encapsulate(Vector3 point)
    {
        Min = Vector3.Min(Min, point);
        Max = Vector3.Max(Max, point);
    }

    public void Encapsulate(DassetBounds other)
    {
        Min = Vector3.Min(Min, other.Min);
        Max = Vector3.Max(Max, other.Max);
    }

    /// <summary>8 角点逐个变换后重新包围：旋转/缩放下结果仍轴对齐（略偏大但保守正确，供视锥剔除用）。</summary>
    public readonly DassetBounds Transformed(in Matrix4x4 transform)
    {
        var result = Empty;
        for (var i = 0; i < 8; i++)
        {
            var corner = new Vector3(
                (i & 1) == 0 ? Min.X : Max.X,
                (i & 2) == 0 ? Min.Y : Max.Y,
                (i & 4) == 0 ? Min.Z : Max.Z);
            result.Encapsulate(Vector3.Transform(corner, transform));
        }
        return result;
    }
}

/// <summary>贴图表条目：原始 PNG/JPG 字节（不解码），运行时再由 Foster Image 解码上传。</summary>
public sealed class DassetTextureEntry
{
    public string Name = string.Empty;
    public DassetTextureCodec Codec;
    public byte[] Bytes = [];
}

/// <summary>
/// 材质的纯数据记录：PBR 数值参数 + 贴图表索引（-1 表示无）。
/// 运行时装配 GPU 资源时按索引查 <see cref="DassetModel.Textures"/>。
/// </summary>
public sealed class DassetMaterial
{
    public Vector4 BaseColorFactor = Vector4.One;
    public float Metallic;
    public float Roughness = 1f;
    public bool DoubleSided;
    public DassetAlphaMode AlphaMode = DassetAlphaMode.Opaque;
    public float AlphaCutoff = 0.5f;
    public int AlbedoTextureIndex = -1;
    public int NormalTextureIndex = -1;
    public int MetallicRoughnessTextureIndex = -1;
    public int OcclusionTextureIndex = -1;
    public float OcclusionStrength = 1f;
    public int EmissiveTextureIndex = -1;
    public Vector3 EmissiveFactor;
    public float NormalScale = 1f;

    public bool HasNormalMap => NormalTextureIndex >= 0;

    /// <summary>
    /// 推导本次 draw 的渲染状态：Blend 进透明队列（alpha 混合、不写深度）；
    /// Mask 留在不透明队列（cutout 由 shader clip 处理，<see cref="AlphaCutoff"/> 走材质 uniform）；
    /// DoubleSided 一律关面剔除。
    /// </summary>
    public RenderState3D ToRenderState()
    {
        var state = AlphaMode == DassetAlphaMode.Blend ? RenderState3D.Transparent : RenderState3D.Opaque;
        if (DoubleSided)
            state.Cull = CullMode.None;
        return state;
    }
}

/// <summary>一个可绘制单元的纯数据：顶点/索引 + 材质 + 自身 AABB。</summary>
public sealed class DassetPrimitive
{
    /// <summary>静态顶点（PositionNormalUvVertex，cook 时已把节点世界矩阵烘焙进去）。</summary>
    public PositionNormalUvVertex[] Vertices = [];

    /// <summary>蒙皮顶点（非 null 即蒙皮 primitive）：顶点在 mesh bind 空间，由骨骼 palette 驱动。</summary>
    public PositionNormalUvSkinVertex[]? SkinVertices;

    /// <summary>蒙皮 primitive 使用的骨架在 <see cref="DassetModel.Skeletons"/> 里的下标；静态为 -1。</summary>
    public int SkinIndex = -1;

    public uint[] Indices = [];
    public DassetMaterial Material = new();
    public DassetBounds Bounds;

    public bool IsSkinned => SkinVertices != null;
}

/// <summary>
/// cook 产物（纯数据 POCO，不碰 GraphicsDevice）：贴图表 + 逐 primitive 顶点/索引/材质/AABB
/// + 骨架/动画剪辑（v2）。静态 primitive 的节点变换烘焙进顶点；蒙皮 primitive 顶点保持
/// mesh bind 空间，由骨骼 palette 驱动。
/// 由 GltfModelCooker（DragonLib.Gltf，离线）生成，经 <see cref="DassetWriter"/> 落盘；
/// 运行时由 <see cref="DassetReader"/> 读回、DassetModelLoader 上传 GPU。
/// </summary>
public sealed class DassetModel
{
    public List<DassetTextureEntry> Textures = [];
    public List<DassetPrimitive> Primitives = [];

    /// <summary>骨架表（v2 新增，蒙皮模型通常 1 个）；primitive 按 <see cref="DassetPrimitive.SkinIndex"/> 引用。</summary>
    public List<DassetSkeleton> Skeletons = [];

    /// <summary>动画剪辑表（v2 新增）。播放逻辑见 Engine.Animation.SkeletonAnimator。</summary>
    public List<DassetAnimationClip> Clips = [];

    public DassetBounds Bounds;
}
