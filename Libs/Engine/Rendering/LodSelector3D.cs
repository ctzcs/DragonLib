using System.Numerics;
using Engine.Assets.Dasset;
using Engine.World;

namespace Engine.Rendering;

/// <summary>每个模型实例独立持有的 LOD 状态；屏幕高度占比阈值从高到低，最后必须为 0。</summary>
public sealed class LodSelector3D
{
    private readonly float[] _thresholds;
    public int LevelCount => _thresholds.Length;
    public int CurrentLevel { get; private set; } = -1;
    /// <summary>阈值上下的相对滞回范围，避免摄像机在边界附近抖动；0 表示关闭。</summary>
    public float Hysteresis { get; set; } = .1f;

    public LodSelector3D(params float[] minimumScreenHeights)
    {
        ArgumentNullException.ThrowIfNull(minimumScreenHeights);
        if (minimumScreenHeights.Length == 0 || minimumScreenHeights[^1] != 0)
            throw new ArgumentException("LOD thresholds must end with zero.", nameof(minimumScreenHeights));
        for (var i = 0; i < minimumScreenHeights.Length; i++)
            if (!float.IsFinite(minimumScreenHeights[i]) || minimumScreenHeights[i] < 0 ||
                i > 0 && minimumScreenHeights[i] >= minimumScreenHeights[i - 1])
                throw new ArgumentException("LOD thresholds must be finite and strictly descending.", nameof(minimumScreenHeights));
        _thresholds = (float[])minimumScreenHeights.Clone();
    }

    public void Reset() => CurrentLevel = -1;

    public int Select(Camera3D camera, in DassetBounds localBounds, in Matrix4x4 world)
        => Select(ScreenHeight(camera, localBounds.Transformed(world)));

    public int Select(float screenHeight)
    {
        if (float.IsNaN(screenHeight) || screenHeight < 0) throw new ArgumentOutOfRangeException(nameof(screenHeight));
        var hysteresis = float.IsFinite(Hysteresis) ? Math.Clamp(Hysteresis, 0, .49f) : 0;
        if (CurrentLevel < 0)
        {
            CurrentLevel = LevelCount - 1;
            for (var i = 0; i < LevelCount; i++)
                if (screenHeight >= _thresholds[i]) { CurrentLevel = i; break; }
        }
        else
        {
            while (CurrentLevel > 0 && screenHeight >= _thresholds[CurrentLevel - 1] * (1 + hysteresis)) CurrentLevel--;
            while (CurrentLevel < LevelCount - 1 && screenHeight < _thresholds[CurrentLevel] * (1 - hysteresis)) CurrentLevel++;
        }
        return CurrentLevel;
    }

    /// <summary>保守球的投影直径 / 视口高度；相机位于包围球内、近裁面相交或 bounds 不可靠时返回无穷大，选最高细节。</summary>
    public static float ScreenHeight(Camera3D camera, in DassetBounds worldBounds)
    {
        ArgumentNullException.ThrowIfNull(camera);
        var center = (worldBounds.Min + worldBounds.Max) * .5f;
        var radius = Vector3.Distance(worldBounds.Min, worldBounds.Max) * .5f;
        var depth = Vector3.Dot(center - camera.Position, camera.Forward);
        if (!float.IsFinite(radius) || !float.IsFinite(depth) || radius < 0 ||
            depth - radius <= MathF.Max(.001f, camera.NearClip)) return float.PositiveInfinity;
        // 使用球的最近深度，包含离轴情况，宁愿多绘制细节而不低估投影尺寸。
        return radius * MathF.Abs(camera.Projection.M22) / (depth - radius);
    }
}
