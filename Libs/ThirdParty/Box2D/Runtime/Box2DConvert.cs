using System.Numerics;
using global::Box2D.NET;
using static global::Box2D.NET.B2MathFunction;

namespace DragonLib.Box2D;

/// <summary>Converts between engine numerics types and Box2D value types.</summary>
public static class Box2DConvert
{
    public static B2Vec2 ToBox2D(this Vector2 value)
        => new(value.X, value.Y);

    public static Vector2 ToVector2(this B2Vec2 value)
        => new(value.X, value.Y);

    public static B2Rot ToBox2DRotation(this float radians)
        => b2MakeRot(radians);

    public static float ToRadians(this B2Rot rotation)
        => b2Rot_GetAngle(rotation);
}
