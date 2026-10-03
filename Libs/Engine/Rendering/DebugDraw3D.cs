using System.Numerics;
using System.Runtime.InteropServices;
using Engine.Assets.Dasset;
using Engine.World;
using Foster.Framework;

namespace Engine.Rendering;

/// <summary>立即模式调试线；Render 后清空，深度测试可选，始终不写深度。</summary>
public sealed class DebugDraw3D : IDisposable
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct LineVertex(Vector3 position, Vector4 color) : IVertex
    {
        public Vector3 Position = position;
        public Vector4 Color = color;
        private static readonly VertexFormat format = new([new(0, VertexType.Float3, false), new(1, VertexType.Float4, false)]);
        public readonly VertexFormat Format => format;
    }
    private readonly List<LineVertex> _vertices = [];
    private readonly VertexBuffer<LineVertex> _buffer;
    private readonly EmbeddedShaderMaterial _shader;
    public int LineCount => _vertices.Count / 2;
    public bool DepthTestEnabled = true;

    public DebugDraw3D(GraphicsDevice device)
    {
        _shader = EmbeddedShaderMaterial.Load(device, typeof(DebugDraw3D).Assembly, "Engine/Shaders/DebugLine3D",
            new ShaderStageSpec(0, 0, "fragment_main"), new ShaderStageSpec(0, 1, "vertex_main"));
        _buffer = new VertexBuffer<LineVertex>(device, "Debug lines 3D");
    }
    private static Vector4 ColorVector(Color c) => new(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
    public void Line(Vector3 a, Vector3 b, Color color)
    {
        var c = ColorVector(color); _vertices.Add(new(a, c)); _vertices.Add(new(b, c));
    }
    public void Clear() => _vertices.Clear();
    public void Aabb(in DassetBounds bounds, Color color)
    {
        Span<Vector3> corners = stackalloc Vector3[8];
        for (var i = 0; i < 8; i++) corners[i] = new((i & 1) == 0 ? bounds.Min.X : bounds.Max.X,
            (i & 2) == 0 ? bounds.Min.Y : bounds.Max.Y, (i & 4) == 0 ? bounds.Min.Z : bounds.Max.Z);
        BoxEdges(corners, color);
    }
    private void BoxEdges(ReadOnlySpan<Vector3> corners, Color color)
    {
        for (var i = 0; i < 8; i++)
            for (var bit = 1; bit <= 4; bit <<= 1)
                if ((i & bit) == 0) Line(corners[i], corners[i | bit], color);
    }
    public void Sphere(Vector3 center, float radius, Color color, int segments = 32)
    {
        if (radius < 0 || segments < 3) throw new ArgumentOutOfRangeException(nameof(radius));
        for (var plane = 0; plane < 3; plane++)
            for (var i = 0; i < segments; i++)
            {
                var a = i * MathF.Tau / segments; var b = (i + 1) * MathF.Tau / segments;
                Vector3 Point(float angle) => plane == 0 ? new(MathF.Cos(angle), MathF.Sin(angle), 0)
                    : plane == 1 ? new(0, MathF.Cos(angle), MathF.Sin(angle)) : new(MathF.Sin(angle), 0, MathF.Cos(angle));
                Line(center + Point(a) * radius, center + Point(b) * radius, color);
            }
    }
    public void Frustum(in Matrix4x4 viewProjection, Color color)
    {
        if (!Matrix4x4.Invert(viewProjection, out var inverse)) return;
        Span<Vector3> corners = stackalloc Vector3[8];
        for (var i = 0; i < 8; i++)
        {
            var p = Vector4.Transform(new Vector4((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? 0 : 1, 1), inverse);
            corners[i] = new Vector3(p.X, p.Y, p.Z) / p.W;
        }
        BoxEdges(corners, color);
    }
    public void Axis(in Matrix4x4 world, float length = 1)
    {
        var origin = world.Translation;
        Line(origin, Vector3.Transform(Vector3.UnitX * length, world), Color.Red);
        Line(origin, Vector3.Transform(Vector3.UnitY * length, world), Color.Green);
        Line(origin, Vector3.Transform(Vector3.UnitZ * length, world), Color.Blue);
    }
    public void Grid(int halfLines, float spacing, Color color)
    {
        if (halfLines < 0 || spacing <= 0) throw new ArgumentOutOfRangeException(nameof(halfLines));
        var extent = halfLines * spacing;
        for (var i = -halfLines; i <= halfLines; i++)
        {
            Line(new(i * spacing, 0, -extent), new(i * spacing, 0, extent), color);
            Line(new(-extent, 0, i * spacing), new(extent, 0, i * spacing), color);
        }
    }
    public void Skeleton(DassetSkeleton skeleton, ReadOnlySpan<Matrix4x4> matrices, in Matrix4x4 world,
        Color color, bool isPalette = true)
    {
        var count = Math.Min(skeleton.Joints.Count, matrices.Length);
        var positions = new Vector3[count];
        for (var i = 0; i < count; i++)
        {
            var global = matrices[i];
            if (isPalette && Matrix4x4.Invert(skeleton.Joints[i].InverseBindMatrix, out var bind)) global = bind * global;
            positions[i] = Vector3.Transform(global.Translation, world);
        }
        for (var i = 0; i < count; i++)
        {
            var parent = skeleton.Joints[i].ParentIndex;
            if (parent >= 0 && parent < count) Line(positions[parent], positions[i], color);
        }
    }
    public void Render(IDrawableTarget target, Camera3D camera)
    {
        if (_vertices.Count == 0) return;
        try
        {
            camera.ViewportSize = new(target.WidthInPixels, target.HeightInPixels);
            _buffer.Clear();
            _buffer.Upload(CollectionsMarshal.AsSpan(_vertices));
            _shader.Material.Vertex.SetUniformBuffer(camera.ViewProjection);
            target.GraphicsDevice.Draw(new DrawCommand(target, _buffer, _shader.Material)
            {
                Topology = PrimitiveTopology.Lines, CullMode = CullMode.None,
                DepthTestEnabled = DepthTestEnabled, DepthCompare = DepthCompare.LessOrEqual,
                DepthWriteEnabled = false, BlendMode = BlendMode.NonPremultiplied,
            });
        }
        finally { Clear(); }
    }
    public void Dispose() { _buffer.Dispose(); _shader.Dispose(); Clear(); }
}

