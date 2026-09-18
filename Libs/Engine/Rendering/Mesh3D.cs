using System.Numerics;
using System.Runtime.InteropServices;
using Engine.Assets.Dasset;
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
/// Owns 3D vertex/index data.
/// Rendering is performed by <see cref="Renderer3D"/>.
/// </summary>
public sealed class Mesh3D : IDisposable
{
    public Mesh<PositionNormalColorVertex, uint> Geometry { get; }

    /// <summary>局部空间 AABB（构造时由顶点算出），视锥剔除用。</summary>
    public DassetBounds Bounds { get; }

    public Mesh3D(
        GraphicsDevice graphicsDevice,
        ReadOnlySpan<PositionNormalColorVertex> vertices,
        ReadOnlySpan<uint> indices,
        string? name = null)
    {
        Geometry = new Mesh<PositionNormalColorVertex, uint>(graphicsDevice, name);
        Geometry.SetVertices(vertices);
        Geometry.SetIndices(indices);

        var bounds = DassetBounds.Empty;
        foreach (var vertex in vertices)
            bounds.Encapsulate(vertex.Position);
        Bounds = bounds;
    }

    public static Mesh3D CreateCube(
        GraphicsDevice graphicsDevice,
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

        return new Mesh3D(graphicsDevice, CollectionsMarshal.AsSpan(vertices), CollectionsMarshal.AsSpan(indices), name);
    }

    /// <summary>
    /// Creates a faceted icosphere. Roughness offsets shared points radially before
    /// the triangle vertices are split, keeping the mesh watertight.
    /// </summary>
    public static Mesh3D CreateIcosphere(
        GraphicsDevice graphicsDevice,
        float radius = 1f,
        int subdivisions = 0,
        float roughness = 0f,
        Color? color = null,
        int seed = 0,
        string? name = null)
    {
        if (radius <= 0f)
            throw new ArgumentOutOfRangeException(nameof(radius));
        if (subdivisions is < 0 or > 6)
            throw new ArgumentOutOfRangeException(nameof(subdivisions));
        if (roughness is < 0f or > 0.95f)
            throw new ArgumentOutOfRangeException(nameof(roughness));

        var goldenRatio = (1f + MathF.Sqrt(5f)) * 0.5f;
        var points = new List<Vector3>
        {
            new(-1f, goldenRatio, 0f), new(1f, goldenRatio, 0f),
            new(-1f, -goldenRatio, 0f), new(1f, -goldenRatio, 0f),
            new(0f, -1f, goldenRatio), new(0f, 1f, goldenRatio),
            new(0f, -1f, -goldenRatio), new(0f, 1f, -goldenRatio),
            new(goldenRatio, 0f, -1f), new(goldenRatio, 0f, 1f),
            new(-goldenRatio, 0f, -1f), new(-goldenRatio, 0f, 1f),
        };

        var faces = new List<(int A, int B, int C)>
        {
            (0, 11, 5), (0, 5, 1), (0, 1, 7), (0, 7, 10), (0, 10, 11),
            (1, 5, 9), (5, 11, 4), (11, 10, 2), (10, 7, 6), (7, 1, 8),
            (3, 9, 4), (3, 4, 2), (3, 2, 6), (3, 6, 8), (3, 8, 9),
            (4, 9, 5), (2, 4, 11), (6, 2, 10), (8, 6, 7), (9, 8, 1),
        };

        for (var level = 0; level < subdivisions; level++)
        {
            var midpointCache = new Dictionary<ulong, int>();
            var nextFaces = new List<(int A, int B, int C)>(faces.Count * 4);

            foreach (var (a, b, c) in faces)
            {
                var ab = GetMidpoint(a, b, points, midpointCache);
                var bc = GetMidpoint(b, c, points, midpointCache);
                var ca = GetMidpoint(c, a, points, midpointCache);
                nextFaces.Add((a, ab, ca));
                nextFaces.Add((b, bc, ab));
                nextFaces.Add((c, ca, bc));
                nextFaces.Add((ab, bc, ca));
            }

            faces = nextFaces;
        }

        var random = new Random(seed);
        for (var i = 0; i < points.Count; i++)
        {
            var radialScale = 1f + ((random.NextSingle() * 2f - 1f) * roughness);
            points[i] = Vector3.Normalize(points[i]) * radius * radialScale;
        }

        var faceColor = color ?? Color.White;
        var vertices = new List<PositionNormalColorVertex>(faces.Count * 3);
        var indices = new List<uint>(faces.Count * 3);
        foreach (var (aIndex, bIndex, cIndex) in faces)
        {
            var a = points[aIndex];
            var b = points[bIndex];
            var c = points[cIndex];
            var normal = Vector3.Normalize(Vector3.Cross(b - a, c - a));
            if (Vector3.Dot(normal, a + b + c) < 0f)
            {
                (b, c) = (c, b);
                normal = -normal;
            }

            var start = (uint)vertices.Count;
            vertices.Add(new(a, normal, faceColor));
            vertices.Add(new(b, normal, faceColor));
            vertices.Add(new(c, normal, faceColor));
            // 法线已保证朝外，(a,b,c) 叉积与法线同向：外侧 CCW 即正面，索引保持顶点序。
            indices.Add(start);
            indices.Add(start + 1);
            indices.Add(start + 2);
        }

        return new Mesh3D(graphicsDevice, CollectionsMarshal.AsSpan(vertices), CollectionsMarshal.AsSpan(indices), name);
    }

    private static int GetMidpoint(
        int first,
        int second,
        List<Vector3> points,
        Dictionary<ulong, int> cache)
    {
        var low = (uint)Math.Min(first, second);
        var high = (uint)Math.Max(first, second);
        var key = ((ulong)low << 32) | high;
        if (cache.TryGetValue(key, out var existing))
            return existing;

        var index = points.Count;
        points.Add(Vector3.Normalize(points[first] + points[second]));
        cache.Add(key, index);
        return index;
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
        // 正面 = 从外侧看逆时针（CCW）：(a,b,c) 的叉积与面法线同向（见 GltfModelCooker 头部注释的约定推导）。
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
