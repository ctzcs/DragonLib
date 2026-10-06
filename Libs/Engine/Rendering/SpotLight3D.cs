using System.Numerics;

namespace Engine.Rendering;

/// <summary>无阴影聚光灯；Direction 指向光照射出去的方向，锥角为弧度半角。</summary>
public struct SpotLight3D
{
    public const int MaxCount = 16;
    public const int PackedFloatCount = 4 + MaxCount * 16;
    public Vector3 Position;
    public Vector3 Direction;
    public float Range;
    public Vector3 Color;
    public float Intensity;
    public float InnerAngle;
    public float OuterAngle;

    public SpotLight3D(Vector3 position, Vector3 direction, float range, Vector3 color,
        float intensity = 1, float innerAngle = MathF.PI / 12, float outerAngle = MathF.PI / 6)
    {
        Position = position; Direction = direction; Range = range; Color = color;
        Intensity = intensity; InnerAngle = innerAngle; OuterAngle = outerAngle;
    }

    /// <summary>meta + position/range[16] + color/intensity[16] + direction/cosOuter[16] + cosInner[16]。</summary>
    public static int Pack(ReadOnlySpan<SpotLight3D> lights, Span<float> destination)
    {
        if (destination.Length < PackedFloatCount) throw new ArgumentOutOfRangeException(nameof(destination));
        destination.Clear();
        var count = Math.Min(lights.Length, MaxCount);
        destination[0] = count;
        for (var i = 0; i < count; i++)
        {
            var light = lights[i];
            var direction = light.Direction.LengthSquared() > 1e-8f ? Vector3.Normalize(light.Direction) : -Vector3.UnitY;
            var outer = Math.Clamp(light.OuterAngle, .001f, MathF.PI / 2);
            var inner = Math.Clamp(light.InnerAngle, 0, outer);
            Write(destination, 4 + i * 4, new Vector4(light.Position, MathF.Max(0, light.Range)));
            Write(destination, 4 + MaxCount * 4 + i * 4, new Vector4(Vector3.Max(Vector3.Zero, light.Color), MathF.Max(0, light.Intensity)));
            Write(destination, 4 + MaxCount * 8 + i * 4, new Vector4(direction, MathF.Cos(outer)));
            destination[4 + MaxCount * 12 + i * 4] = MathF.Cos(inner);
        }
        return count;
    }

    private static void Write(Span<float> destination, int offset, Vector4 value)
    {
        destination[offset] = value.X; destination[offset + 1] = value.Y;
        destination[offset + 2] = value.Z; destination[offset + 3] = value.W;
    }
}
