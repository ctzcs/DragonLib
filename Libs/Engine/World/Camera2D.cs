using System.Numerics;
using Foster.Framework;

namespace Engine.World;

public class Camera2D
{
    public Vector2 Position;
    public float Zoom = 1f;
    public float Rotation = 0f;
    public float PPU = 32f;  // 1 单位 = 32 像素

    public Matrix3x2 GetMatrix(IDrawableTarget target)
    {
        return
            Matrix3x2.CreateTranslation(-Position) *
            Matrix3x2.CreateRotation(Rotation) *
            Matrix3x2.CreateScale(PPU * Zoom) *
            Matrix3x2.CreateTranslation(target.WidthInPixels / 2f, target.HeightInPixels / 2f);
    }
}