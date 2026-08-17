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

public sealed class FishSdfDemoSystem : IEcsInit, IEcsDestroy, IUpdateSystem, IRenderSystem
{
    private const string ShaderResourceBase = "Game0/Shaders/FishSdf";

    [StructLayout(LayoutKind.Sequential)]
    private struct FishUniforms
    {
        public Vector4 Params;
        public Vector4 Direction;
        public Vector4 BaseColor;
        public Vector4 StripeColor;
        public Vector4 BellyColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FishVertexUniforms
    {
        public Matrix4x4 CameraMatrix;
    }

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
    private float _length = 5.2f;
    private float _height = 1.45f;
    private float _tail = 1.55f;
    private float _angle = 0.15f;
    private Vector3 _base = new(0.93f, 0.31f, 0.12f);
    private Vector3 _stripe = new(1.0f, 0.76f, 0.24f);
    private Vector3 _belly = new(0.97f, 0.80f, 0.56f);

    public void Init()
    {
        _shader = EmbeddedShaderMaterial.Load(_game.GraphicsDevice, typeof(FishSdfDemoSystem).Assembly,
            ShaderResourceBase, new ShaderStageSpec(0, 1, "fragment_main"), new ShaderStageSpec(0, 2, "vertex_main"));
    }

    public void Destroy() => _shader?.Dispose();

    public void Update()
    {
        var active = _sceneRouter.Current == RuntimeScene.FishSdfShader;
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
        var data = new FishUniforms {
            Params = new(_elapsed, _length, _height, _tail), Direction = new(direction, 0f, 0f),
            BaseColor = new(_base, 1f), StripeColor = new(_stripe, 1f), BellyColor = new(_belly, 1f),
        };
        _shader.Material.Fragment.SetUniformBuffer(data);
        _shader.Material.Vertex.SetUniformBuffer(new FishVertexUniforms
        {
            CameraMatrix = ToMatrix4x4(_camera.Matrix),
        }, slot: 1);
        _batcher.PushMaterial(_shader.Material);
        // The shader applies CameraMatrix itself; prevent the batcher applying it a second time.
        _batcher.PushMatrix(Matrix3x2.Identity, relative: false);
        _batcher.Quad(
            null,
            new Vector2(-6f, -6f), new Vector2(6f, -6f), new Vector2(6f, 6f), new Vector2(-6f, 6f),
            Vector2.Zero, Vector2.UnitX, Vector2.One, Vector2.UnitY,
            Color.White);
        _batcher.PopMatrix();
        _batcher.PopMaterial();
    }

    private static Matrix4x4 ToMatrix4x4(in Matrix3x2 matrix) => new(
        matrix.M11, matrix.M12, 0f, 0f,
        matrix.M21, matrix.M22, 0f, 0f,
        0f, 0f, 1f, 0f,
        matrix.M31, matrix.M32, 0f, 1f);

    private void DrawControls()
    {
        ImGui.SetNextWindowPos(ImGui.GetMainViewport().WorkPos + new Vector2(16, 100), ImGuiCond.FirstUseEver);
        ImGui.Begin("Fish SDF Shader");
        ImGui.TextUnformatted("Textureless: SDF shape -> pattern -> lighting -> details");
        ImGui.Checkbox("Follow mouse", ref _followMouse);
        ImGui.SliderAngle("Direction", ref _angle);
        ImGui.SliderFloat("Body length", ref _length, 2.5f, 7f);
        ImGui.SliderFloat("Body height", ref _height, 0.5f, 2.2f);
        ImGui.SliderFloat("Tail size", ref _tail, 0.4f, 2.5f);
        ImGui.ColorEdit3("Base color", ref _base);
        ImGui.ColorEdit3("Stripe color", ref _stripe);
        ImGui.ColorEdit3("Belly color", ref _belly);
        ImGui.End();
    }
}
