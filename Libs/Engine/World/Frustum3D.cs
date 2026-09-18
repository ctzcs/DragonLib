using System.Numerics;

namespace Engine.World;

/// <summary>
/// 视锥：6 个归一化平面，xyz 为朝内法线、w 为距离项。
/// 点 p 在视锥内 ⟺ 对所有平面 dot(n, p) + w ≥ 0。
/// </summary>
public readonly struct Frustum3D
{
    public readonly Vector4 Left;
    public readonly Vector4 Right;
    public readonly Vector4 Bottom;
    public readonly Vector4 Top;
    public readonly Vector4 Near;
    public readonly Vector4 Far;

    private Frustum3D(Vector4 left, Vector4 right, Vector4 bottom, Vector4 top, Vector4 near, Vector4 far)
    {
        Left = left;
        Right = right;
        Bottom = bottom;
        Top = top;
        Near = near;
        Far = far;
    }

    /// <summary>
    /// Gribb-Hartmann 提取：行向量约定（v * M）下平面来自 ViewProjection 矩阵【列】的组合。
    /// 深度范围按 [0, w]（Vulkan/D3D12/Metal，SDL GPU 全系如此），近平面即 column2。
    /// </summary>
    public static Frustum3D FromViewProjection(in Matrix4x4 m)
    {
        var column0 = new Vector4(m.M11, m.M21, m.M31, m.M41);
        var column1 = new Vector4(m.M12, m.M22, m.M32, m.M42);
        var column2 = new Vector4(m.M13, m.M23, m.M33, m.M43);
        var column3 = new Vector4(m.M14, m.M24, m.M34, m.M44);

        return new Frustum3D(
            NormalizePlane(column3 + column0),
            NormalizePlane(column3 - column0),
            NormalizePlane(column3 + column1),
            NormalizePlane(column3 - column1),
            NormalizePlane(column2),
            NormalizePlane(column3 - column2));
    }

    /// <summary>
    /// AABB 相交测试（保守正确）：逐平面取法线方向上的正顶点，正顶点落在平面外侧才判整体不可见；
    /// 跨平面边界的盒子一律算可见。
    /// </summary>
    public bool IntersectsAabb(in Vector3 min, in Vector3 max)
        => TestPlane(Left, min, max)
           && TestPlane(Right, min, max)
           && TestPlane(Bottom, min, max)
           && TestPlane(Top, min, max)
           && TestPlane(Near, min, max)
           && TestPlane(Far, min, max);

    private static bool TestPlane(in Vector4 plane, in Vector3 min, in Vector3 max)
    {
        var positive = new Vector3(
            plane.X >= 0f ? max.X : min.X,
            plane.Y >= 0f ? max.Y : min.Y,
            plane.Z >= 0f ? max.Z : min.Z);
        return plane.X * positive.X + plane.Y * positive.Y + plane.Z * positive.Z + plane.W >= 0f;
    }

    private static Vector4 NormalizePlane(Vector4 plane)
    {
        var length = MathF.Sqrt(plane.X * plane.X + plane.Y * plane.Y + plane.Z * plane.Z);
        return length > 1e-8f ? plane / length : plane;
    }
}
