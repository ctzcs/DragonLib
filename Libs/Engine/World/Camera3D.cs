using System.Numerics;
using Foster.Framework;

namespace Engine.World;

/// <summary>
/// A small perspective camera for direct 3D rendering.
/// Matrices use the same convention as Foster's built-in shaders.
/// </summary>
public sealed class Camera3D : ICamera
{
    private Vector3 _position = new(0f, 2f, 8f);
    private Vector3 _target;
    private Vector3 _up = Vector3.UnitY;
    private float _fieldOfView = MathF.PI / 3f;
    private float _nearClip = 0.05f;
    private float _farClip = 250f;
    private Point2 _viewportSize;
    private bool _dirty = true;
    private Matrix4x4 _view = Matrix4x4.Identity;
    private Matrix4x4 _projection = Matrix4x4.Identity;
    private Matrix4x4 _viewProjection = Matrix4x4.Identity;

    public Vector3 Position
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

    public Vector3 Target
    {
        get => _target;
        set
        {
            if (_target == value)
                return;

            _target = value;
            _dirty = true;
        }
    }

    public Vector3 Up
    {
        get => _up;
        set
        {
            if (_up == value)
                return;

            _up = value;
            _dirty = true;
        }
    }

    public float FieldOfView
    {
        get => _fieldOfView;
        set
        {
            if (_fieldOfView == value)
                return;

            _fieldOfView = value;
            _dirty = true;
        }
    }

    public float NearClip
    {
        get => _nearClip;
        set
        {
            if (_nearClip == value)
                return;

            _nearClip = value;
            _dirty = true;
        }
    }

    public float FarClip
    {
        get => _farClip;
        set
        {
            if (_farClip == value)
                return;

            _farClip = value;
            _dirty = true;
        }
    }

    public float AspectRatio { get; private set; } = 16f / 9f;

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

    public Matrix4x4 View
    {
        get
        {
            EnsureUpdated();
            return _view;
        }
    }

    public Matrix4x4 Projection
    {
        get
        {
            EnsureUpdated();
            return _projection;
        }
    }

    public Matrix4x4 ViewProjection
    {
        get
        {
            EnsureUpdated();
            return _viewProjection;
        }
    }

    public Vector3 Forward
    {
        get
        {
            var direction = Target - Position;
            return direction.LengthSquared() > 1e-8f
                ? Vector3.Normalize(direction)
                : -Vector3.UnitZ;
        }
    }

    public Vector3 Right => Vector3.Normalize(Vector3.Cross(Forward, Up));

    public void Update()
    {
        if (_viewportSize.X <= 0 || _viewportSize.Y <= 0)
            return;

        if (!_dirty)
            return;

        AspectRatio = _viewportSize.X / (float)_viewportSize.Y;
        var fov = Math.Clamp(_fieldOfView, 0.1f, MathF.PI - 0.1f);
        var nearClip = MathF.Max(0.001f, _nearClip);
        var farClip = MathF.Max(nearClip + 0.001f, _farClip);

        _view = Matrix4x4.CreateLookAt(_position, _target, _up);
        _projection = Matrix4x4.CreatePerspectiveFieldOfView(fov, AspectRatio, nearClip, farClip);
        _viewProjection = _view * _projection;
        _dirty = false;
    }

    private void EnsureUpdated()
    {
        Update();
    }
}
