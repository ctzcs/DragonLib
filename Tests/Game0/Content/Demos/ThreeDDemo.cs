using System.Diagnostics;
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
/// An instanced 3D benchmark that renders a procedural asteroid belt in one draw call.
/// </summary>
public sealed class ThreeDDemoSystem : IEcsInit, IEcsDestroy, IUpdateSystem, IRenderSystem
{
    private const string ShaderResourceBase = "Game0/Shaders/Basic3D";
    private const string InstancedShaderResourceBase = "Game0/Shaders/Basic3DInstanced";

    [DI] private SceneRouter<RuntimeScene> _sceneRouter = null!;
    [DI] private Camera3D _camera = null!;
    [DI] private Batcher _batcher = null!;
    [DI] private Input _input = null!;
    [DI] private MyGame _game = null!;

    private EmbeddedShaderMaterial? _shader;
    private EmbeddedShaderMaterial? _instancedShader;
    private const int MaximumInstanceCount = 1_000_000;
    private const float AutoWarmupSeconds = 1f;
    private const float AutoSampleSeconds = 2f;
    private static readonly int[] InstancePresets =
        [1_000, 10_000, 50_000, 100_000, 250_000, 500_000, 1_000_000];

    private Mesh3D? _planet;
    private Mesh3D? _asteroid;
    private VertexBuffer<AsteroidInstance>? _asteroidInstances;
    private RenderTarget3D? _renderTarget;
    private Renderer3D? _renderer;
    private bool _wasActive;
    private float _yaw = 0.65f;
    private float _pitch = 0.38f;
    private float _distance = 34f;
    private Vector3 _target = Vector3.Zero;
    private float _elapsed;
    private int _instanceCount = 10_000;
    private int _requestedInstanceCount = 10_000;
    private double _buildMilliseconds;
    private bool _autoRunning;
    private int _autoPresetIndex;
    private float _autoStageElapsed;
    private float _autoFpsTotal;
    private float _autoFpsDuration;
    private int _lastStableCount;
    private float _targetFramesPerSecond = 60f;
    private string _autoStatus = "Ready";
    private float _lightAzimuth = -2.54f;
    private float _lightElevation = 0.62f;
    private Vector3 _ambientLightColor = new(0.32f, 0.36f, 0.44f);
    private Vector3 _diffuseLightColor = new(1.00f, 0.91f, 0.76f);
    private float _ambientLightIntensity = 1f;
    private float _diffuseLightIntensity = 1f;
    private float _ringInnerRadius = 7.5f;
    private float _ringOuterRadius = 21f;
    private float _requestedRingInnerRadius = 7.5f;
    private float _requestedRingOuterRadius = 21f;

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct AsteroidInstance : IVertex
    {
        public Matrix4x4 World;
        public Color Tint;
        public float OrbitSpeed;

        public AsteroidInstance(in Matrix4x4 world, Color tint, float orbitSpeed)
        {
            World = world;
            Tint = tint;
            OrbitSpeed = orbitSpeed;
        }

        public readonly VertexFormat Format => format;

        private static readonly VertexFormat format = VertexFormat.Create<AsteroidInstance>(
            new(3, VertexType.Float4, false),
            new(4, VertexType.Float4, false),
            new(5, VertexType.Float4, false),
            new(6, VertexType.Float4, false),
            new(7, VertexType.UByte4, true),
            new(8, VertexType.Float, false));
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct OrbitUniforms
    {
        public Vector4 OrbitParams;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct Basic3DLightUniforms
    {
        public Vector4 LightDirection;
        public Vector4 Ambient;
        public Vector4 Diffuse;
    }

    public void Init()
    {
        _shader = EmbeddedShaderMaterial.Load(
            _game.GraphicsDevice,
            typeof(ThreeDDemoSystem).Assembly,
            ShaderResourceBase,
            new ShaderStageSpec(0, 1, "fragment_main"),
            new ShaderStageSpec(0, 1, "vertex_main"));
        _instancedShader = EmbeddedShaderMaterial.Load(
            _game.GraphicsDevice,
            typeof(ThreeDDemoSystem).Assembly,
            InstancedShaderResourceBase,
            new ShaderStageSpec(0, 1, "fragment_main"),
            new ShaderStageSpec(0, 2, "vertex_main"));

        _planet = Mesh3D.CreateIcosphere(
            _game.GraphicsDevice, 3.6f, 3, 0.035f, new Color(0x4F8190), 81, "Benchmark Planet");
        _asteroid = Mesh3D.CreateIcosphere(
            _game.GraphicsDevice, 1f, 0, 0.28f, Color.White, 19, "Benchmark Asteroid");
        _asteroidInstances = new VertexBuffer<AsteroidInstance>(_game.GraphicsDevice, "Asteroid Instances");
        _renderTarget = new RenderTarget3D(_game.GraphicsDevice);
        _renderer = new Renderer3D(_game.GraphicsDevice);
        RebuildAsteroids(_instanceCount);
    }

    public void Destroy()
    {
        _planet?.Dispose();
        _asteroid?.Dispose();
        _asteroidInstances?.Dispose();
        _renderTarget?.Dispose();
        _renderer?.Dispose();
        _shader?.Dispose();
        _instancedShader?.Dispose();
        _planet = null;
        _asteroid = null;
        _asteroidInstances = null;
        _renderTarget = null;
        _shader = null;
        _instancedShader = null;
        _renderer = null;
    }

    public void Update()
    {
        var active = _sceneRouter.Current == RuntimeScene.ThreeD;
        if (active && !_wasActive)
        {
            _yaw = 0.65f;
            _pitch = 0.38f;
            _distance = 34f;
            _target = Vector3.Zero;
            _elapsed = 0f;
        }

        _wasActive = active;
        if (!active)
            return;

        _elapsed += _game.Time.Delta;
        UpdateAutomaticBenchmark();
        var io = ImGui.GetIO();
        if (!io.WantCaptureMouse)
        {
            if (_input.Mouse.RightDown)
            {
                _yaw -= _input.Mouse.Delta.X * 0.008f;
                _pitch = Math.Clamp(_pitch - _input.Mouse.Delta.Y * 0.008f, -0.05f, 1.2f);
            }

            _distance = Math.Clamp(_distance - _input.Mouse.Wheel.Y * 1.25f, 12f, 70f);
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
                movement = Vector3.Normalize(movement) * (_game.Time.Delta * 7f);
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
        if (!_wasActive || _planet == null || _asteroid == null || _asteroidInstances == null ||
            _renderTarget == null || _shader == null || _instancedShader == null || _renderer == null)
            return;

        var width = _game.Window.WidthInPixels;
        var height = _game.Window.HeightInPixels;
        _renderTarget.Resize(width, height);
        _renderTarget.Clear(new Color(0x090D16));
        _camera.ViewportSize = new Point2(width, height);

        var horizontalLight = MathF.Cos(_lightElevation);
        var lightDirection = new Vector3(
            horizontalLight * MathF.Cos(_lightAzimuth),
            -MathF.Sin(_lightElevation),
            horizontalLight * MathF.Sin(_lightAzimuth));
        var ambient = _ambientLightColor * _ambientLightIntensity;
        var diffuse = _diffuseLightColor * _diffuseLightIntensity;

        var lightUniforms = new Basic3DLightUniforms
        {
            LightDirection = new Vector4(lightDirection, 0f),
            Ambient = new Vector4(ambient, 1f),
            Diffuse = new Vector4(diffuse, 1f),
        };
        _shader.Material.Fragment.SetUniformBuffer(lightUniforms);
        _instancedShader.Material.Fragment.SetUniformBuffer(lightUniforms);

        _renderer.Begin(_renderTarget.Target, _camera);
        _renderer.Draw(_planet, _shader.Material, Matrix4x4.CreateRotationY(_elapsed * 0.08f));
        _instancedShader.Material.Vertex.SetUniformBuffer(new OrbitUniforms
        {
            OrbitParams = new Vector4(_elapsed, 0f, 0f, 0f),
        }, 1);
        _renderer.DrawInstances(_asteroid, _instancedShader.Material, _asteroidInstances, _instanceCount);
        _renderer.End();

        _renderTarget.Composite(_batcher, width, height);
    }

    private void DrawControls()
    {
        ImGui.SetNextWindowPos(new Vector2(16f, 136f), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(390f, 0f), ImGuiCond.FirstUseEver);
        ImGui.Begin("3D Instance Benchmark");
        ImGui.TextUnformatted("GPU-instanced asteroid belt");
        ImGui.TextUnformatted("RMB drag: orbit   Wheel: zoom");
        ImGui.TextUnformatted("WASD: move focus");
        ImGui.Separator();

        var currentPreset = Array.IndexOf(InstancePresets, _instanceCount);
        var preview = currentPreset >= 0 ? $"{InstancePresets[currentPreset]:N0}" : "Custom";
        if (ImGui.BeginCombo("Preset", preview))
        {
            for (var i = 0; i < InstancePresets.Length; i++)
            {
                var selected = i == currentPreset;
                if (ImGui.Selectable($"{InstancePresets[i]:N0}", selected))
                {
                    StopAutomaticBenchmark();
                    _requestedInstanceCount = InstancePresets[i];
                    RebuildAsteroids(_requestedInstanceCount);
                }
                if (selected)
                    ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }

        ImGui.SetNextItemWidth(180f);
        ImGui.InputInt("Custom count", ref _requestedInstanceCount, 1_000, 10_000);
        _requestedInstanceCount = Math.Clamp(_requestedInstanceCount, 1, MaximumInstanceCount);
        if (ImGui.Button("Apply instance count"))
        {
            StopAutomaticBenchmark();
            RebuildAsteroids(_requestedInstanceCount);
        }

        ImGui.Separator();
        ImGui.TextUnformatted("Asteroid ring");
        ImGui.SliderFloat("Inner radius", ref _requestedRingInnerRadius, 4f, 25f, "%.1f");
        ImGui.SliderFloat("Outer radius", ref _requestedRingOuterRadius, 5f, 35f, "%.1f");
        _requestedRingOuterRadius = MathF.Max(
            _requestedRingOuterRadius,
            _requestedRingInnerRadius + 0.5f);
        if (ImGui.Button("Apply ring radius"))
        {
            StopAutomaticBenchmark();
            _ringInnerRadius = _requestedRingInnerRadius;
            _ringOuterRadius = _requestedRingOuterRadius;
            RebuildAsteroids(_instanceCount);
        }

        ImGui.Separator();
        ImGui.SliderFloat("Target FPS", ref _targetFramesPerSecond, 30f, 240f, "%.0f");
        if (!_autoRunning)
        {
            if (ImGui.Button("Start automatic sweep"))
                StartAutomaticBenchmark();
        }
        else if (ImGui.Button("Stop automatic sweep"))
        {
            StopAutomaticBenchmark();
        }

        ImGui.SameLine();
        ImGui.TextDisabled(_autoStatus);
        ImGui.Separator();

        var asteroidTriangles = (long)_asteroid!.Geometry.IndexCount / 3 * _instanceCount;
        var totalTriangles = asteroidTriangles + _planet!.Geometry.IndexCount / 3;
        var bufferMiB = (long)_instanceCount * Marshal.SizeOf<AsteroidInstance>() / (1024d * 1024d);
        ImGui.Text($"Render FPS: {_game.RenderFramesPerSecond:F1}");
        ImGui.Text($"Instances: {_instanceCount:N0}");
        ImGui.Text($"Triangles/frame: {totalTriangles:N0}");
        ImGui.Text($"Draw calls: 2 (planet + asteroid ring)");
        ImGui.Text($"Instance stride: {Marshal.SizeOf<AsteroidInstance>()} bytes");
        ImGui.Text($"Instance buffer: {bufferMiB:F2} MiB");
        ImGui.Text($"Last build + upload: {_buildMilliseconds:F2} ms");
        if (_lastStableCount > 0)
            ImGui.Text($"Last stable at target: {_lastStableCount:N0}");

        ImGui.Separator();
        ImGui.TextUnformatted("Lighting");
        ImGui.SliderAngle("Light azimuth", ref _lightAzimuth, -180f, 180f);
        ImGui.SliderAngle("Light elevation", ref _lightElevation, 5f, 85f);
        ImGui.ColorEdit3("Ambient color", ref _ambientLightColor);
        ImGui.SliderFloat("Ambient intensity", ref _ambientLightIntensity, 0f, 2f, "%.2f");
        ImGui.ColorEdit3("Main light color", ref _diffuseLightColor);
        ImGui.SliderFloat("Main light intensity", ref _diffuseLightIntensity, 0f, 3f, "%.2f");
        if (ImGui.Button("Reset lighting"))
            ResetLighting();
        ImGui.End();
    }

    private void ResetLighting()
    {
        _lightAzimuth = -2.54f;
        _lightElevation = 0.62f;
        _ambientLightColor = new Vector3(0.32f, 0.36f, 0.44f);
        _diffuseLightColor = new Vector3(1.00f, 0.91f, 0.76f);
        _ambientLightIntensity = 1f;
        _diffuseLightIntensity = 1f;
    }

    private void RebuildAsteroids(int count)
    {
        if (_asteroidInstances == null)
            return;

        count = Math.Clamp(count, 1, MaximumInstanceCount);
        var stopwatch = Stopwatch.StartNew();
        var instances = new AsteroidInstance[count];
        var random = new Random(unchecked((int)0xA57E_201D));
        for (var i = 0; i < instances.Length; i++)
        {
            var angle = random.NextSingle() * MathF.Tau;
            var normalizedRadius = random.NextSingle();
            var radius = _ringInnerRadius + normalizedRadius * (_ringOuterRadius - _ringInnerRadius);
            var verticalTaper = 0.3f + MathF.Sin(normalizedRadius * MathF.PI) * 1.7f;
            var y = (random.NextSingle() + random.NextSingle() - 1f) * verticalTaper;
            var position = new Vector3(MathF.Cos(angle) * radius, y, MathF.Sin(angle) * radius);

            var size = 0.12f + MathF.Pow(random.NextSingle(), 2.4f) * 0.62f;
            var scale = new Vector3(
                size * (0.7f + random.NextSingle() * 0.65f),
                size * (0.65f + random.NextSingle() * 0.7f),
                size * (0.7f + random.NextSingle() * 0.65f));
            var rotation = Matrix4x4.CreateFromYawPitchRoll(
                random.NextSingle() * MathF.Tau,
                random.NextSingle() * MathF.Tau,
                random.NextSingle() * MathF.Tau);
            var world = Matrix4x4.CreateScale(scale) * rotation * Matrix4x4.CreateTranslation(position);
            var orbitSpeed = (0.09f + random.NextSingle() * 0.035f) * MathF.Sqrt(12f / radius);
            instances[i] = new AsteroidInstance(
                world,
                CreateAsteroidTint(random),
                orbitSpeed);
        }

        _asteroidInstances.Clear();
        _asteroidInstances.Upload(instances.AsSpan());
        stopwatch.Stop();
        _instanceCount = count;
        _requestedInstanceCount = count;
        _buildMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
    }

    private static Color CreateAsteroidTint(Random random)
    {
        var brightness = 0.72f + random.NextSingle() * 0.38f;
        Vector3 baseColor;
        if (random.NextSingle() < 0.58f)
        {
            baseColor = new Vector3(
                0.50f + random.NextSingle() * 0.18f,
                0.40f + random.NextSingle() * 0.15f,
                0.32f + random.NextSingle() * 0.13f);
        }
        else
        {
            var gray = 0.42f + random.NextSingle() * 0.22f;
            baseColor = new Vector3(
                gray + random.NextSingle() * 0.05f,
                gray + random.NextSingle() * 0.04f,
                gray + 0.02f + random.NextSingle() * 0.07f);
        }

        return new Color(Vector3.Min(baseColor * brightness, Vector3.One));
    }

    private void StartAutomaticBenchmark()
    {
        _autoRunning = true;
        _autoPresetIndex = 0;
        _autoStageElapsed = 0f;
        _autoFpsTotal = 0f;
        _autoFpsDuration = 0f;
        _lastStableCount = 0;
        _autoStatus = "Warming up 1,000";
        RebuildAsteroids(InstancePresets[_autoPresetIndex]);
    }

    private void StopAutomaticBenchmark()
    {
        _autoRunning = false;
        _autoStatus = "Stopped";
    }

    private void UpdateAutomaticBenchmark()
    {
        if (!_autoRunning)
            return;

        var delta = MathF.Min(_game.Time.Delta, 0.1f);
        _autoStageElapsed += delta;
        if (_autoStageElapsed >= AutoWarmupSeconds)
        {
            _autoFpsTotal += _game.RenderFramesPerSecond * delta;
            _autoFpsDuration += delta;
            _autoStatus = $"Sampling {_instanceCount:N0}";
        }

        if (_autoStageElapsed < AutoWarmupSeconds + AutoSampleSeconds)
            return;

        var averageFps = _autoFpsDuration > 0f ? _autoFpsTotal / _autoFpsDuration : 0f;
        if (averageFps < _targetFramesPerSecond)
        {
            _autoRunning = false;
            _autoStatus = $"Stopped at {averageFps:F1} FPS";
            return;
        }

        _lastStableCount = _instanceCount;
        _autoPresetIndex++;
        if (_autoPresetIndex >= InstancePresets.Length)
        {
            _autoRunning = false;
            _autoStatus = $"Completed at {averageFps:F1} FPS";
            return;
        }

        _autoStageElapsed = 0f;
        _autoFpsTotal = 0f;
        _autoFpsDuration = 0f;
        RebuildAsteroids(InstancePresets[_autoPresetIndex]);
        _autoStatus = $"Warming up {_instanceCount:N0}";
    }
}
