using System.Numerics;
using Engine.World;
using Foster.Framework;
using Xunit;

namespace Game0.EntitiesTests;

/// <summary>
/// SkinningDemo orbit 相机的定量核对：「画面变化 == 相机变化」的映射是否正确。
/// 用 demo 同款轨道公式/实体布局与 Camera3D 真实矩阵计算屏幕位置。
/// 纯 CPU 逻辑，不需要 GraphicsDevice。
/// </summary>
public sealed class CameraOrbitTests
{
    // SkinningDemo 的三个实体沿 X 轴排布，柱中心 y≈1.5（柱身 y∈[0,3]，相机 target y=1.5）。
    private static readonly Vector3[] PillarCenters =
        [new(-2.2f, 1.5f, 0f), new(0f, 1.5f, 0f), new(2.2f, 1.5f, 0f)];

    private static readonly Vector3 Target = new(0f, 1.5f, 0f);
    private const float Distance = 9f;
    private const float Pitch = 0.3f;

    /// <summary>SkinningDemo 同款轨道公式。</summary>
    private static Vector3 OrbitPosition(float yaw)
    {
        var horizontal = MathF.Cos(Pitch) * Distance;
        return Target + new Vector3(
            MathF.Sin(yaw) * horizontal,
            MathF.Sin(Pitch) * Distance,
            MathF.Cos(yaw) * horizontal);
    }

    private static float ScreenX(Vector3 point, Vector3 cameraPosition)
    {
        var camera = new Camera3D
        {
            Target = Target,
            Position = cameraPosition,
            ViewportSize = new Point2(1280, 720),
        };
        camera.Update();
        var clip = Vector4.Transform(new Vector4(point, 1f), camera.ViewProjection);
        return (clip.X / clip.W + 1f) * 0.5f * 1280f;
    }

    [Fact]
    public void OrbitParallaxMatchesUserScreenshots()
    {
        // 用户实测：相机 A（yaw=-2.00，pos=(-8.18,1.19,-3.74)）偏离柱排轴线 ~24.6°，三柱散开；
        // 相机 B（yaw=-1.70，pos=(-8.92,1.05,-1.12)）偏离 ~7.2°，三柱几乎重叠。
        // 直接照抄两截图的相机坐标重算屏幕位置——散开幅度应从 ~133px 收窄到 ~40px。
        var spreadA = Spread(PillarCenters.Select(p => ScreenX(p, new Vector3(-8.18f, 1.19f, -3.74f))));
        var spreadB = Spread(PillarCenters.Select(p => ScreenX(p, new Vector3(-8.92f, 1.05f, -1.12f))));

        Assert.True(spreadA > 100f, $"相机 A 下三柱应散开（实际幅度 {spreadA:F1}px）");
        Assert.True(spreadB < 60f, $"相机 B 下三柱应明显收窄（实际幅度 {spreadB:F1}px）");
        Assert.True(spreadB < spreadA * 0.5f,
            $"B/A 幅度比 {spreadB / spreadA:F2}——沿轴线观察时微小方位角变化会剧烈切换散开/重叠，这是透视特性。");
    }

    [Fact]
    public void DragRightOrbitsCameraRightAndFixedPointMovesLeft()
    {
        // 方向约定（与 Unity/Blender 的 orbit 一致）：向右拖（Mouse.Delta.X > 0）= 相机向右绕场景
        // （看到物体右侧面）= 固定世界点屏幕 x 左移。demo 里 yaw 必须 += delta（-= 就反了）。
        const float yaw = 0.6f;
        const float draggedYaw = yaw + 10f * 0.008f; // 向右拖 10px

        var fixedPoint = new Vector3(2.2f, 1.5f, 0f);
        var before = ScreenX(fixedPoint, OrbitPosition(yaw));
        var after = ScreenX(fixedPoint, OrbitPosition(draggedYaw));

        Assert.True(after < before, $"向右拖后固定点屏幕 x 应左移（{before:F1} → {after:F1}）。");

        // 幅度 sanity：拖 10px 不应产生巨幅跳动（体感「只转一点点却大动」的回归线）。
        Assert.True(before - after < 40f, $"拖 10px 的移动量应平缓（实际 {before - after:F1}px）。");
    }

    private static float Spread(IEnumerable<float> xs)
    {
        var list = xs.ToList();
        return list.Max() - list.Min();
    }
}
