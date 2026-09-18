using System.Numerics;

namespace Engine.Rendering;

/// <summary>
/// 点光（本期不投影阴影）。Range 之外亮度为 0（shader 里 smooth window 衰减），
/// Color/Intensity 相乘即为最终亮度——光照是美术参数，不做物理单位归一。
/// </summary>
public struct PointLight3D
{
    /// <summary>shader cbuffer 的点光容量上限（与 Standard3D/LightSandbox 的 MAX_POINT_LIGHTS 对齐）。</summary>
    public const int MaxCount = 16;

    /// <summary>打包后的 float 数：float4 count + float4 position/range[16] + float4 color/intensity[16]。</summary>
    public const int PackedFloatCount = 4 + MaxCount * 4 + MaxCount * 4;

    public Vector3 Position;
    public float Range;
    public Vector3 Color;
    public float Intensity;

    public PointLight3D(Vector3 position, float range, Vector3 color, float intensity = 1f)
    {
        Position = position;
        Range = range;
        Color = color;
        Intensity = intensity;
    }

    /// <summary>
    /// 打包成 shader cbuffer 布局（Standard3D b3 / LightSandbox b2 共用）：
    /// [0..3] count，[4 + i*4] position.xyz + range，[68 + i*4] color.rgb + intensity。
    /// 超出 <see cref="MaxCount"/> 的截断；destination 长度须 ≥ <see cref="PackedFloatCount"/>，整体先清零再写。
    /// 返回实际写入的点光数。
    /// </summary>
    public static int Pack(ReadOnlySpan<PointLight3D> lights, Span<float> destination)
    {
        if (destination.Length < PackedFloatCount)
            throw new ArgumentOutOfRangeException(nameof(destination));

        destination.Clear();
        var count = Math.Min(lights.Length, MaxCount);
        destination[0] = count;
        for (var i = 0; i < count; i++)
        {
            var light = lights[i];
            var positionOffset = 4 + i * 4;
            destination[positionOffset] = light.Position.X;
            destination[positionOffset + 1] = light.Position.Y;
            destination[positionOffset + 2] = light.Position.Z;
            destination[positionOffset + 3] = light.Range;

            var colorOffset = 4 + MaxCount * 4 + i * 4;
            destination[colorOffset] = light.Color.X;
            destination[colorOffset + 1] = light.Color.Y;
            destination[colorOffset + 2] = light.Color.Z;
            destination[colorOffset + 3] = light.Intensity;
        }
        return count;
    }
}
