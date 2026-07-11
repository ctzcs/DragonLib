using System.Numerics;
using Foster.Framework;

namespace Engine.World;
/// <summary>
/// 该相机空间和屏幕空间一致，y向下
/// </summary>
public class Camera2D
{
    public Vector2 Position;
    public float Zoom = 1f;
    public float Rotation = 0f;
    public float PPU = 32f;  // 1 单位 = 32 像素
    public Point2 Viewport; // 通常是Targetable的宽高

    public Matrix3x2 Matrix =>
        //世界空间到相机空间 World->View
        Matrix3x2.CreateTranslation(-Position) *
        Matrix3x2.CreateRotation(Rotation) *
        Matrix3x2.CreateScale(PPU * Zoom) *
        //相机空间到屏幕空间 View->Screen
        Matrix3x2.CreateTranslation(Viewport.X / 2f, Viewport.Y / 2f);
}