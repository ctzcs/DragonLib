using System.Numerics;
using System.Runtime.InteropServices;
using DCFApixels.DragonECS;
using Engine.ECS;
using Engine.Rendering;
using Engine.World;
using Foster.Framework;
using ImGuiNET;

namespace Game0.Content.Demos;

/// <summary>
/// A small lighting laboratory: a room of simple meshes makes directional,
/// ambient, and point-light changes easy to compare while orbiting the camera.
/// </summary>
public sealed class LightSandboxDemoSystem : IEcsInit, IEcsDestroy, IUpdateSystem, IRenderSystem
{
    private const string ShaderResourceBase = "Game0/Shaders/LightSandbox";

    [DI] private SceneRouter<RuntimeScene> _sceneRouter = null!;
    [DI] private Camera3D _camera = null!;
    [DI] private Batcher _batcher = null!;
    [DI] private Input _input = null!;
    [DI] private MyGame _game = null!;

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct LightUniforms
    {
        public Vector4 LightDirection;
        public Vector4 Ambient;
        public Vector4 Diffuse;
        public Vector4 PointLightPosition;
        public Vector4 PointLightColor;
        public Vector4 PointLightParams;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct MaterialUniforms
    {
        public Vector4 Albedo;
        public Vector4 Emissive;
    }

    private EmbeddedShaderMaterial? _shader;
    private EmbeddedShaderMaterial? _glowShader;
    private Mesh3D? _floor;
    private Mesh3D? _wall;
    private Mesh3D? _orangeCube;
    private Mesh3D? _goldCube;
    private Mesh3D? _tealSphere;
    private Mesh3D? _purpleSphere;
    private Mesh3D? _glowSphere;
    private RenderTarget3D? _renderTarget;
    private Renderer3D? _renderer;
    private bool _wasActive;
    private float _elapsed;
    private float _yaw = 0.72f;
    private float _pitch = 0.38f;
    private float _distance = 17f;
    private Vector3 _target = new(0f, 0.5f, 0f);
    private Vector3 _pointLightPosition = new(-2.6f, 3.6f, 1.2f);
    private Vector3 _ambientColor = new(0.18f, 0.22f, 0.28f);
    private Vector3 _directionalColor = new(1.0f, 0.86f, 0.67f);
    private Vector3 _pointLightColor = new(1.0f, 0.34f, 0.12f);
    private float _ambientIntensity = 0.8f;
    private float _directionalIntensity = 1.1f;
    private float _pointLightIntensity = 3.2f;
    private float _pointLightRadius = 8f;
    private bool _pointLightEnabled = true;
    private bool _animatePointLight = true;
    private bool _autoOrbit;

    public void Init()
    {
        _shader = EmbeddedShaderMaterial.Load(
            _game.GraphicsDevice,
            typeof(LightSandboxDemoSystem).Assembly,
            ShaderResourceBase,
            new ShaderStageSpec(0, 2, "fragment_main"),
            new ShaderStageSpec(0, 1, "vertex_main"));
        _glowShader = EmbeddedShaderMaterial.Load(
            _game.GraphicsDevice,
            typeof(LightSandboxDemoSystem).Assembly,
            ShaderResourceBase,
            new ShaderStageSpec(0, 2, "fragment_main"),
            new ShaderStageSpec(0, 1, "vertex_main"));
        _floor = Mesh3D.CreateCube(_game.GraphicsDevice, 1f, new Color(0x66727B), "Lighting Sandbox Floor");
        _wall = Mesh3D.CreateCube(_game.GraphicsDevice, 1f, new Color(0x3D4C5A), "Lighting Sandbox Wall");
        _orangeCube = Mesh3D.CreateCube(_game.GraphicsDevice, 1f, new Color(0xCC704D), "Lighting Sandbox Orange");
        _goldCube = Mesh3D.CreateCube(_game.GraphicsDevice, 1f, new Color(0xD8B15B), "Lighting Sandbox Gold");
        _tealSphere = Mesh3D.CreateIcosphere(_game.GraphicsDevice, 1f, 2, 0.04f, new Color(0x78B8B0), 17, "Lighting Sandbox Teal");
        _purpleSphere = Mesh3D.CreateIcosphere(_game.GraphicsDevice, 1f, 2, 0.04f, new Color(0xA28CC8), 23, "Lighting Sandbox Purple");
        _glowSphere = Mesh3D.CreateIcosphere(_game.GraphicsDevice, 1f, 2, 0f, Color.White, 29, "Lighting Sandbox Glow");
        _renderTarget = new RenderTarget3D(_game.GraphicsDevice);
        _renderer = new Renderer3D(_game.GraphicsDevice);
    }

    public void Destroy()
    {
        _floor?.Dispose();
        _wall?.Dispose();
        _orangeCube?.Dispose();
        _goldCube?.Dispose();
        _tealSphere?.Dispose();
        _purpleSphere?.Dispose();
        _glowSphere?.Dispose();
        _renderTarget?.Dispose();
        _renderer?.Dispose();
        _shader?.Dispose();
        _glowShader?.Dispose();
        _floor = null;
        _wall = null;
        _orangeCube = null;
        _goldCube = null;
        _tealSphere = null;
        _purpleSphere = null;
        _glowSphere = null;
        _renderTarget = null;
        _renderer = null;
        _shader = null;
        _glowShader = null;
    }

    public void Update()
    {
        var active = _sceneRouter.Current == RuntimeScene.LightSandbox;
        if (active && !_wasActive)
            ResetScene();

        _wasActive = active;
        if (!active)
            return;

        var delta = MathF.Min(_game.Time.Delta, 1f / 15f);
        _elapsed += delta;
        var io = ImGui.GetIO();
        if (!io.WantCaptureMouse)
        {
            if (_input.Mouse.RightDown)
            {
                _yaw -= _input.Mouse.Delta.X * 0.008f;
                _pitch = Math.Clamp(_pitch - _input.Mouse.Delta.Y * 0.008f, -0.1f, 1.2f);
            }

            _distance = Math.Clamp(_distance - _input.Mouse.Wheel.Y * 1.1f, 8f, 30f);
        }

        if (_autoOrbit)
            _yaw += delta * 0.22f;

        if (_animatePointLight)
        {
            _pointLightPosition.X = MathF.Sin(_elapsed * 0.8f) * 3.3f;
            _pointLightPosition.Z = MathF.Cos(_elapsed * 0.65f) * 2.3f;
            _pointLightPosition.Y = 3.2f + MathF.Sin(_elapsed * 1.25f) * 0.65f;
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
        if (!_wasActive || _floor == null || _wall == null || _orangeCube == null || _goldCube == null ||
            _tealSphere == null || _purpleSphere == null || _glowSphere == null || _renderTarget == null ||
            _renderer == null || _shader == null || _glowShader == null)
            return;

        var width = _game.Window.WidthInPixels;
        var height = _game.Window.HeightInPixels;
        _renderTarget.Resize(width, height);
        _renderTarget.Clear(new Color(0x101720));
        _camera.ViewportSize = new Point2(width, height);

        var lightDirection = new Vector3(
            MathF.Cos(_directionalElevation) * MathF.Cos(_directionalAzimuth),
            -MathF.Sin(_directionalElevation),
            MathF.Cos(_directionalElevation) * MathF.Sin(_directionalAzimuth));
        var uniforms = new LightUniforms
        {
            LightDirection = new Vector4(lightDirection, 0f),
            Ambient = new Vector4(_ambientColor * _ambientIntensity, 1f),
            Diffuse = new Vector4(_directionalColor * _directionalIntensity, 1f),
            PointLightPosition = new Vector4(_pointLightPosition, 1f),
            PointLightColor = new Vector4(_pointLightColor, 1f),
            PointLightParams = new Vector4(_pointLightEnabled ? 1f : 0f, _pointLightIntensity, _pointLightRadius, 0f),
        };
        _shader.Material.Fragment.SetUniformBuffer(uniforms);
        _glowShader.Material.Fragment.SetUniformBuffer(uniforms);
        _shader.Material.Fragment.SetUniformBuffer(new MaterialUniforms
        {
            Albedo = Vector4.One,
            Emissive = Vector4.Zero,
        }, 1);
        _glowShader.Material.Fragment.SetUniformBuffer(new MaterialUniforms
        {
            Albedo = Vector4.One,
            Emissive = new Vector4(_pointLightColor, 1.5f),
        }, 1);

        _renderer.Begin(_renderTarget.Target, _camera);
        Draw(_floor, Matrix4x4.CreateScale(14f, 0.35f, 10f) * Matrix4x4.CreateTranslation(0f, -2.15f, 0f));
        Draw(_wall, Matrix4x4.CreateScale(14f, 6f, 0.3f) * Matrix4x4.CreateTranslation(0f, 0.8f, -5f));
        Draw(_wall, Matrix4x4.CreateScale(0.3f, 6f, 10f) * Matrix4x4.CreateTranslation(-7f, 0.8f, 0f));
        Draw(_orangeCube, Matrix4x4.CreateScale(1.8f, 2.2f, 1.8f) * Matrix4x4.CreateTranslation(-3.7f, -0.75f, -0.9f));
        Draw(_goldCube, Matrix4x4.CreateScale(1.4f, 1.4f, 1.4f) * Matrix4x4.CreateTranslation(0.2f, -1.45f, 1.2f));
        Draw(_tealSphere, Matrix4x4.CreateScale(1.45f) * Matrix4x4.CreateTranslation(3.2f, -0.65f, -0.15f));
        Draw(_purpleSphere, Matrix4x4.CreateScale(0.95f) * Matrix4x4.CreateTranslation(1.2f, -1.2f, -2.3f));
        if (_pointLightEnabled)
            _renderer.Draw(_glowSphere, _glowShader.Material, Matrix4x4.CreateScale(0.38f) * Matrix4x4.CreateTranslation(_pointLightPosition));
        _renderer.End();
        _renderTarget.Composite(_batcher, width, height);
    }

    private float _directionalAzimuth = -2.25f;
    private float _directionalElevation = 0.62f;

    private void Draw(Mesh3D mesh, in Matrix4x4 world)
    {
        _renderer!.Draw(mesh, _shader.Material, world);
    }

    private void DrawControls()
    {
        ImGui.SetNextWindowPos(new Vector2(16f, 136f), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(390f, 0f), ImGuiCond.FirstUseEver);
        ImGui.Begin("Lighting Sandbox");
        ImGui.TextUnformatted("Realtime room lighting lab");
        ImGui.TextUnformatted("RMB drag: orbit   Wheel: zoom");
        ImGui.Separator();

        if (ImGui.Button("Warm studio"))
            ApplyWarmPreset();
        ImGui.SameLine();
        if (ImGui.Button("Cool moon"))
            ApplyCoolPreset();
        ImGui.SameLine();
        if (ImGui.Button("Reset"))
            ResetScene();

        ImGui.Separator();
        ImGui.TextUnformatted("Directional light");
        ImGui.SliderAngle("Azimuth", ref _directionalAzimuth, -180f, 180f);
        ImGui.SliderAngle("Elevation", ref _directionalElevation, 5f, 85f);
        ImGui.ColorEdit3("Color", ref _directionalColor);
        ImGui.SliderFloat("Intensity", ref _directionalIntensity, 0f, 3f, "%.2f");

        ImGui.Separator();
        ImGui.TextUnformatted("World fill");
        ImGui.ColorEdit3("Ambient", ref _ambientColor);
        ImGui.SliderFloat("Ambient intensity", ref _ambientIntensity, 0f, 2f, "%.2f");

        ImGui.Separator();
        ImGui.TextUnformatted("Point light");
        ImGui.Checkbox("Enabled", ref _pointLightEnabled);
        ImGui.Checkbox("Animate position", ref _animatePointLight);
        ImGui.ColorEdit3("Point color", ref _pointLightColor);
        ImGui.SliderFloat("Point intensity", ref _pointLightIntensity, 0f, 8f, "%.2f");
        ImGui.SliderFloat("Radius", ref _pointLightRadius, 2f, 16f, "%.1f");
        ImGui.Text($"Position: {_pointLightPosition.X:F1}, {_pointLightPosition.Y:F1}, {_pointLightPosition.Z:F1}");

        ImGui.Separator();
        ImGui.Checkbox("Auto orbit camera", ref _autoOrbit);
        ImGui.Text($"Render FPS: {_game.RenderFramesPerSecond:F1}");
        ImGui.Text("Objects: 8   Draw calls: 8");
        ImGui.End();
    }

    private void ResetScene()
    {
        _yaw = 0.72f;
        _pitch = 0.38f;
        _distance = 17f;
        _target = new Vector3(0f, 0.5f, 0f);
        _directionalAzimuth = -2.25f;
        _directionalElevation = 0.62f;
        _ambientColor = new Vector3(0.18f, 0.22f, 0.28f);
        _directionalColor = new Vector3(1.0f, 0.86f, 0.67f);
        _pointLightColor = new Vector3(1.0f, 0.34f, 0.12f);
        _ambientIntensity = 0.8f;
        _directionalIntensity = 1.1f;
        _pointLightIntensity = 3.2f;
        _pointLightRadius = 8f;
        _pointLightEnabled = true;
        _animatePointLight = true;
        _autoOrbit = false;
        _pointLightPosition = new Vector3(-2.6f, 3.6f, 1.2f);
    }

    private void ApplyWarmPreset()
    {
        _ambientColor = new Vector3(0.24f, 0.15f, 0.1f);
        _directionalColor = new Vector3(1f, 0.48f, 0.22f);
        _pointLightColor = new Vector3(1f, 0.72f, 0.28f);
        _ambientIntensity = 0.55f;
        _directionalIntensity = 1.2f;
        _pointLightIntensity = 4.8f;
    }

    private void ApplyCoolPreset()
    {
        _ambientColor = new Vector3(0.08f, 0.15f, 0.28f);
        _directionalColor = new Vector3(0.42f, 0.64f, 1f);
        _pointLightColor = new Vector3(0.26f, 0.62f, 1f);
        _ambientIntensity = 0.9f;
        _directionalIntensity = 0.7f;
        _pointLightIntensity = 3.6f;
    }
}
