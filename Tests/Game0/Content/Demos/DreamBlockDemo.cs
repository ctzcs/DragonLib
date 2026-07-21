using System.Numerics;
using System.Runtime.InteropServices;
using DCFApixels.DragonECS;
using Engine.ECS;
using Engine.Rendering;
using Engine.World;
using Foster.Framework;
using ImGuiNET;

namespace Game0;

public sealed class DreamBlockDemoSystem : IEcsInit, IEcsDestroy, IUpdateSystem, IRenderSystem
{
    private const int ImpactCount = 4;
    private const float InteractionMargin = 0.45f;
    private const float DrawMargin = 1.25f;
    private const float ImpactDuration = 1.4f;
    private const string ShaderResourceBase = "Game0/Shaders/DreamBlock";

    [StructLayout(LayoutKind.Sequential)]
    private struct DreamBlockUniformData
    {
        public Vector4 DeepColor;
        public Vector4 MidColor;
        public Vector4 EdgeColor;
        public Vector4 Animation;
        public Vector4 Shape;
        public Vector4 Effect;
        public Vector4 Impact0;
        public Vector4 Impact1;
        public Vector4 Impact2;
        public Vector4 Impact3;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DreamBlockVertexUniformData
    {
        public Matrix4x4 CameraMatrix;
        public Vector4 Animation;
        public Vector4 Shape;
        public Vector4 Effect;
        public Vector4 Impact0;
        public Vector4 Impact1;
        public Vector4 Impact2;
        public Vector4 Impact3;
    }

    private struct Impact
    {
        public Vector2 LocalPosition;
        public float StartedAt;
        public float Strength;
        public bool Active;
    }

    private sealed class DreamBlock(Vector2 center, Vector2 size)
    {
        public readonly Vector2 Center = center;
        public readonly Vector2 Size = size;
        public readonly Impact[] Impacts = new Impact[ImpactCount];
        public int NextImpact;

        public void ClearImpacts()
        {
            Array.Clear(Impacts);
            NextImpact = 0;
        }
    }

    private readonly record struct CameraState(Vector2 Position, float Zoom, float Rotation, float Ppu);

    [DI] private SceneRouter<RuntimeScene> _sceneRouter = null!;
    [DI] private Batcher _batcher = null!;
    [DI] private Camera2D _camera = null!;
    [DI] private Input _input = null!;
    [DI] private MyGame _game = null!;

    private readonly DreamBlock[] _blocks =
    [
        new(new Vector2(-13f, -4.5f), new Vector2(15f, 6f)),
        new(new Vector2(4f, -3.5f), new Vector2(8f, 8f)),
        new(new Vector2(15f, 5f), new Vector2(6f, 13f)),
    ];

    private EmbeddedShaderMaterial? _shader;
    private CameraState _savedCamera;
    private bool _wasActive;
    private bool _paused;
    private float _elapsed;

    private float _animationSpeed = 1f;
    private float _flowSpeed = 1f;
    private float _warpAmount = 0.09f;
    private float _rippleStrength = 0.32f;
    private float _edgeWidth = 0.16f;
    private float _glowIntensity = 0.9f;
    private int _gridDensity = 2;
    private Vector3 _deepColor = new(0.025f, 0.11f, 0.20f);
    private Vector3 _midColor = new(0.05f, 0.58f, 0.72f);
    private Vector3 _edgeColor = new(0.55f, 0.96f, 1f);

    public void Init()
    {
        _shader = EmbeddedShaderMaterial.Load(
            _game.GraphicsDevice,
            typeof(DreamBlockDemoSystem).Assembly,
            ShaderResourceBase,
            fragment: new ShaderStageSpec(0, 1, "fragment_main"),
            vertex: new ShaderStageSpec(0, 2, "vertex_main"));
    }

    public void Destroy()
    {
        _shader?.Dispose();
        _shader = null;
    }

    public void Update()
    {
        HandleSceneTransition();
        if (!_wasActive)
            return;

        DrawControls();

        if (!_paused)
            _elapsed += _game.Time.Delta * _animationSpeed;

        ExpireImpacts();
        HandlePointerImpact();
    }

    public void Render()
    {
        if (!_wasActive || _shader == null)
            return;

        foreach (var block in _blocks)
            DrawBlock(block);
    }

    private void HandleSceneTransition()
    {
        var isActive = _sceneRouter.Current == RuntimeScene.DreamBlockShader;
        if (isActive && !_wasActive)
        {
            _savedCamera = new CameraState(_camera.Position, _camera.Zoom, _camera.Rotation, _camera.PPU);
            _camera.Position = Vector2.Zero;
            _camera.Zoom = 1f;
            _camera.Rotation = 0f;
            _camera.PPU = 32f;
            _elapsed = 0f;
            ClearImpacts();
        }
        else if (!isActive && _wasActive)
        {
            _camera.Position = _savedCamera.Position;
            _camera.Zoom = _savedCamera.Zoom;
            _camera.Rotation = _savedCamera.Rotation;
            _camera.PPU = _savedCamera.Ppu;
        }

        _wasActive = isActive;
    }

    private void DrawControls()
    {
        var viewport = ImGui.GetMainViewport();
        var initialPosition = viewport.WorkPos + new Vector2(viewport.WorkSize.X - 336f, 16f);
        ImGui.SetNextWindowPos(initialPosition, ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(320f, 0f), ImGuiCond.FirstUseEver);
        ImGui.Begin("Dream Block Shader");
        ImGui.Checkbox("Pause", ref _paused);
        ImGui.SameLine();
        if (ImGui.Button("Reset"))
            ResetSettings();

        ImGui.SliderFloat("Animation Speed", ref _animationSpeed, 0.1f, 3f);
        ImGui.SliderFloat("Flow Speed", ref _flowSpeed, 0f, 3f);
        ImGui.SliderFloat("Edge Wobble", ref _warpAmount, 0f, 0.3f);
        ImGui.SliderFloat("Ripple Strength", ref _rippleStrength, 0f, 0.7f);
        ImGui.SliderFloat("Edge Width", ref _edgeWidth, 0.03f, 0.4f);
        ImGui.SliderFloat("Glow", ref _glowIntensity, 0f, 2f);
        ImGui.SliderInt("Grid Density", ref _gridDensity, 1, 4);
        ImGui.ColorEdit3("Deep Color", ref _deepColor);
        ImGui.ColorEdit3("Flow Color", ref _midColor);
        ImGui.ColorEdit3("Edge Color", ref _edgeColor);
        ImGui.End();
    }

    private void ResetSettings()
    {
        _paused = false;
        _elapsed = 0f;
        _animationSpeed = 1f;
        _flowSpeed = 1f;
        _warpAmount = 0.09f;
        _rippleStrength = 0.32f;
        _edgeWidth = 0.16f;
        _glowIntensity = 0.9f;
        _gridDensity = 2;
        _deepColor = new Vector3(0.025f, 0.11f, 0.20f);
        _midColor = new Vector3(0.05f, 0.58f, 0.72f);
        _edgeColor = new Vector3(0.55f, 0.96f, 1f);
        ClearImpacts();
    }

    private void ClearImpacts()
    {
        foreach (var block in _blocks)
            block.ClearImpacts();
    }

    private void ExpireImpacts()
    {
        foreach (var block in _blocks)
        {
            foreach (ref var impact in block.Impacts.AsSpan())
            {
                if (impact.Active && _elapsed - impact.StartedAt > ImpactDuration)
                    impact.Active = false;
            }
        }
    }

    private void HandlePointerImpact()
    {
        if (!_input.Mouse.LeftPressed || ImGui.GetIO().WantCaptureMouse)
            return;

        if (!Matrix3x2.Invert(_camera.Matrix, out var inverseCamera))
            return;

        var worldPosition = Vector2.Transform(_input.Mouse.Position, inverseCamera);
        foreach (var block in _blocks)
        {
            var local = worldPosition - block.Center;
            var half = block.Size * 0.5f;
            if (MathF.Abs(local.X) > half.X + InteractionMargin ||
                MathF.Abs(local.Y) > half.Y + InteractionMargin)
                continue;

            AddImpact(block, ProjectToBoundary(local, half));
            break;
        }
    }

    private void AddImpact(DreamBlock block, Vector2 localPosition)
    {
        block.Impacts[block.NextImpact] = new Impact
        {
            LocalPosition = localPosition,
            StartedAt = _elapsed,
            Strength = 1f,
            Active = true,
        };
        block.NextImpact = (block.NextImpact + 1) % ImpactCount;
    }

    private static Vector2 ProjectToBoundary(Vector2 local, Vector2 half)
    {
        var projected = Vector2.Clamp(local, -half, half);
        var inside = MathF.Abs(local.X) <= half.X && MathF.Abs(local.Y) <= half.Y;
        if (!inside)
            return projected;

        var distanceX = half.X - MathF.Abs(local.X);
        var distanceY = half.Y - MathF.Abs(local.Y);
        if (distanceX < distanceY)
            projected.X = MathF.CopySign(half.X, local.X == 0f ? 1f : local.X);
        else
            projected.Y = MathF.CopySign(half.Y, local.Y == 0f ? 1f : local.Y);
        return projected;
    }

    private void DrawBlock(DreamBlock block)
    {
        var outerSize = block.Size + new Vector2(DrawMargin * 2f);
        var halfOuter = outerSize * 0.5f;
        var topLeft = block.Center - halfOuter;

        var uniforms = new DreamBlockUniformData
        {
            DeepColor = new Vector4(_deepColor, 1f),
            MidColor = new Vector4(_midColor, 1f),
            EdgeColor = new Vector4(_edgeColor, 1f),
            Animation = new Vector4(_elapsed, _flowSpeed, _warpAmount, _edgeWidth),
            Shape = new Vector4(outerSize.X, outerSize.Y, block.Size.X * 0.5f, block.Size.Y * 0.5f),
            Effect = new Vector4(0.65f, _glowIntensity, _rippleStrength, ImpactDuration),
            Impact0 = GetImpact(block.Impacts[0]),
            Impact1 = GetImpact(block.Impacts[1]),
            Impact2 = GetImpact(block.Impacts[2]),
            Impact3 = GetImpact(block.Impacts[3]),
        };

        var vertexUniforms = new DreamBlockVertexUniformData
        {
            CameraMatrix = ToMatrix4x4(_camera.Matrix),
            Animation = uniforms.Animation,
            Shape = uniforms.Shape,
            Effect = uniforms.Effect,
            Impact0 = uniforms.Impact0,
            Impact1 = uniforms.Impact1,
            Impact2 = uniforms.Impact2,
            Impact3 = uniforms.Impact3,
        };

        _shader!.Material.Fragment.SetUniformBuffer(uniforms);
        _shader.Material.Vertex.SetUniformBuffer(vertexUniforms, slot: 1);
        _batcher.PushMaterial(_shader.Material);
        _batcher.PushMatrix(Matrix3x2.Identity, relative: false);
        DrawSubdividedGrid(topLeft, outerSize);
        _batcher.PopMatrix();
        _batcher.PopMaterial();
    }

    private void DrawSubdividedGrid(Vector2 topLeft, Vector2 size)
    {
        var columns = Math.Clamp((int)MathF.Ceiling(size.X * _gridDensity), 4, 32);
        var rows = Math.Clamp((int)MathF.Ceiling(size.Y * _gridDensity), 4, 32);

        for (var y = 0; y < rows; y++)
        {
            var v0 = y / (float)rows;
            var v1 = (y + 1) / (float)rows;
            for (var x = 0; x < columns; x++)
            {
                var u0 = x / (float)columns;
                var u1 = (x + 1) / (float)columns;

                var topLeftVertex = topLeft + new Vector2(size.X * u0, size.Y * v0);
                var topRightVertex = topLeft + new Vector2(size.X * u1, size.Y * v0);
                var bottomRightVertex = topLeft + new Vector2(size.X * u1, size.Y * v1);
                var bottomLeftVertex = topLeft + new Vector2(size.X * u0, size.Y * v1);

                _batcher.Quad(
                    null,
                    topLeftVertex,
                    topRightVertex,
                    bottomRightVertex,
                    bottomLeftVertex,
                    new Vector2(u0, v0),
                    new Vector2(u1, v0),
                    new Vector2(u1, v1),
                    new Vector2(u0, v1),
                    Color.White);
            }
        }
    }

    private static Matrix4x4 ToMatrix4x4(in Matrix3x2 matrix) => new(
        matrix.M11, matrix.M12, 0f, 0f,
        matrix.M21, matrix.M22, 0f, 0f,
        0f, 0f, 1f, 0f,
        matrix.M31, matrix.M32, 0f, 1f);

    private Vector4 GetImpact(in Impact impact)
    {
        if (!impact.Active)
            return new Vector4(0f, 0f, ImpactDuration + 1f, 0f);

        return new Vector4(
            impact.LocalPosition.X,
            impact.LocalPosition.Y,
            _elapsed - impact.StartedAt,
            impact.Strength);
    }
}
