using System.Numerics;
using System.Runtime.InteropServices;
using Foster.Framework;

namespace Engine.Rendering;

/// <summary>
/// Vertex layout for textured 3D meshes. Tangent.w stores the bitangent
/// handedness (+1/-1) used to build the TBN basis for normal mapping.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PositionNormalUvVertex : IVertex
{
    public Vector3 Position;
    public Vector3 Normal;
    public Vector2 Uv;
    public Vector4 Tangent;

    public PositionNormalUvVertex(Vector3 position, Vector3 normal, Vector2 uv, Vector4 tangent)
    {
        Position = position;
        Normal = normal;
        Uv = uv;
        Tangent = tangent;
    }

    public readonly VertexFormat Format => format;

    private static readonly VertexFormat format = new([
        new(0, VertexType.Float3, false),
        new(1, VertexType.Float3, false),
        new(2, VertexType.Float2, false),
        new(3, VertexType.Float4, false),
    ]);
}
