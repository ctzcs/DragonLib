using System.Numerics;
using System.Runtime.InteropServices;
using Engine.World;
using Foster.Framework;

namespace Engine.Rendering;

/// <summary>
/// The default vertex layout for simple lit 3D meshes.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PositionNormalColorVertex : IVertex
{
    public Vector3 Position;
    public Vector3 Normal;
    public Color Color;

    public PositionNormalColorVertex(Vector3 position, Vector3 normal, Color color)
    {
        Position = position;
        Normal = normal;
        Color = color;
    }

    public readonly VertexFormat Format => format;

    private static readonly VertexFormat format = new([
        new(0, VertexType.Float3, false),
        new(1, VertexType.Float3, false),
        new(2, VertexType.UByte4, true),
    ]);
}

/// <summary>
/// Owns a simple indexed mesh and submits it with depth testing enabled.
/// </summary>
public sealed class Mesh3D : IDisposable
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct VertexUniforms
    {
        public Matrix4x4 WorldViewProjection;
        public Matrix4x4 World;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct FragmentUniforms
    {
        public Vector4 LightDirection;
        public Vector4 Ambient;
        public Vector4 Diffuse;
    }

    private readonly GraphicsDevice _graphicsDevice;

    public Mesh<PositionNormalColorVertex, uint> Geometry { get; }
    public Material Material { get; }

    public Mesh3D(
        GraphicsDevice graphicsDevice,
        Material material,
        ReadOnlySpan<PositionNormalColorVertex> vertices,
        ReadOnlySpan<uint> indices,
        string? name = null)
    {
        _graphicsDevice = graphicsDevice;
        Material = material;
        Geometry = new Mesh<PositionNormalColorVertex, uint>(graphicsDevice, name);
        Geometry.SetVertices(vertices);
        Geometry.SetIndices(indices);
    }

    public void Draw(
        IDrawableTarget target,
        Camera3D camera,
        in Matrix4x4 world,
        in Vector3 lightDirection,
        in Vector3 ambient,
        in Vector3 diffuse)
    {
        Material.Vertex.SetUniformBuffer(new VertexUniforms
        {
            WorldViewProjection = world * camera.ViewProjection,
            World = world,
        });
        Material.Fragment.SetUniformBuffer(new FragmentUniforms
        {
            LightDirection = new Vector4(lightDirection, 0f),
            Ambient = new Vector4(ambient, 1f),
            Diffuse = new Vector4(diffuse, 1f),
        });

        _graphicsDevice.Draw(new DrawCommand(target, Geometry, Material)
        {
            BlendMode = BlendMode.NonPremultiplied,
            CullMode = CullMode.Back,
            DepthCompare = DepthCompare.LessOrEqual,
            DepthTestEnabled = true,
            DepthWriteEnabled = true,
        });
    }

    public static Mesh3D CreateCube(
        GraphicsDevice graphicsDevice,
        Material material,
        float size = 1f,
        Color? color = null,
        string? name = null)
    {
        var half = size * 0.5f;
        var faceColor = color ?? Color.White;
        var vertices = new List<PositionNormalColorVertex>(24);
        var indices = new List<uint>(36);

        AddFace(vertices, indices, new Vector3(0f, 0f, 1f),
            new Vector3(-half, -half, half), new Vector3(half, -half, half),
            new Vector3(half, half, half), new Vector3(-half, half, half), faceColor);
        AddFace(vertices, indices, new Vector3(0f, 0f, -1f),
            new Vector3(half, -half, -half), new Vector3(-half, -half, -half),
            new Vector3(-half, half, -half), new Vector3(half, half, -half), faceColor);
        AddFace(vertices, indices, new Vector3(0f, 1f, 0f),
            new Vector3(-half, half, half), new Vector3(half, half, half),
            new Vector3(half, half, -half), new Vector3(-half, half, -half), faceColor);
        AddFace(vertices, indices, new Vector3(0f, -1f, 0f),
            new Vector3(-half, -half, -half), new Vector3(half, -half, -half),
            new Vector3(half, -half, half), new Vector3(-half, -half, half), faceColor);
        AddFace(vertices, indices, new Vector3(1f, 0f, 0f),
            new Vector3(half, -half, half), new Vector3(half, -half, -half),
            new Vector3(half, half, -half), new Vector3(half, half, half), faceColor);
        AddFace(vertices, indices, new Vector3(-1f, 0f, 0f),
            new Vector3(-half, -half, -half), new Vector3(-half, -half, half),
            new Vector3(-half, half, half), new Vector3(-half, half, -half), faceColor);

        return new Mesh3D(graphicsDevice, material, CollectionsMarshal.AsSpan(vertices), CollectionsMarshal.AsSpan(indices), name);
    }

    private static void AddFace(
        List<PositionNormalColorVertex> vertices,
        List<uint> indices,
        Vector3 normal,
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Vector3 d,
        Color color)
    {
        var start = (uint)vertices.Count;
        vertices.Add(new(a, normal, color));
        vertices.Add(new(b, normal, color));
        vertices.Add(new(c, normal, color));
        vertices.Add(new(d, normal, color));
        indices.Add(start);
        indices.Add(start + 1);
        indices.Add(start + 2);
        indices.Add(start);
        indices.Add(start + 2);
        indices.Add(start + 3);
    }

    public void Dispose()
    {
        Geometry.Dispose();
    }
}
