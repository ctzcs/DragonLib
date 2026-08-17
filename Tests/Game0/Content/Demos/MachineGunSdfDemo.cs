using System;
using System.Numerics;
using System.Runtime.InteropServices;
using DCFApixels.DragonECS;
using Engine.ECS;
using Engine.Rendering;
using Engine.World;
using Foster.Framework;
using ImGuiNET;

namespace Game0.Content.Demos;

public sealed class MachineGunSdfDemoSystem : IEcsInit, IEcsDestroy, IUpdateSystem, IRenderSystem
{
    private const string ShaderResourceBase = "Game0/Shaders/MachineGunSdf";

    [StructLayout(LayoutKind.Sequential)]
    private struct GunUniforms
    {
        public Vector4 Params;
        public Vector4 Direction;
        public Vector4 MetalColor;
        public Vector4 GripColor;
        public Vector4 AccentColor;
        public Vector4 GlowColor;
        public Vector4 PanelColor;
        public Vector4 BrassColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct VertexUniforms { public Matrix4x4 CameraMatrix; }

    private readonly record struct CameraState(Vector2 Position, float Zoom, float Rotation, float Ppu);

    [DI] private SceneRouter<RuntimeScene> _sceneRouter = null!;
    [DI] private Batcher _batcher = null!;
    [DI] private Camera2D _camera = null!;
    [DI] private Input _input = null!;
    [DI] private MyGame _game = null!;

    private EmbeddedShaderMaterial? _shader;
    private CameraState _savedCamera;
    private bool _wasActive;
    private bool _followMouse = true;
    private float _elapsed;
    private float _angle = MathF.PI;
    private float _scale = 1f;
    private float _fire = 0.7f;
    private Vector3 _metal = new(0.055f, 0.075f, 0.095f);
    private Vector3 _grip = new(0.015f, 0.022f, 0.032f);
    private Vector3 _accent = new(0.88f, 0.07f, 0.06f);
    private Vector3 _glow = new(0.02f, 0.88f, 1.0f);
    private Vector3 _panel = new(0.40f, 0.46f, 0.50f);
    private Vector3 _brass = new(0.88f, 0.57f, 0.14f);

    public void Init() => _shader = EmbeddedShaderMaterial.Load(_game.GraphicsDevice, typeof(MachineGunSdfDemoSystem).Assembly,
        ShaderResourceBase, new ShaderStageSpec(0, 1, "fragment_main"), new ShaderStageSpec(0, 2, "vertex_main"));

    public void Destroy() => _shader?.Dispose();

    public void Update()
    {
        var active = _sceneRouter.Current == RuntimeScene.MachineGunSdfShader;
        if (active && !_wasActive)
        {
            _savedCamera = new(_camera.Position, _camera.Zoom, _camera.Rotation, _camera.PPU);
            _camera.Position = Vector2.Zero; _camera.Zoom = 1f; _camera.Rotation = 0f; _camera.PPU = 54f;
            _elapsed = 0f;
        }
        else if (!active && _wasActive)
        {
            _camera.Position = _savedCamera.Position; _camera.Zoom = _savedCamera.Zoom; _camera.Rotation = _savedCamera.Rotation; _camera.PPU = _savedCamera.Ppu;
        }
        _wasActive = active;
        if (!active) return;

        _elapsed += _game.Time.Delta;
        if (_followMouse && !ImGui.GetIO().WantCaptureMouse && _camera.TryScreenToWorld(_input.Mouse.Position, out var mouse) && mouse.LengthSquared() > 0.01f)
            _angle = MathF.Atan2(mouse.Y, mouse.X);
        DrawControls();
    }

    public void Render()
    {
        if (!_wasActive || _shader == null) return;
        var direction = new Vector2(MathF.Cos(_angle), MathF.Sin(_angle));
        var recoil = _fire * (0.5f + 0.5f * MathF.Sin(_elapsed * 22f));
        _shader.Material.Fragment.SetUniformBuffer(new GunUniforms
        {
            Params = new(_elapsed, _scale, _fire, recoil), Direction = new(direction, 0f, 0f),
            MetalColor = new(_metal, 1f), GripColor = new(_grip, 1f), AccentColor = new(_accent, 1f), GlowColor = new(_glow, 1f),
            PanelColor = new(_panel, 1f), BrassColor = new(_brass, 1f),
        });
        _shader.Material.Vertex.SetUniformBuffer(new VertexUniforms { CameraMatrix = ToMatrix4x4(_camera.Matrix) }, 1);
        _batcher.PushMaterial(_shader.Material);
        _batcher.PushMatrix(Matrix3x2.Identity, relative: false);
        _batcher.Quad(null,
            new Vector2(-7.5f, -7.5f), new Vector2(7.5f, -7.5f), new Vector2(7.5f, 7.5f), new Vector2(-7.5f, 7.5f),
            Vector2.Zero, Vector2.UnitX, Vector2.One, Vector2.UnitY, Color.White);
        _batcher.PopMatrix();
        _batcher.PopMaterial();
    }

    private static Matrix4x4 ToMatrix4x4(in Matrix3x2 matrix) => new(
        matrix.M11, matrix.M12, 0f, 0f, matrix.M21, matrix.M22, 0f, 0f,
        0f, 0f, 1f, 0f, matrix.M31, matrix.M32, 0f, 1f);

    private void DrawControls()
    {
        ImGui.SetNextWindowPos(ImGui.GetMainViewport().WorkPos + new Vector2(16, 100), ImGuiCond.FirstUseEver);
        ImGui.Begin("Machine Gun SDF Shader");
        ImGui.TextUnformatted("Cyber SDF: armoured receiver, energy rail, segmented barrel, muzzle flash");
        ImGui.Checkbox("Follow mouse", ref _followMouse);
        ImGui.SliderAngle("Aim", ref _angle);
        ImGui.SliderFloat("Scale", ref _scale, 0.6f, 1.4f);
        ImGui.SliderFloat("Fire intensity", ref _fire, 0f, 1f);
        ImGui.ColorEdit3("Armour", ref _metal);
        ImGui.ColorEdit3("Polymer", ref _grip);
        ImGui.ColorEdit3("Warning trim", ref _accent);
        ImGui.ColorEdit3("Energy glow", ref _glow);
        ImGui.ColorEdit3("Armour panels", ref _panel);
        ImGui.ColorEdit3("Brass pipes", ref _brass);
        ImGui.End();
    }
}
