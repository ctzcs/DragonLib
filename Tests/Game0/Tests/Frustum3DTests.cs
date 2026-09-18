using System.Numerics;
using Engine.Assets.Dasset;
using Engine.World;
using Xunit;

namespace Game0.EntitiesTests;

/// <summary>
/// 视锥剔除的纯数学：Frustum3D 平面提取（Gribb-Hartmann）、AABB 在/不在/跨边界、
/// DassetBounds 经世界矩阵变换后的 AABB。纯 CPU 逻辑，不需要 GraphicsDevice。
/// </summary>
public sealed class Frustum3DTests
{
    [Fact]
    public void IdentityViewProjectionYieldsUnitCubePlanes()
    {
        // 单位 VP 等价 [-1,1]² × [0,1] 的正交盒，可精确验证各平面系数。
        var frustum = Frustum3D.FromViewProjection(Matrix4x4.Identity);

        Assert.Equal(new Vector4(1f, 0f, 0f, 1f), frustum.Left);
        Assert.Equal(new Vector4(-1f, 0f, 0f, 1f), frustum.Right);
        Assert.Equal(new Vector4(0f, 1f, 0f, 1f), frustum.Bottom);
        Assert.Equal(new Vector4(0f, -1f, 0f, 1f), frustum.Top);
        Assert.Equal(new Vector4(0f, 0f, 1f, 0f), frustum.Near);
        Assert.Equal(new Vector4(0f, 0f, -1f, 1f), frustum.Far);
    }

    [Fact]
    public void PerspectiveFrustumClassifiesBoxes()
    {
        // 相机在 (0,0,5) 朝 -Z 看原点，FOV 60°，near 0.1，far 100。
        var viewProjection = Matrix4x4.CreateLookAt(new Vector3(0f, 0f, 5f), Vector3.Zero, Vector3.UnitY)
            * Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3f, 1f, 0.1f, 100f);
        var frustum = Frustum3D.FromViewProjection(viewProjection);

        // 原点附近的盒子：在。
        Assert.True(frustum.IntersectsAabb(new Vector3(-1f), new Vector3(1f)));
        // 相机身后的盒子：不在。
        Assert.False(frustum.IntersectsAabb(new Vector3(-1f, -1f, 19f), new Vector3(1f, 1f, 21f)));
        // 远超远平面的盒子：不在。
        Assert.False(frustum.IntersectsAabb(new Vector3(-1f, -1f, -210f), new Vector3(1f, 1f, -200f)));
        // 侧面远超视角的盒子：不在。
        Assert.False(frustum.IntersectsAabb(new Vector3(99f, -1f, -1f), new Vector3(101f, 1f, 1f)));
        // 跨右边界的盒子（距相机 5 处右边界约 x≈2.89，盒子跨两侧）：保守算在。
        Assert.True(frustum.IntersectsAabb(new Vector3(2.5f, -0.5f, -0.5f), new Vector3(3.5f, 0.5f, 0.5f)));
        // 套着相机的盒子（跨近平面）：保守算在。
        Assert.True(frustum.IntersectsAabb(new Vector3(-0.5f, -0.5f, 4.5f), new Vector3(0.5f, 0.5f, 5.5f)));
    }

    [Fact]
    public void TranslatedBoundsShiftWithWorldMatrix()
    {
        var local = new DassetBounds { Min = new Vector3(-1f), Max = new Vector3(1f) };

        var world = local.Transformed(Matrix4x4.CreateTranslation(5f, 0f, 0f));

        Assert.Equal(new Vector3(4f, -1f, -1f), world.Min);
        Assert.Equal(new Vector3(6f, 1f, 1f), world.Max);
    }

    [Fact]
    public void RotatedBoundsStayAxisAlignedAndConservative()
    {
        var local = new DassetBounds { Min = new Vector3(1f, -1f, -2f), Max = new Vector3(3f, 1f, 2f) };

        // 绕 Y 转 90°：(x,z) → (z,-x)，x 展宽到原 z 的范围，z 到 -x 的范围。
        var world = local.Transformed(Matrix4x4.CreateRotationY(MathF.PI / 2f));

        Assert.True(Vector3.Distance(new Vector3(-2f, -1f, -3f), world.Min) < 1e-4f);
        Assert.True(Vector3.Distance(new Vector3(2f, 1f, -1f), world.Max) < 1e-4f);
    }

    [Fact]
    public void CulledDecisionMatchesManualPlaneTest()
    {
        // 与 Renderer3D.End() 相同的判定路径：local bounds → world → frustum 测试。
        var viewProjection = Matrix4x4.CreateLookAt(new Vector3(0f, 0f, 5f), Vector3.Zero, Vector3.UnitY)
            * Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3f, 1f, 0.1f, 100f);
        var frustum = Frustum3D.FromViewProjection(viewProjection);
        var local = new DassetBounds { Min = new Vector3(-0.5f), Max = new Vector3(0.5f) };

        var visible = local.Transformed(Matrix4x4.CreateTranslation(0f, 0f, 0f));
        var culled = local.Transformed(Matrix4x4.CreateTranslation(0f, 0f, 30f));

        Assert.True(frustum.IntersectsAabb(visible.Min, visible.Max));
        Assert.False(frustum.IntersectsAabb(culled.Min, culled.Max));
    }
}
