using System.Numerics;
using Engine.Rendering;
using Xunit;

namespace Game0.EntitiesTests;

/// <summary>
/// PointLight3D.Pack 的 cbuffer 打包布局（与 Standard3D/LightSandbox 的点光块对齐）。
/// 纯 CPU 逻辑，不需要 GraphicsDevice。
/// </summary>
public sealed class PointLight3DTests
{
    [Fact]
    public void PackEmptyWritesZeroCount()
    {
        var buffer = new float[PointLight3D.PackedFloatCount];

        var count = PointLight3D.Pack([], buffer);

        Assert.Equal(0, count);
        Assert.Equal(0f, buffer[0]);
        Assert.All(buffer, value => Assert.Equal(0f, value));
    }

    [Fact]
    public void PackWritesPositionRangeAndColorIntensityAtFixedOffsets()
    {
        var lights = new[]
        {
            new PointLight3D(new Vector3(1f, 2f, 3f), 4f, new Vector3(0.5f, 0.6f, 0.7f), 2f),
            new PointLight3D(new Vector3(-1f, -2f, -3f), 8f, new Vector3(0.1f, 0.2f, 0.3f), 1.5f),
        };
        var buffer = new float[PointLight3D.PackedFloatCount];

        var count = PointLight3D.Pack(lights, buffer);

        Assert.Equal(2, count);
        Assert.Equal(2f, buffer[0]);

        // 第一盏：position/range 在 [4..7]，color/intensity 在 [68..71]。
        Assert.Equal(new[] { 1f, 2f, 3f, 4f }, buffer[4..8]);
        Assert.Equal(new[] { 0.5f, 0.6f, 0.7f, 2f }, buffer[68..72]);

        // 第二盏紧跟其后。
        Assert.Equal(new[] { -1f, -2f, -3f, 8f }, buffer[8..12]);
        Assert.Equal(new[] { 0.1f, 0.2f, 0.3f, 1.5f }, buffer[72..76]);
    }

    [Fact]
    public void PackTruncatesBeyondMaxCount()
    {
        var lights = new PointLight3D[PointLight3D.MaxCount + 5];
        for (var i = 0; i < lights.Length; i++)
            lights[i] = new PointLight3D(new Vector3(i, 0f, 0f), 1f, Vector3.One);
        var buffer = new float[PointLight3D.PackedFloatCount];

        var count = PointLight3D.Pack(lights, buffer);

        Assert.Equal(PointLight3D.MaxCount, count);
        Assert.Equal((float)PointLight3D.MaxCount, buffer[0]);
        // 最后一盏可见的是第 16 盏（下标 15），第 17 盏（x=16）不得出现。
        var lastPositionOffset = 4 + (PointLight3D.MaxCount - 1) * 4;
        Assert.Equal(PointLight3D.MaxCount - 1, buffer[lastPositionOffset]);
    }

    [Fact]
    public void PackRejectsTooSmallDestination()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PointLight3D.Pack([], new float[PointLight3D.PackedFloatCount - 1]));
    }
}
