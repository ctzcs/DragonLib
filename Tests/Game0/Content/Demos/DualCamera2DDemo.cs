using System.Numerics;
using DCFApixels.DragonECS;
using Engine.ECS;
using Engine.World;
using Foster.Framework;
using ImGuiNET;

namespace Game0.Content.Demos;

/// <summary>
/// Renders one 2D world through two independent cameras. The left view follows
/// the player while the right view keeps the whole arena visible.
/// </summary>
public sealed class DualCamera2DDemoSystem : IEcsInit, IEcsDestroy, IUpdateSystem, IRenderSystem
{
    private const float ArenaHalfWidth = 12f;
    private const float ArenaHalfHeight = 7f;

    [DI] private SceneRouter<RuntimeScene> _sceneRouter = null!;
    [DI] private Batcher _overlayBatcher = null!;
    [DI] private Input _input = null!;
    [DI] private MyGame _game = null!;

    private readonly Camera2D _followCamera = new();
    private readonly Camera2D _overviewCamera = new();
    private Batcher? _followBatcher;
    private Batcher? _overviewBatcher;
    private Vector2 _playerPosition;
    private Vector2 _playerFacing = Vector2.UnitX;
    private float _followZoom = 1f;
    private float _overviewZoom = 1f;
    private float _elapsed;
    private bool _rotateOverview;
    private bool _wasActive;

    public void Init()
    {
        _followBatcher = new Batcher(_game.GraphicsDevice);
        _overviewBatcher = new Batcher(_game.GraphicsDevice);
        ResetDemo();
    }

    public void Destroy()
    {
        _followBatcher?.Dispose();
        _overviewBatcher?.Dispose();
        _followBatcher = null;
        _overviewBatcher = null;
    }

    public void Update()
    {
        var active = _sceneRouter.Current == RuntimeScene.DualCamera2D;
        if (active && !_wasActive)
            ResetDemo();

        _wasActive = active;
        if (!active)
            return;

        var delta = MathF.Min(_game.Time.Delta, 1f / 15f);
        _elapsed += delta;

        var io = ImGui.GetIO();
        if (!io.WantCaptureKeyboard)
        {
            var movement = Vector2.Zero;
            if (_input.Keyboard.Down(Keys.A)) movement.X -= 1f;
            if (_input.Keyboard.Down(Keys.D)) movement.X += 1f;
            if (_input.Keyboard.Down(Keys.W)) movement.Y -= 1f;
            if (_input.Keyboard.Down(Keys.S)) movement.Y += 1f;

            if (movement.LengthSquared() > 0f)
            {
                movement = Vector2.Normalize(movement);
                _playerFacing = movement;
                _playerPosition += movement * (6f * delta);
                _playerPosition.X = Math.Clamp(_playerPosition.X, -ArenaHalfWidth + 0.6f, ArenaHalfWidth - 0.6f);
                _playerPosition.Y = Math.Clamp(_playerPosition.Y, -ArenaHalfHeight + 0.6f, ArenaHalfHeight - 0.6f);
            }
        }

        if (!io.WantCaptureMouse && _input.Mouse.Wheel.Y != 0f)
            _followZoom = Math.Clamp(_followZoom + _input.Mouse.Wheel.Y * 0.1f, 0.6f, 2.2f);

        var followAmount = 1f - MathF.Exp(-7f * delta);
        _followCamera.Position = Vector2.Lerp(_followCamera.Position, _playerPosition, followAmount);
        _followCamera.Zoom = _followZoom;
        _overviewCamera.Zoom = _overviewZoom;
        _overviewCamera.Rotation = _rotateOverview ? MathF.Sin(_elapsed * 0.35f) * 0.08f : 0f;

        DrawControls();
    }

    public void Render()
    {
        if (!_wasActive || _followBatcher == null || _overviewBatcher == null)
            return;

        var width = _game.Window.WidthInPixels;
        var height = _game.Window.HeightInPixels;
        var leftWidth = width / 2;
        var rightWidth = width - leftWidth;
        if (leftWidth <= 0 || rightWidth <= 0 || height <= 0)
            return;

        var leftViewport = new RectInt(0, 0, leftWidth, height);
        var rightViewport = new RectInt(leftWidth, 0, rightWidth, height);

        ConfigureCameras(leftViewport, rightViewport);
        RenderView(_followBatcher, _followCamera, leftViewport, new Color(0x101820));
        RenderView(_overviewBatcher, _overviewCamera, rightViewport, new Color(0x182016));
        DrawViewportOverlay(width, height, leftWidth);
    }

    private void ConfigureCameras(in RectInt leftViewport, in RectInt rightViewport)
    {
        _followCamera.ViewportSize = leftViewport.Size;
        _followCamera.PPU = 78f;

        _overviewCamera.ViewportSize = rightViewport.Size;
        _overviewCamera.Position = Vector2.Zero;
        _overviewCamera.PPU = MathF.Max(1f, MathF.Min(
            rightViewport.Width / (ArenaHalfWidth * 2f + 3f),
            rightViewport.Height / (ArenaHalfHeight * 2f + 3f)));
    }

    private void RenderView(Batcher batcher, Camera2D camera, in RectInt viewport, Color background)
    {
        batcher.Rect(0f, 0f, viewport.Width, viewport.Height, background);
        batcher.PushMatrix(camera.Matrix);
        try
        {
            DrawWorld(batcher);
        }
        finally
        {
            batcher.PopMatrix();
        }

        batcher.Render(_game.Window, viewport, viewport);
        batcher.Clear();
    }

    private void DrawWorld(Batcher batcher)
    {
        batcher.Rect(
            -ArenaHalfWidth,
            -ArenaHalfHeight,
            ArenaHalfWidth * 2f,
            ArenaHalfHeight * 2f,
            new Color(0x17242B));
        DrawGrid(batcher);

        batcher.RectLine(
            new Rect(-ArenaHalfWidth, -ArenaHalfHeight, ArenaHalfWidth * 2f, ArenaHalfHeight * 2f),
            0.12f,
            new Color(0xD8E0D5));

        DrawLandmark(batcher, new Vector2(-8f, -3.5f), new Vector2(2.8f, 1.8f), new Color(0xD96C4B));
        DrawLandmark(batcher, new Vector2(6.8f, -3.8f), new Vector2(2.2f, 2.2f), new Color(0xE3B341));
        DrawLandmark(batcher, new Vector2(-7.2f, 4.2f), new Vector2(2.4f, 1.6f), new Color(0x4AA6A1));
        DrawLandmark(batcher, new Vector2(7.4f, 3.8f), new Vector2(3f, 1.4f), new Color(0x7289DA));

        for (var index = 0; index < 5; index++)
        {
            var phase = _elapsed * (0.45f + index * 0.06f) + index * 1.25f;
            var position = new Vector2(
                MathF.Cos(phase) * (4.2f + index * 0.75f),
                MathF.Sin(phase * 1.3f) * (2.4f + index * 0.35f));
            var color = index % 2 == 0 ? new Color(0xF08A5D) : new Color(0x56B4C2);
            batcher.Circle(position, 0.3f, 18, color);
            batcher.CircleLine(position, 0.3f, 0.06f, 18, Color.White);
        }

        batcher.Circle(_playerPosition, 0.48f, 24, new Color(0xF4D35E));
        batcher.CircleLine(_playerPosition, 0.48f, 0.08f, 24, Color.White);
        var side = new Vector2(-_playerFacing.Y, _playerFacing.X) * 0.18f;
        batcher.Triangle(
            _playerPosition + _playerFacing * 0.72f,
            _playerPosition - _playerFacing * 0.05f + side,
            _playerPosition - _playerFacing * 0.05f - side,
            new Color(0xF7F4EA));
    }

    private static void DrawGrid(Batcher batcher)
    {
        var minor = new Color(0x263841);
        var major = new Color(0x3E5962);
        for (var x = -(int)ArenaHalfWidth; x <= ArenaHalfWidth; x++)
            batcher.Line(new Vector2(x, -ArenaHalfHeight), new Vector2(x, ArenaHalfHeight), 0.025f, x == 0 ? major : minor);
        for (var y = -(int)ArenaHalfHeight; y <= ArenaHalfHeight; y++)
            batcher.Line(new Vector2(-ArenaHalfWidth, y), new Vector2(ArenaHalfWidth, y), 0.025f, y == 0 ? major : minor);
    }

    private static void DrawLandmark(Batcher batcher, Vector2 center, Vector2 size, Color color)
    {
        var rect = new Rect(center.X - size.X * 0.5f, center.Y - size.Y * 0.5f, size.X, size.Y);
        batcher.Rect(rect, color);
        batcher.RectLine(rect, 0.08f, Color.White);
    }

    private void DrawViewportOverlay(int width, int height, int splitX)
    {
        _overlayBatcher.PushMatrix(Matrix3x2.Identity, relative: false);
        try
        {
            _overlayBatcher.Rect(splitX - 2f, 0f, 4f, height, new Color(0xF2F0E8));
            _overlayBatcher.Rect(0f, 0f, width / 2f, 5f, new Color(0xF4D35E));
            _overlayBatcher.Rect(width / 2f, 0f, width - width / 2f, 5f, new Color(0x56B4C2));
        }
        finally
        {
            _overlayBatcher.PopMatrix();
        }
    }

    private void DrawControls()
    {
        ImGui.SetNextWindowPos(new Vector2(16f, 136f), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(330f, 0f), ImGuiCond.FirstUseEver);
        ImGui.Begin("Dual Camera 2D");
        ImGui.TextUnformatted("Left: follow camera   Right: overview camera");
        ImGui.TextUnformatted("WASD: move   Wheel: follow zoom");
        ImGui.Separator();
        ImGui.SliderFloat("Follow zoom", ref _followZoom, 0.6f, 2.2f);
        ImGui.SliderFloat("Overview zoom", ref _overviewZoom, 0.7f, 1.35f);
        ImGui.Checkbox("Animate overview rotation", ref _rotateOverview);
        ImGui.Text($"Player: {_playerPosition.X:F1}, {_playerPosition.Y:F1}");
        if (ImGui.Button("Reset cameras"))
            ResetDemo();
        ImGui.End();
    }

    private void ResetDemo()
    {
        _playerPosition = Vector2.Zero;
        _playerFacing = Vector2.UnitX;
        _followCamera.Position = Vector2.Zero;
        _followCamera.Rotation = 0f;
        _followZoom = 1f;
        _overviewZoom = 1f;
        _rotateOverview = false;
        _elapsed = 0f;
    }
}
