using System.Numerics;
using Engine.Assets.Dasset;
using Engine.Rendering;

namespace Engine.World;

/// <summary>方向归一化，使所有相交距离都以世界单位计。</summary>
public readonly struct Ray3D
{
    public Vector3 Origin { get; }
    public Vector3 Direction { get; }
    public Ray3D(Vector3 origin, Vector3 direction)
    {
        if (!float.IsFinite(direction.LengthSquared()) || direction.LengthSquared() < 1e-20f)
            throw new ArgumentException("Ray direction must be finite and nonzero.", nameof(direction));
        Origin = origin; Direction = Vector3.Normalize(direction);
    }
    public Vector3 At(float distance) => Origin + Direction * distance;
}

public static class Intersections3D
{
    public static bool Aabb(in Ray3D ray, in DassetBounds bounds, out float distance)
    {
        var near = 0f; var far = float.PositiveInfinity;
        for (var axis = 0; axis < 3; axis++)
        {
            var o = ray.Origin[axis]; var d = ray.Direction[axis];
            if (bounds.Min[axis] > bounds.Max[axis]) { distance = 0; return false; }
            if (MathF.Abs(d) < 1e-8f)
            {
                if (o < bounds.Min[axis] || o > bounds.Max[axis]) { distance = 0; return false; }
                continue;
            }
            var a = (bounds.Min[axis] - o) / d; var b = (bounds.Max[axis] - o) / d;
            near = MathF.Max(near, MathF.Min(a, b)); far = MathF.Min(far, MathF.Max(a, b));
            if (near > far) { distance = 0; return false; }
        }
        distance = near; return true;
    }

    public static bool Triangle(in Ray3D ray, Vector3 a, Vector3 b, Vector3 c, out float distance, bool doubleSided = true)
    {
        distance = 0;
        var ab = b - a; var ac = c - a; var p = Vector3.Cross(ray.Direction, ac);
        var det = Vector3.Dot(ab, p);
        if (doubleSided ? MathF.Abs(det) < 1e-8f : det < 1e-8f) return false;
        var t = ray.Origin - a; var u = Vector3.Dot(t, p) / det;
        if (u < 0 || u > 1) return false;
        var q = Vector3.Cross(t, ab); var v = Vector3.Dot(ray.Direction, q) / det;
        if (v < 0 || u + v > 1) return false;
        distance = Vector3.Dot(ac, q) / det; return distance >= 0;
    }

    public static bool Sphere(in Ray3D ray, Vector3 center, float radius, out float distance)
    {
        if (radius < 0) throw new ArgumentOutOfRangeException(nameof(radius));
        distance = 0;
        var offset = ray.Origin - center; var c = offset.LengthSquared() - radius * radius;
        if (c <= 0) return true;
        var b = Vector3.Dot(offset, ray.Direction); var discriminant = b * b - c;
        if (discriminant < 0) return false;
        distance = -b - MathF.Sqrt(discriminant); return distance >= 0;
    }

    public static bool Plane(in Ray3D ray, Vector3 point, Vector3 normal, out float distance)
    {
        distance = 0; var denominator = Vector3.Dot(ray.Direction, normal);
        if (MathF.Abs(denominator) < 1e-8f) return false;
        distance = Vector3.Dot(point - ray.Origin, normal) / denominator; return distance >= 0;
    }

    /// <summary>逐三角形拾取也可使用当前 palette；在世界空间相交，非均匀缩放不会破坏距离排序。</summary>
    public static bool Primitive(in Ray3D ray, DassetPrimitive primitive, in Matrix4x4 world,
        out float distance, ReadOnlySpan<Matrix4x4> palette = default)
    {
        var positions = new Vector3[primitive.IsSkinned ? primitive.SkinVertices!.Length : primitive.Vertices.Length];
        for (var i = 0; i < positions.Length; i++)
        {
            var position = primitive.IsSkinned ? primitive.SkinVertices![i].Position : primitive.Vertices[i].Position;
            if (primitive.IsSkinned && !palette.IsEmpty)
            {
                var v = primitive.SkinVertices![i]; position = Vector3.Zero;
                for (var j = 0; j < 4; j++)
                {
                    var index = (int)((v.Joints >> (j * 8)) & 255);
                    if (index >= palette.Length) index = 0;
                    if (v.Weights[j] != 0)
                        position += Vector3.Transform(v.Position, palette[index]) * v.Weights[j];
                }
            }
            positions[i] = Vector3.Transform(position, world);
        }
        distance = float.PositiveInfinity;
        for (var i = 0; i + 2 < primitive.Indices.Length; i += 3)
            if (Triangle(ray, positions[primitive.Indices[i]], positions[primitive.Indices[i + 1]], positions[primitive.Indices[i + 2]], out var hit))
                distance = MathF.Min(distance, hit);
        return float.IsFinite(distance);
    }
}
