using System.Numerics;
using System.Runtime.InteropServices;
using Foster.Framework;

namespace Engine.Rendering;

/// <summary>
/// 蒙皮网格的顶点布局：在 <see cref="PositionNormalUvVertex"/> 基础上追加
/// Joints（UByte4，关节下标）与 Weights（Float4，cook 时已归一化）。
/// 关节上限见 <see cref="Engine.Animation.SkeletonAnimator.MaxJoints"/>（远小于 256，UByte4 够用）。
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PositionNormalUvSkinVertex : IVertex
{
    public Vector3 Position;
    public Vector3 Normal;
    public Vector2 Uv;
    public Vector4 Tangent;

    /// <summary>4 个关节下标打包进 uint（byte0 | byte1&lt;&lt;8 | …），对应 TEXCOORD4。</summary>
    public uint Joints;
    public Vector4 Weights;

    public readonly VertexFormat Format => format;

    private static readonly VertexFormat format = new([
        new(0, VertexType.Float3, false),
        new(1, VertexType.Float3, false),
        new(2, VertexType.Float2, false),
        new(3, VertexType.Float4, false),
        new(4, VertexType.UByte4, false),
        new(5, VertexType.Float4, false),
    ]);
}
