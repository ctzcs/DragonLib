using System.Numerics;
using DCFApixels.DragonECS;
using Engine.ECS;
using Engine.Rendering;
using Engine.World;
using Foster.Framework;
using ImGuiNET;

namespace Game0.Content.Demos;

/// <summary>
/// A small stylized 3D scene inspired by Celeste64's compact, readable geometry.
/// It intentionally uses procedural cubes so the demo has no external asset dependency.
/// </summary>
public sealed class ThreeDDemoSystem : IEcsInit, IEcsDestroy, IUpdateSystem, IRenderSystem
{
    private const string ShaderResourceBase = "Game0/Shaders/Basic3D";

    [DI] private SceneRouter<RuntimeScene> _sceneRouter = null!;
    [DI] private Camera3D _camera = null!;
    [DI] private Batcher _batcher = null!;
    [DI] private Input _input = null!;
    [DI] private MyGame _game = null!;

    private EmbeddedShaderMaterial? _shader;
    private Mesh3D? _platform;
    private Mesh3D? _accent;
    private RenderTarget3D? _renderTarget;
    private bool _wasActive;
    private float _yaw = 0.65f;
    private float _pitch = 0.38f;
    private float _distance = 13f;
    private Vector3 _target = new(0f, 0.7f, 0f);
    private float _elapsed;

    private static readonly (Vector3 Position, Vector3 Scale)[] Platforms =
    [
        (new(-3.8f, 0.1f, -1.2f), new(2.4f, 0.55f, 2.4f)),
        (new(0.0f, 1.0f, -1.0f), new(2.2f, 0.55f, 2.2f)),
        (new(3.6f, 2.0f, -0.6f), new(2.0f, 0.55f, 2.0f)),
        (new(1.5f, 3.0f, 2.1f), new(1.6f, 0.45f, 1.6f)),
        (new(-2.2f, 2.35f, 2.8f), new(1.4f, 0.45f, 1.4f)),
    ];

    public void Init()
    {
        _shader = EmbeddedShaderMaterial.Load(
            _game.GraphicsDevice,
            typeof(ThreeDDemoSystem).Assembly,
            ShaderResourceBase,
            new ShaderStageSpec(0, 1, "fragment_main"),
            new ShaderStageSpec(0, 1, "vertex_main"));

        _platform = Mesh3D.CreateCube(_game.GraphicsDevice, _shader.Material, 1f, new Color(0x4E6B78), "3D Platform");
        _accent = Mesh3D.CreateCube(_game.GraphicsDevice, _shader.Material, 1f, new Color(0xD98645), "3D Accent");
        _renderTarget = new RenderTarget3D(_game.GraphicsDevice);
    }

    public void Destroy()
    {
        _platform?.Dispose();
        _accent?.Dispose();
        _renderTarget?.Dispose();
        _shader?.Dispose();
        _platform = null;
        _accent = null;
        _renderTarget = null;
        _shader = null;
    }

    public void Update()
    {
        var active = _sceneRouter.Current == RuntimeScene.ThreeD;
        if (active && !_wasActive)
        {
            _yaw = 0.65f;
            _pitch = 0.38f;
            _distance = 13f;
            _target = new Vector3(0f, 0.7f, 0f);
            _elapsed = 0f;
        }

        _wasActive = active;
        if (!active)
            return;

        _elapsed += _game.Time.Delta;
        var io = ImGui.GetIO();
        if (!io.WantCaptureMouse)
        {
            if (_input.Mouse.RightDown)
            {
                _yaw -= _input.Mouse.Delta.X * 0.008f;
                _pitch = Math.Clamp(_pitch - _input.Mouse.Delta.Y * 0.008f, -0.05f, 1.2f);
            }

            _distance = Math.Clamp(_distance - _input.Mouse.Wheel.Y * 0.75f, 5f, 28f);
        }

        if (!io.WantCaptureKeyboard)
        {
            var movement = Vector3.Zero;
            if (_input.Keyboard.Down(Keys.A)) movement.X -= 1f;
            if (_input.Keyboard.Down(Keys.D)) movement.X += 1f;
            if (_input.Keyboard.Down(Keys.W)) movement.Z -= 1f;
            if (_input.Keyboard.Down(Keys.S)) movement.Z += 1f;
            if (movement.LengthSquared() > 0f)
            {
                movement = Vector3.Normalize(movement) * (_game.Time.Delta * 4f);
                _target += new Vector3(movement.X, 0f, movement.Z);
            }
        }

        var horizontal = MathF.Cos(_pitch) * _distance;
        _camera.Target = _target;
        _camera.Position = _target + new Vector3(
            MathF.Sin(_yaw) * horizontal,
            MathF.Sin(_pitch) * _distance,
            MathF.Cos(_yaw) * horizontal);

        DrawControls();
    }

    public void Render()
    {
        if (!_wasActive || _platform == null || _accent == null || _renderTarget == null)
            return;

        var width = _game.Window.WidthInPixels;
        var height = _game.Window.HeightInPixels;
        _renderTarget.Resize(width, height);
        _renderTarget.Clear(new Color(0xB9D6D1));
        _camera.Update(width, height);

        var lightDirection = Vector3.Normalize(new Vector3(-0.45f, -1f, -0.35f));
        var ambient = new Vector3(0.28f, 0.31f, 0.34f);
        var diffuse = new Vector3(0.72f, 0.69f, 0.62f);

        _platform.Draw(_renderTarget.Target, _camera,
            Matrix4x4.CreateScale(14f, 0.3f, 14f) * Matrix4x4.CreateTranslation(0f, -1.35f, 0f),
            lightDirection, ambient, diffuse);

        foreach (var (position, scale) in Platforms)
        {
            _platform.Draw(_renderTarget.Target, _camera,
                Matrix4x4.CreateScale(scale) * Matrix4x4.CreateTranslation(position),
                lightDirection, ambient, diffuse);
        }

        var accentRotation = Matrix4x4.CreateRotationY(_elapsed * 0.7f) * Matrix4x4.CreateRotationX(0.3f);
        _accent.Draw(_renderTarget.Target, _camera,
            Matrix4x4.CreateScale(1.15f) * accentRotation * Matrix4x4.CreateTranslation(0f, 2.35f, 0.4f),
            lightDirection, ambient, diffuse);

        _renderTarget.Composite(_batcher, width, height);
    }

    private void DrawControls()
    {
        ImGui.SetNextWindowPos(new Vector2(16f, 136f), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(330f, 0f), ImGuiCond.FirstUseEver);
        ImGui.Begin("3D Demo");
        ImGui.TextUnformatted("Procedural mountain platforms");
        ImGui.TextUnformatted("RMB drag: orbit   Wheel: zoom");
        ImGui.TextUnformatted("WASD: move focus");
        ImGui.Separator();
        ImGui.Text($"Camera distance: {_distance:F1}");
        ImGui.Text($"Platform count: {Platforms.Length + 1}");
        ImGui.End();
    }
}
