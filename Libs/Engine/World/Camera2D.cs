using System.Numerics;
using Foster.Framework;

namespace Engine.World;
/// <summary>
/// 该相机空间和屏幕空间一致，y向下
/// </summary>
public class Camera2D : ICamera
{
    private Vector2 _position;
    private float _zoom = 1f;
    private float _rotation;
    private float _ppu = 32f;
    private Point2 _viewportSize;
    private Matrix3x2 _matrix = Matrix3x2.Identity;
    private Matrix3x2 _inverseMatrix = Matrix3x2.Identity;
    private bool _dirty = true;
    private bool _inverseValid = true;

    public Vector2 Position
    {
        get => _position;
        set
        {
            if (_position == value)
                return;

            _position = value;
            _dirty = true;
        }
    }

    public float Zoom
    {
        get => _zoom;
        set
        {
            if (_zoom == value)
                return;

            _zoom = value;
            _dirty = true;
        }
    }

    public float Rotation
    {
        get => _rotation;
        set
        {
            if (_rotation == value)
                return;

            _rotation = value;
            _dirty = true;
        }
    }

    public float PPU
    {
        get => _ppu;
        set
        {
            if (_ppu == value)
                return;

            _ppu = value;
            _dirty = true;
        }
    }

    public Point2 ViewportSize
    {
        get => _viewportSize;
        set
        {
            if (_viewportSize == value)
                return;

            _viewportSize = value;
            _dirty = true;
        }
    }

    public Matrix3x2 Matrix
    {
        get
        {
            Update();
            return _matrix;
        }
    }

    /// <summary>Rebuilds the camera matrices when camera state has changed.</summary>
    public void Update()
    {
        if (!_dirty)
            return;

        // 世界空间到相机空间 World->View
        _matrix = Matrix3x2.CreateTranslation(-_position) *
            Matrix3x2.CreateRotation(_rotation) *
            Matrix3x2.CreateScale(_ppu * _zoom) *
            // 相机空间到屏幕空间 View->Screen
            Matrix3x2.CreateTranslation(_viewportSize.X / 2f, _viewportSize.Y / 2f);

        _inverseValid = Matrix3x2.Invert(_matrix, out _inverseMatrix);
        _dirty = false;
    }

    public Vector2 WorldToScreen(Vector2 worldPosition)
        => Vector2.Transform(worldPosition, Matrix);

    public Vector2 ScreenToWorld(Vector2 screenPosition)
    {
        if (!TryScreenToWorld(screenPosition, out var worldPosition))
            throw new InvalidOperationException("The camera transform is not invertible.");

        return worldPosition;
    }

    public bool TryScreenToWorld(Vector2 screenPosition, out Vector2 worldPosition)
    {
        Update();
        if (!_inverseValid)
        {
            worldPosition = default;
            return false;
        }

        worldPosition = Vector2.Transform(screenPosition, _inverseMatrix);
        return true;
    }
}
