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

/// <summary>
/// A multi-pass 2D Radiance Cascades laboratory. The shader follows the
/// reference probe packing, interval cascade, ping-pong merge, and linear
/// filtering steps. Everything the rays can hit is texture-driven, so
/// raymarch cost stays flat no matter how much is drawn or how many lights
/// exist: scene geometry, drawn occluders and painted strokes are baked into
/// one static distance field (rebuilt on change), while point lights are
/// re-stamped every frame into a dynamic emission/distance texture pair.
/// </summary>
public sealed class RadianceCascadesDemoSystem : IEcsInit, IEcsDestroy, IUpdateSystem, IRenderSystem
{
    private const string ShaderResourceBase = "Game0/Shaders/RadianceCascades";
    private const int MaxObstacles = 8;
    private const int MaxCascadeCount = 24;
    private static readonly int[] SupportedBaseRayCounts = [4, 16];

    // User strokes are baked into two fixed-resolution, world-anchored
    // textures (sRGB colors + encoded distance field) instead of a GPU
    // segment buffer, keeping the raymarch cost independent of stroke count,
    // like the reference sandbox's texture-driven architecture.
    private const int PaintTextureWidth = 2048;
    private const int PaintTextureHeight = 1152;
    private const float PaintWorldWidth = 48f;
    private const float PaintWorldHeight = 27f;
    private const float StrokeDistanceLow = -0.8f;
    // Upper encode range. SDF marching leaps by the sampled distance, so a
    // deep range keeps long rays cheap; 8-bit precision (8.8/256 ≈ 0.034
    // world units) is fine for the cascade's low-res probes.
    private const float StrokeDistanceHigh = 8f;
    // Dynamic (light) textures only hold soft radial stamps, so they run at
    // half the static bake resolution to cut per-frame clear/draw bandwidth.
    private const int DynamicTextureWidth = PaintTextureWidth / 2;
    private const int DynamicTextureHeight = PaintTextureHeight / 2;

    // The reference sandbox paints with a fixed swatch palette beside the
    // canvas; stroke colors are stored in linear space so the final 1/2.2
    // output matches the swatch the user picked.
    private static readonly (Color Swatch, Vector3 Emission)[] StrokePalette =
    [
        (new Color(255, 246, 211, 255), SrgbToLinear(255, 246, 211)),
        (new Color(249, 168, 117, 255), SrgbToLinear(249, 168, 117)),
        (new Color(235, 107, 111, 255), SrgbToLinear(235, 107, 111)),
        (new Color(124, 63, 88, 255), SrgbToLinear(124, 63, 88)),
        (new Color(3, 196, 161, 255), SrgbToLinear(3, 196, 161)),
        (new Color(61, 158, 252, 255), SrgbToLinear(61, 158, 252)),
    ];

    private static Vector3 SrgbToLinear(byte r, byte g, byte b) => new(
        MathF.Pow(r / 255f, 2.2f),
        MathF.Pow(g / 255f, 2.2f),
        MathF.Pow(b / 255f, 2.2f));

    private const int ViewportUniformIndex = 0;
    private const int CascadeUniformIndex = ViewportUniformIndex + 1;
    private const int DisplayUniformIndex = CascadeUniformIndex + 1;
    private const int WorldUniformIndex = DisplayUniformIndex + 1;
    private const int TuningUniformIndex = WorldUniformIndex + 1;
    private const int PaintUniformIndex = TuningUniformIndex + 1;
    private const int UniformVectorCount = PaintUniformIndex + 1;

    private sealed class LightSource
    {
        public Vector2 Position;
        public Vector3 Color;
        public float Intensity;
        public float Radius;
        public bool Enabled = true;

        public LightSource(Vector2 position, Vector3 color, float intensity, float radius)
        {
            Position = position;
            Color = color;
            Intensity = intensity;
            Radius = radius;
        }
    }

    private sealed class EmissionSegment
    {
        public Vector2 Start;
        public Vector2 End;
        public Vector3 Color;
        public float Intensity;
        public float Radius;

        public EmissionSegment(Vector2 start, Vector2 end, Vector3 color, float intensity, float radius)
        {
            Start = start;
            End = end;
            Color = color;
            Intensity = intensity;
            Radius = radius;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct StrokeFieldUniforms
    {
        public Vector4 Segment; // capsule: xy start, zw end; circle/round box: xy center, zw half size
        public Vector4 Params;  // x radius, y encode scale, z primitive type (0 capsule, 1 circle, 2 round box), w encode offset
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct LightStampUniforms
    {
        public Vector4 Light;   // xy center, z effective radius, w intensity
        public Vector4 Color;   // rgb linear color
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct VertexUniforms
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
    private EmbeddedShaderMaterial? _strokeFieldShader;
    private EmbeddedShaderMaterial? _lightStampShader;
    private EmbeddedShaderMaterial? _compositeShader;
    private EmbeddedShaderMaterial? _blurShader;
    private Batcher? _passBatcher;
    private Target? _targetA;
    private Target? _targetB;
    private Target? _staticEmissionTarget;
    private Target? _staticDistanceTarget;
    private Target? _dynamicEmissionTarget;
    private Target? _dynamicDistanceTarget;
    // Bench-only: window-sized target the composite is re-rendered into so the
    // final image can be dumped deterministically (OS window captures are
    // unreliable — z-order, focus, other apps).
    private Target? _compositeTarget;
    private bool _compositeDumped;
    private bool _paintDirty = true;
    private int _targetWidth;
    private int _targetHeight;
    private CameraState _savedCamera;
    private bool _wasActive;
    private bool _draggingLight;
    private bool _drawingEmission;
    private Vector2 _lastDrawPoint;
    private int _obstacleCount;
    private readonly Vector4[] _obstacles = new Vector4[MaxObstacles];
    private readonly List<EmissionSegment> _emissionSegments = new();
    private Vector3 _emissionColor = StrokePalette[1].Emission;
    private int _selectedPaletteIndex = 1;
    private float _emissionIntensity = 4f;
    private float _emissionRadius = 0.16f;

    private Vector3 _worldColor = new(0.06f, 0.09f, 0.14f);
    private float _maxRayDistance = 55f;
    private float _intervalLength = 2f;
    private float _intervalOverlap = 0.04f;
    private float _renderScale = 0.2f;
    private int _raymarchSteps = 10;
    private int _giSmoothing = 2;
    private float _giIntensity = 0.7f;
    private float _giFloor = 0.02f;
    private int _baseRayCount = 16;
    private int _cascadeCount = 4;
    private bool _autoCascadeCount = true;
    private int _cascadeIndex = -1;
    private bool _linearFilter = true;
    private bool _correctSrgb = true;
    private bool _showProbes;
    private bool _showScene = true;
    private int _selectedLight;
    private readonly List<LightSource> _lights = new();
    private readonly Vector4[] _fragmentUniformVectors = new Vector4[UniformVectorCount];

    public void Init()
    {
        _shader = EmbeddedShaderMaterial.Load(
            _game.GraphicsDevice,
            typeof(RadianceCascadesDemoSystem).Assembly,
            ShaderResourceBase,
            new ShaderStageSpec(5, 1, "fragment_main"),
            new ShaderStageSpec(0, 2, "vertex_main"));
        _strokeFieldShader = EmbeddedShaderMaterial.Load(
            _game.GraphicsDevice,
            typeof(RadianceCascadesDemoSystem).Assembly,
            "Game0/Shaders/RadianceCascadesStroke",
            new ShaderStageSpec(0, 1, "fragment_main"),
            new ShaderStageSpec(0, 1, "vertex_main"));
        _lightStampShader = EmbeddedShaderMaterial.Load(
            _game.GraphicsDevice,
            typeof(RadianceCascadesDemoSystem).Assembly,
            "Game0/Shaders/RadianceCascadesLight",
            new ShaderStageSpec(0, 1, "fragment_main"),
            new ShaderStageSpec(0, 1, "vertex_main"));
        _compositeShader = EmbeddedShaderMaterial.Load(
            _game.GraphicsDevice,
            typeof(RadianceCascadesDemoSystem).Assembly,
            "Game0/Shaders/RadianceCascadesComposite",
            new ShaderStageSpec(4, 1, "fragment_main"),
            new ShaderStageSpec(0, 1, "vertex_main"));
        _blurShader = EmbeddedShaderMaterial.Load(
            _game.GraphicsDevice,
            typeof(RadianceCascadesDemoSystem).Assembly,
            "Game0/Shaders/RadianceCascadesBlur",
            new ShaderStageSpec(1, 1, "fragment_main"),
            new ShaderStageSpec(0, 1, "vertex_main"));
        _passBatcher = new Batcher(_game.GraphicsDevice);
        _staticEmissionTarget = new Target(_game.GraphicsDevice, PaintTextureWidth, PaintTextureHeight, name: "Radiance Cascades static emission");
        _staticDistanceTarget = new Target(_game.GraphicsDevice, PaintTextureWidth, PaintTextureHeight, name: "Radiance Cascades static distance");
        _dynamicEmissionTarget = new Target(_game.GraphicsDevice, DynamicTextureWidth, DynamicTextureHeight, name: "Radiance Cascades dynamic emission");
        _dynamicDistanceTarget = new Target(_game.GraphicsDevice, DynamicTextureWidth, DynamicTextureHeight, name: "Radiance Cascades dynamic distance");
        _paintDirty = true;
    }

    public void Destroy()
    {
        _shader?.Dispose();
        _shader = null;
        _strokeFieldShader?.Dispose();
        _strokeFieldShader = null;
        _lightStampShader?.Dispose();
        _lightStampShader = null;
        _compositeShader?.Dispose();
        _compositeShader = null;
        _blurShader?.Dispose();
        _blurShader = null;
        _targetA?.Dispose();
        _targetB?.Dispose();
        _staticEmissionTarget?.Dispose();
        _staticDistanceTarget?.Dispose();
        _dynamicEmissionTarget?.Dispose();
        _dynamicDistanceTarget?.Dispose();
        _compositeTarget?.Dispose();
        _compositeTarget = null;
        _passBatcher?.Dispose();
        _targetA = null;
        _targetB = null;
        _staticEmissionTarget = null;
        _staticDistanceTarget = null;
        _dynamicEmissionTarget = null;
        _dynamicDistanceTarget = null;
        _passBatcher = null;
    }

    // --- Benchmark hook: set RC_BENCH=<seconds> to auto-enter this scene,
    // disable vsync and unlock the update loop, sample Render FPS after a
    // warmup, then write the result to RC_BENCH_OUT (default rc-bench.txt)
    // and exit. Optional overrides: RC_BENCH_SCALE / RC_BENCH_RAYS /
    // RC_BENCH_STEPS / RC_BENCH_LIGHTS. Used to A/B the texture-driven
    // rewrite against the analytic baseline.
    private static readonly int BenchSeconds =
        int.TryParse(Environment.GetEnvironmentVariable("RC_BENCH"), out var benchEnv) ? benchEnv : 0;
    private bool _benchStarted;
    private bool _benchConfigured;
    private bool _benchDumpPending = Environment.GetEnvironmentVariable("RC_BENCH_DUMP") != null;
    private double _benchElapsed;
    private const double BenchWarmupSeconds = 3.0;
    private float _benchMin = float.MaxValue;
    private float _benchMax;
    private double _benchSum;
    private int _benchCount;

    public void Update()
    {
        if (BenchSeconds > 0 && !_benchStarted && _sceneRouter.Current == RuntimeScene.Main)
        {
            _game.GraphicsDevice.VSync = false;
            _game.UpdateMode = UpdateMode.UnlockedStep();
            _sceneRouter.SwitchTo(RuntimeScene.RadianceCascades2D);
            _benchStarted = true;
        }

        var active = _sceneRouter.Current == RuntimeScene.RadianceCascades2D;
        if (active && !_wasActive)
        {
            _savedCamera = new(_camera.Position, _camera.Zoom, _camera.Rotation, _camera.PPU);
            _camera.Position = Vector2.Zero;
            _camera.Zoom = 1f;
            _camera.Rotation = 0f;
            _camera.PPU = 52f;
            ResetScene();
        }
        else if (!active && _wasActive)
        {
            _camera.Position = _savedCamera.Position;
            _camera.Zoom = _savedCamera.Zoom;
            _camera.Rotation = _savedCamera.Rotation;
            _camera.PPU = _savedCamera.Ppu;
        }

        _wasActive = active;
        if (!active)
            return;

        if (_benchStarted)
        {
            UpdateBenchmark();
            if (!_benchConfigured)
                ConfigureBenchmark();
        }

        DrawControls();
        HandleCanvasInput();
    }

    // One separable gaussian pass over a cascade target (see
    // RadianceCascadesBlur.hlsl). The optimized taps rely on linear sampling.
    private void BlurPass(Target source, Target destination, float dirX, float dirY, in TextureSampler sampler)
    {
        _blurShader!.Material.Fragment.SetUniformBuffer(new Vector4(dirX, dirY, 0f, 0f));
        _passBatcher!.PushMaterial(_blurShader.Material);
        _passBatcher.PushSampler(sampler);
        _passBatcher.PushMatrix(Matrix3x2.Identity, relative: false);
        _passBatcher.Quad(source.Attachments[0],
            new Vector2(0f, 0f), new Vector2(source.Width, 0f), new Vector2(source.Width, source.Height), new Vector2(0f, source.Height),
            Vector2.Zero, Vector2.UnitX, Vector2.One, Vector2.UnitY, Color.White);
        _passBatcher.PopMatrix();
        _passBatcher.PopSampler();
        _passBatcher.PopMaterial();
        _passBatcher.Render(destination);
        _passBatcher.Clear();
    }

    // Dumps a bake target to a PNG for visual verification of the
    // texture-driven pipeline (RC_BENCH_DUMP=1).
    private static void DumpTarget(Target target, string fileName)
    {
        try
        {
            var pixels = new Color[target.Width * target.Height];
            target.Attachments[0].GetData<Color>(pixels);
            new Image(target.Width, target.Height, pixels).WritePng(fileName);
            Log.Info($"RC dump wrote {fileName} ({target.Width}x{target.Height})");
        }
        catch (Exception exception)
        {
            Log.Info($"RC dump failed for {fileName}: {exception.Message}");
        }
    }

    private void ConfigureBenchmark()
    {
        _benchConfigured = true;
        if (float.TryParse(Environment.GetEnvironmentVariable("RC_BENCH_SCALE"), out var scale))
            _renderScale = Math.Clamp(scale, 0.05f, 1f);
        if (int.TryParse(Environment.GetEnvironmentVariable("RC_BENCH_RAYS"), out var rays))
            _baseRayCount = Math.Clamp(rays, 4, 16);
        if (int.TryParse(Environment.GetEnvironmentVariable("RC_BENCH_STEPS"), out var steps))
            _raymarchSteps = Math.Clamp(steps, 4, 24);
        if (int.TryParse(Environment.GetEnvironmentVariable("RC_BENCH_SMOOTH"), out var smooth))
            _giSmoothing = Math.Clamp(smooth, 0, 3);
        if (float.TryParse(Environment.GetEnvironmentVariable("RC_BENCH_OVERLAP"), out var overlap))
            _intervalOverlap = Math.Clamp(overlap, 0f, 0.2f);
        if (float.TryParse(Environment.GetEnvironmentVariable("RC_BENCH_FLOOR"), out var floor))
            _giFloor = Math.Clamp(floor, 0f, 0.1f);
        if (float.TryParse(Environment.GetEnvironmentVariable("RC_BENCH_MAXDIST"), out var maxDist))
            _maxRayDistance = Math.Clamp(maxDist, 4f, 80f);
        if (float.TryParse(Environment.GetEnvironmentVariable("RC_BENCH_GIINT"), out var giInt))
            _giIntensity = Math.Clamp(giInt, 0.1f, 3f);
        if (int.TryParse(Environment.GetEnvironmentVariable("RC_BENCH_LIGHTS"), out var lightTarget))
        {
            var rng = new Random(7);
            while (_lights.Count < lightTarget)
            {
                _lights.Add(new LightSource(
                    new Vector2(-14f + (float)rng.NextDouble() * 28f, -8f + (float)rng.NextDouble() * 16f),
                    new Vector3(0.4f + (float)rng.NextDouble() * 0.6f,
                                0.3f + (float)rng.NextDouble() * 0.7f,
                                0.4f + (float)rng.NextDouble() * 0.6f),
                    2f + (float)rng.NextDouble() * 6f,
                    0.2f + (float)rng.NextDouble() * 0.4f));
            }
        }
    }

    private void UpdateBenchmark()
    {
        _benchElapsed += _game.Time.Delta;
        if (_benchElapsed <= BenchWarmupSeconds)
            return;

        var fps = _game.RenderFramesPerSecond;
        if (fps > 0f)
        {
            _benchMin = Math.Min(_benchMin, fps);
            _benchMax = Math.Max(_benchMax, fps);
            _benchSum += fps;
            _benchCount++;
        }

        if (_benchElapsed < BenchWarmupSeconds + BenchSeconds)
            return;

        var avg = _benchCount > 0 ? _benchSum / _benchCount : 0.0;
        var outputPath = Environment.GetEnvironmentVariable("RC_BENCH_OUT") ?? "rc-bench.txt";
        File.WriteAllText(outputPath,
            $"RC_BENCH seconds={BenchSeconds} samples={_benchCount}\n" +
            $"avg={avg:F1} min={(_benchCount > 0 ? _benchMin : 0f):F1} max={_benchMax:F1}\n" +
            $"settings: renderScale={_renderScale} baseRays={_baseRayCount} steps={_raymarchSteps} " +
            $"cascades={(_autoCascadeCount ? CalculateCascadeCount(_game.Window.WidthInPixels, _game.Window.HeightInPixels) : _cascadeCount)} " +
            $"lights={_lights.Count} strokes={_emissionSegments.Count} window={_game.Window.WidthInPixels}x{_game.Window.HeightInPixels}\n");
        _game.Exit();
    }

    public void Render()
    {
        if (!_wasActive || _shader == null || _passBatcher == null || _strokeFieldShader == null ||
            _lightStampShader == null || _staticEmissionTarget == null || _staticDistanceTarget == null ||
            _dynamicEmissionTarget == null || _dynamicDistanceTarget == null)
            return;

        var windowWidth = Math.Max(_game.Window.WidthInPixels, 1);
        var windowHeight = Math.Max(_game.Window.HeightInPixels, 1);
        var width = Math.Max((int)MathF.Round(windowWidth * _renderScale), 1);
        var height = Math.Max((int)MathF.Round(windowHeight * _renderScale), 1);
        EnsureTargets(width, height);
        if (BenchSeconds > 0 && (_compositeTarget == null || _compositeTarget.Width != windowWidth || _compositeTarget.Height != windowHeight))
        {
            _compositeTarget?.Dispose();
            _compositeTarget = new Target(_game.GraphicsDevice, windowWidth, windowHeight, name: "Radiance Cascades composite");
        }
        if (_paintDirty)
            RebuildStaticTextures();
        RebuildDynamicTextures();

        if (_benchDumpPending)
        {
            _benchDumpPending = false;
            DumpTarget(_staticDistanceTarget, "rc-dump-static-distance.png");
            DumpTarget(_staticEmissionTarget, "rc-dump-static-emission.png");
            DumpTarget(_dynamicDistanceTarget, "rc-dump-dynamic-distance.png");
            DumpTarget(_dynamicEmissionTarget, "rc-dump-dynamic-emission.png");
        }
        var worldWidth = windowWidth / _camera.PPU;
        var worldHeight = windowHeight / _camera.PPU;
        var cascadeCount = _autoCascadeCount ? CalculateCascadeCount(width, height) : _cascadeCount;
        var selectedCascade = _cascadeIndex < 0 ? -1 : Math.Min(_cascadeIndex, cascadeCount - 1);
        var cascadeSampler = new TextureSampler(
            _linearFilter ? TextureFilter.Linear : TextureFilter.Nearest,
            TextureWrap.Clamp);
        // Slot 0 belongs to the previous cascade's texture (bound by the
        // batcher per draw); the emission/distance textures stay bound in
        // slots 1-4 for every cascade pass.
        _shader.Material.Fragment.Samplers[1] = new BoundSampler(_staticEmissionTarget.Attachments[0], cascadeSampler);
        _shader.Material.Fragment.Samplers[2] = new BoundSampler(_staticDistanceTarget.Attachments[0], cascadeSampler);
        _shader.Material.Fragment.Samplers[3] = new BoundSampler(_dynamicEmissionTarget.Attachments[0], cascadeSampler);
        _shader.Material.Fragment.Samplers[4] = new BoundSampler(_dynamicDistanceTarget.Attachments[0], cascadeSampler);
        var targets = new[] { _targetA!, _targetB! };
        var previous = -1;
        var output = 0;
        var selectedTexture = -1;

        // Radiance Cascades is a back-to-front chain of probe passes. Each pass
        // stores its directional groups in a regular texture, then the next
        // pass decodes those groups through the previous texture sampler.
        for (var cascadeIndex = cascadeCount - 1; cascadeIndex >= 0; cascadeIndex--)
        {
            var hasPrevious = previous >= 0;
            FillFragmentUniforms(worldWidth, worldHeight, width, cascadeCount, cascadeIndex);
            _shader.Material.Fragment.SetUniformBuffer(MemoryMarshal.AsBytes(_fragmentUniformVectors.AsSpan()));
            _shader.Material.Vertex.SetUniformBuffer(new VertexUniforms { CameraMatrix = Matrix4x4.Identity }, 1);

            _passBatcher.PushMaterial(_shader.Material);
            _passBatcher.PushSampler(cascadeSampler);
            _passBatcher.PushMatrix(Matrix3x2.Identity, relative: false);
            var sourceTexture = hasPrevious ? targets[previous].Attachments[0] : null;
            _passBatcher.Quad(sourceTexture,
                new Vector2(0f, 0f), new Vector2(width, 0f), new Vector2(width, height), new Vector2(0f, height),
                Vector2.Zero, Vector2.UnitX, Vector2.One, Vector2.UnitY, Color.White);
            _passBatcher.PopMatrix();
            _passBatcher.PopSampler();
            _passBatcher.PopMaterial();
            _passBatcher.Render(targets[output]);
            _passBatcher.Clear();

            if (cascadeIndex == selectedCascade)
                selectedTexture = output;
            previous = output;
            output = 1 - output;
        }

        var finalTexture = selectedTexture >= 0 ? targets[selectedTexture].Attachments[0] : targets[previous].Attachments[0];

        // Smooth the indirect term before it reaches the full-res composite:
        // H+V gaussian passes at cascade resolution, ping-ponging between the
        // two cascade targets (each iteration ends back in targets[previous]).
        if (selectedTexture < 0 && _giSmoothing > 0)
        {
            for (var i = 0; i < _giSmoothing; i++)
            {
                BlurPass(targets[previous], targets[output], 1f / width, 0f, cascadeSampler);
                BlurPass(targets[output], targets[previous], 0f, 1f / height, cascadeSampler);
            }
        }

        _batcher.PushMatrix(Matrix3x2.Identity, relative: false);
        if (selectedTexture >= 0 || _compositeShader == null)
        {
            // Debug cascade views blit the raw (linear radiance) probe texture.
            _batcher.ImageStretch(new Subtexture(finalTexture), new Rect(0f, 0f, windowWidth, windowHeight), Color.White);
        }
        else
        {
            // Final view: full-resolution composite — crisp display terms plus
            // the low-res indirect radiance, mirroring the game integration's
            // full-res scene / low-res lighting split.
            FillFragmentUniforms(worldWidth, worldHeight, width, cascadeCount, 0);
            // Repurpose slots the composite shader owns: Tuning.y carries the
            // GI intensity, Display.w the GI floor (probe overlay only exists
            // in the debug cascade views, which never run this pass).
            _fragmentUniformVectors[TuningUniformIndex].Y = _giIntensity;
            _fragmentUniformVectors[DisplayUniformIndex].W = _giFloor;
            _compositeShader.Material.Fragment.SetUniformBuffer(MemoryMarshal.AsBytes(_fragmentUniformVectors.AsSpan()));
            _compositeShader.Material.Fragment.Samplers[1] = new BoundSampler(_staticEmissionTarget!.Attachments[0], cascadeSampler);
            _compositeShader.Material.Fragment.Samplers[2] = new BoundSampler(_staticDistanceTarget!.Attachments[0], cascadeSampler);
            _compositeShader.Material.Fragment.Samplers[3] = new BoundSampler(_dynamicEmissionTarget!.Attachments[0], cascadeSampler);
            _batcher.PushMaterial(_compositeShader.Material);
            _batcher.PushSampler(cascadeSampler);
            _batcher.ImageStretch(new Subtexture(finalTexture), new Rect(0f, 0f, windowWidth, windowHeight), Color.White);
            _batcher.PopSampler();
            _batcher.PopMaterial();

            // Bench-only: re-render the composite into the dump target and
            // save it mid-benchmark (RC_BENCH_DUMP=1).
            if (BenchSeconds > 0 && _compositeTarget != null && !_compositeDumped &&
                Environment.GetEnvironmentVariable("RC_BENCH_DUMP") != null &&
                _benchElapsed > BenchWarmupSeconds + BenchSeconds * 0.5)
            {
                _compositeDumped = true;
                _passBatcher.PushMaterial(_compositeShader.Material);
                _passBatcher.PushSampler(cascadeSampler);
                _passBatcher.PushMatrix(Matrix3x2.Identity, relative: false);
                _passBatcher.ImageStretch(new Subtexture(finalTexture), new Rect(0f, 0f, windowWidth, windowHeight), Color.White);
                _passBatcher.PopMatrix();
                _passBatcher.PopSampler();
                _passBatcher.PopMaterial();
                _passBatcher.Render(_compositeTarget);
                _passBatcher.Clear();
                DumpTarget(_compositeTarget, "rc-dump-composite.png");
            }
        }
        DrawStrokePalette(windowHeight);
        _batcher.PopMatrix();
    }

    private void DrawStrokePalette(int viewportHeight)
    {
        for (var i = 0; i < StrokePalette.Length; i++)
        {
            var rect = GetStrokeSwatchRect(i, viewportHeight);
            _batcher.Rect(rect, StrokePalette[i].Swatch);
            _batcher.RectLine(rect, 1f, new Color(20, 24, 34, 255));
        }
        if ((uint)_selectedPaletteIndex < (uint)StrokePalette.Length)
        {
            var selected = GetStrokeSwatchRect(_selectedPaletteIndex, viewportHeight).Inflate(3f);
            _batcher.RectLine(selected, 2f, Color.White);
        }
    }

    private void EnsureTargets(int width, int height)
    {
        if (_targetA is not null && _targetB is not null && _targetWidth == width && _targetHeight == height)
            return;

        _targetA?.Dispose();
        _targetB?.Dispose();
        _targetA = new Target(_game.GraphicsDevice, width, height, name: "Radiance Cascades A");
        _targetB = new Target(_game.GraphicsDevice, width, height, name: "Radiance Cascades B");
        _targetWidth = width;
        _targetHeight = height;
    }

    private void MarkPaintDirty()
    {
        _paintDirty = true;
    }

    private void RebuildStaticTextures()
    {
        _paintDirty = false;
        var worldToTexture = Matrix3x2.CreateScale(PaintTextureWidth / PaintWorldWidth, PaintTextureHeight / PaintWorldHeight) *
            Matrix3x2.CreateTranslation(PaintTextureWidth * 0.5f, PaintTextureHeight * 0.5f);

        // Pass 1: stroke colors as round-capped lines (line + end circles)
        // into the sRGB emission texture, alpha = coverage.
        _staticEmissionTarget!.Clear(Color.Transparent);
        _passBatcher!.PushMatrix(worldToTexture, relative: false);
        foreach (var segment in _emissionSegments)
        {
            var color = LinearToSrgbColor(segment.Color);
            _passBatcher.Line(segment.Start, segment.End, segment.Radius * 2f, color);
            _passBatcher.Circle(segment.Start, segment.Radius, 12, color);
            _passBatcher.Circle(segment.End, segment.Radius, 12, color);
        }
        _passBatcher.Render(_staticEmissionTarget);
        _passBatcher.PopMatrix();
        _passBatcher.Clear();

        // Pass 2: merged distance field — the static scene primitives, drawn
        // occluders and stroke capsules all min-blend into one texture. All
        // batches share one submission per pass (min blending is
        // order-independent).
        _staticDistanceTarget!.Clear(Color.White);
        var minBlend = new BlendMode(BlendOp.Min, BlendFactor.One, BlendFactor.One);
        _passBatcher.PushMatrix(worldToTexture, relative: false);
        // The fixed scene (was the analytic SDF in the old shader):
        // two ledges, a pillar and a floating platform, plus a sphere.
        StampDistance(new Vector4(1.9f, 2.9f, 1.8f, 0.34f), 0.16f, 2, minBlend);
        StampDistance(new Vector4(7.2f, 2.9f, 0.34f, 2.3f), 0.16f, 2, minBlend);
        StampDistance(new Vector4(4.8f, -3.2f, 0f, 0f), 1.05f, 1, minBlend);
        StampDistance(new Vector4(-5.6f, 5.0f, 2.5f, 0.32f), 0.12f, 2, minBlend);
        for (var i = 0; i < _obstacleCount; i++)
            StampDistance(_obstacles[i], 0.11f, 0, minBlend);
        foreach (var segment in _emissionSegments)
            StampDistance(new Vector4(segment.Start.X, segment.Start.Y, segment.End.X, segment.End.Y), segment.Radius, 0, minBlend);
        _passBatcher.Render(_staticDistanceTarget);
        _passBatcher.PopMatrix();
        _passBatcher.Clear();
    }

    // Point lights are re-stamped every frame: dragging one costs two quads
    // (emission + distance disc) instead of a full bake.
    private void RebuildDynamicTextures()
    {
        var worldToTexture = Matrix3x2.CreateScale(DynamicTextureWidth / PaintWorldWidth, DynamicTextureHeight / PaintWorldHeight) *
            Matrix3x2.CreateTranslation(DynamicTextureWidth * 0.5f, DynamicTextureHeight * 0.5f);
        var addBlend = new BlendMode(BlendOp.Add, BlendFactor.One, BlendFactor.One);
        var minBlend = new BlendMode(BlendOp.Min, BlendFactor.One, BlendFactor.One);

        _dynamicEmissionTarget!.Clear(Color.Transparent);
        _passBatcher!.PushMatrix(worldToTexture, relative: false);
        foreach (var light in _lights)
        {
            if (!light.Enabled)
                continue;
            _lightStampShader!.Material.Fragment.SetUniformBuffer(new LightStampUniforms
            {
                Light = new Vector4(light.Position, light.Radius, light.Intensity),
                Color = new Vector4(light.Color, 1f),
            });
            var extent = light.Radius * 2.2f;
            var min = light.Position - new Vector2(extent);
            var max = light.Position + new Vector2(extent);
            _passBatcher.PushMaterial(_lightStampShader.Material);
            _passBatcher.PushBlend(addBlend);
            _passBatcher.Quad(
                null,
                min, new Vector2(max.X, min.Y), max, new Vector2(min.X, max.Y),
                min, new Vector2(max.X, min.Y), max, new Vector2(min.X, max.Y),
                Color.White);
            _passBatcher.PopBlend();
            _passBatcher.PopMaterial();
        }
        _passBatcher.Render(_dynamicEmissionTarget);
        _passBatcher.PopMatrix();
        _passBatcher.Clear();

        _dynamicDistanceTarget!.Clear(Color.White);
        _passBatcher.PushMatrix(worldToTexture, relative: false);
        foreach (var light in _lights)
        {
            if (!light.Enabled)
                continue;
            // The distance disc uses the emission stamp's solid core radius,
            // so rays terminate where emission is still at full strength.
            StampDistance(new Vector4(light.Position.X, light.Position.Y, 0f, 0f), Math.Max(light.Radius * 0.5f, 0.04f), 1, minBlend);
        }
        _passBatcher.Render(_dynamicDistanceTarget);
        _passBatcher.PopMatrix();
        _passBatcher.Clear();
    }

    // Submits one min-blended primitive quad into the current distance pass.
    // Quad TexCoords carry world positions; the shader's uniform must be set
    // before PushMaterial because pushing clones the material state. Type:
    // 0 capsule (Segment xy/zw endpoints), 1 circle (xy center), 2 round box
    // (xy center, zw half size); `radius` is the capsule/circle radius or the
    // box corner radius.
    private void StampDistance(Vector4 segment, float radius, int type, in BlendMode blend)
    {
        var extent = radius + StrokeDistanceHigh + 0.1f;
        Vector2 min, max;
        if (type == 0)
        {
            var a = new Vector2(segment.X, segment.Y);
            var b = new Vector2(segment.Z, segment.W);
            min = Vector2.Min(a, b) - new Vector2(extent);
            max = Vector2.Max(a, b) + new Vector2(extent);
        }
        else if (type == 1)
        {
            min = new Vector2(segment.X - extent, segment.Y - extent);
            max = new Vector2(segment.X + extent, segment.Y + extent);
        }
        else
        {
            min = new Vector2(segment.X - segment.Z - extent, segment.Y - segment.W - extent);
            max = new Vector2(segment.X + segment.Z + extent, segment.Y + segment.W + extent);
        }
        _strokeFieldShader!.Material.Fragment.SetUniformBuffer(new StrokeFieldUniforms
        {
            Segment = segment,
            Params = new Vector4(radius, StrokeDistanceHigh - StrokeDistanceLow, type, StrokeDistanceLow),
        });
        _passBatcher!.PushMaterial(_strokeFieldShader.Material);
        _passBatcher.PushBlend(blend);
        _passBatcher.Quad(
            null,
            min, new Vector2(max.X, min.Y), max, new Vector2(min.X, max.Y),
            min, new Vector2(max.X, min.Y), max, new Vector2(min.X, max.Y),
            Color.White);
        _passBatcher.PopBlend();
        _passBatcher.PopMaterial();
    }

    private static Color LinearToSrgbColor(Vector3 linear) => new(
        (byte)Math.Clamp(MathF.Round(MathF.Pow(linear.X, 1f / 2.2f) * 255f), 0f, 255f),
        (byte)Math.Clamp(MathF.Round(MathF.Pow(linear.Y, 1f / 2.2f) * 255f), 0f, 255f),
        (byte)Math.Clamp(MathF.Round(MathF.Pow(linear.Z, 1f / 2.2f) * 255f), 0f, 255f),
        255);

    private void HandleCanvasInput()
    {
        if (ImGui.GetIO().WantCaptureMouse)
            return;

        // Number keys swap the stroke color even mid-stroke, matching the
        // reference sandbox's palette interaction.
        if (!ImGui.GetIO().WantCaptureKeyboard)
        {
            for (var i = 0; i < StrokePalette.Length; i++)
            {
                if (_input.Keyboard.Pressed(StrokePaletteHotkey(i)))
                    SelectStrokePalette(i);
            }
        }

        if (_input.Mouse.LeftPressed && !_input.Keyboard.Shift &&
            PickStrokePalette(_input.Mouse.Position, out var paletteIndex))
        {
            // Clicking a swatch picks its color instead of starting a stroke.
            SelectStrokePalette(paletteIndex);
            _draggingLight = false;
            _drawingEmission = false;
            return;
        }

        if (!_camera.TryScreenToWorld(_input.Mouse.Position, out var world))
            return;

        // Right-drag sweeps away nearby emissive strokes, mirroring the
        // reference sandbox's transparent eraser swatch.
        if (_input.Mouse.RightDown)
        {
            EraseEmissionsNear(world);
            _draggingLight = false;
            _drawingEmission = false;
            return;
        }

        if (_input.Mouse.LeftPressed)
        {
            _draggingLight = false;
            _drawingEmission = false;
            _lastDrawPoint = world;

            if (_input.Keyboard.Shift)
            {
                // Shift remains the explicit occluder/obstacle drawing mode.
                return;
            }

            var lightIndex = FindLight(world, 0.5f);
            _draggingLight = lightIndex >= 0;
            if (_draggingLight)
            {
                _selectedLight = lightIndex;
            }
            else
            {
                // Empty-canvas left-drag is a freehand emissive stroke, matching
                // the reference sandbox. No modifier key is required.
                _drawingEmission = true;
            }
        }

        if (!_input.Mouse.LeftDown)
        {
            _draggingLight = false;
            _drawingEmission = false;
            return;
        }

        if (_drawingEmission)
        {
            if (Vector2.DistanceSquared(world, _lastDrawPoint) > 0.0064f)
            {
                AddEmissionSegment(_lastDrawPoint, world);
                _lastDrawPoint = world;
            }
        }
        else if (_draggingLight)
        {
            if ((uint)_selectedLight < (uint)_lights.Count)
                _lights[_selectedLight].Position = Vector2.Clamp(world, new Vector2(-18f, -10f), new Vector2(18f, 10f));
        }
        else if (_input.Keyboard.Shift && Vector2.DistanceSquared(world, _lastDrawPoint) > 0.04f)
        {
            AddObstacle(_lastDrawPoint, world);
            _lastDrawPoint = world;
        }
    }

    private void SelectStrokePalette(int index)
    {
        _selectedPaletteIndex = index;
        _emissionColor = StrokePalette[index].Emission;
    }

    private static Keys StrokePaletteHotkey(int index) => Keys.D1 + index;

    private bool PickStrokePalette(Vector2 mousePosition, out int index)
    {
        var viewportHeight = Math.Max(_game.Window.HeightInPixels, 1);
        for (var i = 0; i < StrokePalette.Length; i++)
        {
            if (GetStrokeSwatchRect(i, viewportHeight).Inflate(4f).Contains(mousePosition))
            {
                index = i;
                return true;
            }
        }
        index = -1;
        return false;
    }

    private static Rect GetStrokeSwatchRect(int index, int viewportHeight)
    {
        const float size = 30f;
        const float gap = 8f;
        const float margin = 14f;
        var totalHeight = StrokePalette.Length * size + (StrokePalette.Length - 1) * gap;
        var y = (viewportHeight - totalHeight) * 0.5f + index * (size + gap);
        return new Rect(margin, y, size, size);
    }

    private void EraseEmissionsNear(Vector2 world)
    {
        for (var i = _emissionSegments.Count - 1; i >= 0; i--)
        {
            var segment = _emissionSegments[i];
            var ab = segment.End - segment.Start;
            var t = Math.Clamp(
                Vector2.Dot(world - segment.Start, ab) / Math.Max(ab.LengthSquared(), 0.0001f), 0f, 1f);
            var distance = Vector2.Distance(world, segment.Start + ab * t);
            if (distance < Math.Max(segment.Radius + 0.25f, 0.45f))
            {
                _emissionSegments.RemoveAt(i);
                MarkPaintDirty();
            }
        }
    }

    private void AddEmissionSegment(Vector2 start, Vector2 end)
    {
        if (Vector2.DistanceSquared(start, end) < 0.0004f)
            return;

        // Coalesce nearly-collinear samples so a long freehand stroke remains
        // cheap for the GPU while corners stay independently editable. Only
        // segments painted with the same brush (color/intensity/radius) merge,
        // so switching palette colors mid-stroke keeps both colors visible.
        if (_emissionSegments.Count > 0)
        {
            var previous = _emissionSegments[^1];
            var incoming = end - start;
            var existing = previous.End - previous.Start;
            var incomingLength = incoming.Length();
            var existingLength = existing.Length();
            var sameBrush = previous.Color == _emissionColor &&
                            previous.Intensity == _emissionIntensity &&
                            previous.Radius == _emissionRadius;
            if (sameBrush &&
                Vector2.DistanceSquared(previous.End, start) < 0.04f &&
                incomingLength > 0.001f && existingLength > 0.001f &&
                Vector2.Dot(Vector2.Normalize(existing), Vector2.Normalize(incoming)) > 0.985f)
            {
                previous.End = end;
                MarkPaintDirty();
                return;
            }
        }
        _emissionSegments.Add(new EmissionSegment(start, end, _emissionColor, _emissionIntensity, _emissionRadius));
        MarkPaintDirty();
    }

    private void AddObstacle(Vector2 a, Vector2 b)
    {
        if (_obstacleCount >= MaxObstacles)
            return;
        _obstacles[_obstacleCount++] = new Vector4(a.X, a.Y, b.X, b.Y);
        MarkPaintDirty();
    }

    private void DrawControls()
    {
        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(viewport.WorkPos + new Vector2(viewport.WorkSize.X - 360f, 16f), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(344f, 0f), ImGuiCond.FirstUseEver);
        ImGui.Begin("Radiance Cascades 2D");
        ImGui.TextUnformatted("Noiseless 2D global illumination");
        ImGui.TextUnformatted("LMB draws light strokes; pick colors on the left");
        ImGui.TextUnformatted("palette or press 1-6, even mid-stroke. RMB erases.");
        ImGui.TextUnformatted("Drag a light to move it. Shift + LMB draws occluders.");
        ImGui.Separator();
        if (ImGui.Button("Reset"))
            ResetScene();
        ImGui.SameLine();
        if (ImGui.Button("Clear drawn lines"))
        {
            Array.Clear(_obstacles);
            _obstacleCount = 0;
            MarkPaintDirty();
        }
        ImGui.SameLine();
        if (ImGui.Button("Clear light strokes"))
        {
            _emissionSegments.Clear();
            MarkPaintDirty();
        }
        if (ImGui.BeginCombo("Base Ray Count", _baseRayCount.ToString()))
        {
            foreach (var value in SupportedBaseRayCounts)
            {
                if (ImGui.Selectable(value.ToString(), value == _baseRayCount))
                    _baseRayCount = value;
            }
            ImGui.EndCombo();
        }
        ImGui.Checkbox("Auto Cascade Count", ref _autoCascadeCount);
        if (_autoCascadeCount)
            ImGui.Text($"Cascade Count: {CalculateCascadeCount(_game.Window.WidthInPixels, _game.Window.HeightInPixels)}");
        else
            ImGui.SliderInt("Cascade Count", ref _cascadeCount, 1, MaxCascadeCount);
        ImGui.SliderInt("Cascade Index", ref _cascadeIndex, -1, MaxCascadeCount - 1);
        ImGui.SliderFloat("Interval Length", ref _intervalLength, 0.5f, 8f, "%.2f px");
        ImGui.SliderFloat("Interval Overlap", ref _intervalOverlap, 0f, 0.2f, "%.3f");
        ImGui.SliderFloat("Max Ray Distance", ref _maxRayDistance, 4f, 80f, "%.1f");
        ImGui.SliderFloat("Render Scale", ref _renderScale, 0.125f, 1f, "%.3f");
        ImGui.SliderInt("Raymarch Steps", ref _raymarchSteps, 4, 24);
        ImGui.SliderInt("GI Smoothing", ref _giSmoothing, 0, 3);
        ImGui.SliderFloat("GI Intensity", ref _giIntensity, 0.1f, 3f, "%.2f");
        ImGui.SliderFloat("GI Floor", ref _giFloor, 0f, 0.1f, "%.3f");
        ImGui.Checkbox("Use Linear Filter", ref _linearFilter);
        ImGui.Checkbox("Correct sRGB", ref _correctSrgb);
        ImGui.Checkbox("Show Probes", ref _showProbes);
        ImGui.Checkbox("Show SDF scene", ref _showScene);
        ImGui.Separator();
        ImGui.Text($"Light strokes: {_emissionSegments.Count} (distance-field texture)");
        if (ImGui.ColorEdit3("Stroke color", ref _emissionColor))
            _selectedPaletteIndex = -1;
        ImGui.SliderFloat("Stroke intensity", ref _emissionIntensity, 0.1f, 30f, "%.2f");
        ImGui.SliderFloat("Stroke radius", ref _emissionRadius, 0.02f, 0.8f, "%.2f");
        ImGui.Separator();
        if (ImGui.Button("Add light"))
        {
            var index = _lights.Count;
            _lights.Add(new LightSource(
                new Vector2(-7f + index * 1.8f, 5f - index * 0.7f),
                new Vector3(0.55f + 0.45f * ((index + 1) % 2), 0.35f + 0.2f * (index % 3), 1f),
                3.5f,
                0.28f));
            _selectedLight = index;
        }
        ImGui.SameLine();
        if (ImGui.Button("Remove selected") && _lights.Count > 1)
        {
            _lights.RemoveAt(Math.Clamp(_selectedLight, 0, _lights.Count - 1));
            _selectedLight = Math.Clamp(_selectedLight, 0, _lights.Count - 1);
        }
        ImGui.Text($"Lights: {_lights.Count} (distance-field texture)");
        for (var i = 0; i < _lights.Count; i++)
        {
            var label = $"Light {i + 1}{(_lights[i].Enabled ? "" : " (off)")}";
            if (ImGui.Selectable(label, i == _selectedLight))
                _selectedLight = i;
        }
        if ((uint)_selectedLight < (uint)_lights.Count)
        {
            var selected = _lights[_selectedLight];
            ImGui.Checkbox("Enabled", ref selected.Enabled);
            ImGui.SliderFloat("Light intensity", ref selected.Intensity, 0.1f, 20f, "%.2f");
            ImGui.SliderFloat("Light radius", ref selected.Radius, 0.05f, 2f, "%.2f");
            ImGui.ColorEdit3("Light color", ref selected.Color);
        }
        ImGui.ColorEdit3("World color", ref _worldColor);
        ImGui.Text($"Drawn occluders: {_obstacleCount}/{MaxObstacles}");
        ImGui.Text($"Render FPS: {_game.RenderFramesPerSecond:F1}");
        ImGui.End();
    }

    private void ResetScene()
    {
        _worldColor = new Vector3(0.06f, 0.09f, 0.14f);
        _maxRayDistance = 55f;
        _intervalLength = 2f;
        _intervalOverlap = 0.04f;
        _renderScale = 0.2f;
        _raymarchSteps = 10;
        _giSmoothing = 2;
        _giIntensity = 0.7f;
        _giFloor = 0.02f;
        _baseRayCount = 16;
        _cascadeCount = 4;
        _autoCascadeCount = true;
        _cascadeIndex = -1;
        _linearFilter = true;
        _correctSrgb = true;
        _showProbes = false;
        _showScene = true;
        _emissionColor = StrokePalette[1].Emission;
        _selectedPaletteIndex = 1;
        _emissionIntensity = 4f;
        _emissionRadius = 0.16f;
        _emissionSegments.Clear();
        _lights.Clear();
        _lights.Add(new LightSource(new Vector2(-5f, -2f), new Vector3(1f, 0.55f, 0.18f), 5f, 0.34f));
        _lights.Add(new LightSource(new Vector2(7f, 4.6f), new Vector3(0.22f, 0.62f, 1f), 3.2f, 0.3f));
        _lights.Add(new LightSource(new Vector2(-8.2f, -6.1f), new Vector3(0.28f, 1f, 0.56f), 2.6f, 0.26f));
        _selectedLight = 0;
        Array.Clear(_obstacles);
        _obstacleCount = 0;
        MarkPaintDirty();
        AddObstacle(new Vector2(-1.0f, -4.2f), new Vector2(-1.0f, 1.8f));
        AddObstacle(new Vector2(3.0f, -0.8f), new Vector2(8.0f, -0.8f));
        AddObstacle(new Vector2(-8.0f, 3.2f), new Vector2(-3.0f, 3.2f));
    }

    private int CalculateCascadeCount(int width, int height)
    {
        var diagonal = Math.Sqrt((double)width * width + (double)height * height);
        // Four cascades keep the default interactive preset responsive. The
        // manual cascade slider still exposes the full supported range.
        return Math.Clamp((int)Math.Ceiling(Math.Log(Math.Max(diagonal, 1d), _baseRayCount)) + 1, 1, 4);
    }

    private int FindLight(Vector2 position, float radius)
    {
        var best = -1;
        var bestDistance = radius * radius;
        for (var i = 0; i < _lights.Count; i++)
        {
            if (!_lights[i].Enabled)
                continue;
            var distance = Vector2.DistanceSquared(position, _lights[i].Position);
            if (distance <= bestDistance)
            {
                best = i;
                bestDistance = distance;
            }
        }
        return best;
    }

    private void FillFragmentUniforms(float worldWidth, float worldHeight, int width, int cascadeCount, int cascadeIndex)
    {
        Array.Clear(_fragmentUniformVectors);

        _fragmentUniformVectors[ViewportUniformIndex] = new Vector4(worldWidth, worldHeight, width, _showScene ? 1f : 0f);
        _fragmentUniformVectors[CascadeUniformIndex] = new Vector4(_baseRayCount, cascadeCount, _intervalLength, _maxRayDistance);
        _fragmentUniformVectors[DisplayUniformIndex] = new Vector4(cascadeIndex, _linearFilter ? 1f : 0f, _correctSrgb ? 1f : 0f, _showProbes ? 1f : 0f);
        _fragmentUniformVectors[WorldUniformIndex] = new Vector4(_worldColor, 0f);
        _fragmentUniformVectors[TuningUniformIndex] = new Vector4(
            _intervalOverlap,
            0f,
            _emissionIntensity,
            _raymarchSteps);
        _fragmentUniformVectors[PaintUniformIndex] = new Vector4(
            PaintWorldWidth,
            PaintWorldHeight,
            StrokeDistanceHigh - StrokeDistanceLow,
            StrokeDistanceLow);
    }

    private static Matrix4x4 ToMatrix4x4(in Matrix3x2 matrix) => new(
        matrix.M11, matrix.M12, 0f, 0f,
        matrix.M21, matrix.M22, 0f, 0f,
        0f, 0f, 1f, 0f,
        matrix.M31, matrix.M32, 0f, 1f);
}
