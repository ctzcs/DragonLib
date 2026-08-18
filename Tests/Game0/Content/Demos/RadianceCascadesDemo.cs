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
/// filtering steps while raymarching a self-contained scene SDF.
/// </summary>
public sealed class RadianceCascadesDemoSystem : IEcsInit, IEcsDestroy, IUpdateSystem, IRenderSystem
{
    private const string ShaderResourceBase = "Game0/Shaders/RadianceCascades";
    private const int MaxObstacles = 8;
    private const int MaxCascadeCount = 24;
    private static readonly int[] SupportedBaseRayCounts = [4, 16];

    private const int ViewportUniformIndex = 0;
    private const int CascadeUniformIndex = ViewportUniformIndex + 1;
    private const int DisplayUniformIndex = CascadeUniformIndex + 1;
    private const int WorldUniformIndex = DisplayUniformIndex + 1;
    private const int TuningUniformIndex = WorldUniformIndex + 1;
    private const int ObstacleUniformIndex = TuningUniformIndex + 1;
    private const int UniformVectorCount = ObstacleUniformIndex + MaxObstacles;

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
    private struct LightGpuData
    {
        public Vector4 Position;
        public Vector4 Color;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct EmissionGpuData
    {
        public Vector4 Segment;
        public Vector4 Color;
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
    private Batcher? _passBatcher;
    private StorageBuffer<LightGpuData>? _lightBuffer;
    private StorageBuffer<EmissionGpuData>? _emissionBuffer;
    private LightGpuData[] _lightUpload = [];
    private EmissionGpuData[] _emissionUpload = [];
    private Target? _targetA;
    private Target? _targetB;
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
    private Vector3 _emissionColor = new(1f, 0.35f, 0.08f);
    private float _emissionIntensity = 4f;
    private float _emissionRadius = 0.16f;

    private Vector3 _worldColor = new(0.06f, 0.09f, 0.14f);
    private float _maxRayDistance = 55f;
    private float _intervalLength = 2f;
    private float _intervalOverlap = 0.04f;
    private float _renderScale = 0.2f;
    private int _raymarchSteps = 10;
    private int _baseRayCount = 4;
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
            new ShaderStageSpec(1, 1, "fragment_main", 4),
            new ShaderStageSpec(0, 2, "vertex_main"));
        _passBatcher = new Batcher(_game.GraphicsDevice);
        _lightBuffer = new StorageBuffer<LightGpuData>(_game.GraphicsDevice, "Radiance Cascades lights");
        _emissionBuffer = new StorageBuffer<EmissionGpuData>(_game.GraphicsDevice, "Radiance Cascades emission");
    }

    public void Destroy()
    {
        _shader?.Dispose();
        _shader = null;
        _lightBuffer?.Dispose();
        _emissionBuffer?.Dispose();
        _lightBuffer = null;
        _emissionBuffer = null;
        _targetA?.Dispose();
        _targetB?.Dispose();
        _passBatcher?.Dispose();
        _targetA = null;
        _targetB = null;
        _passBatcher = null;
    }

    public void Update()
    {
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

        DrawControls();
        HandleCanvasInput();
    }

    public void Render()
    {
        if (!_wasActive || _shader == null || _passBatcher == null || _lightBuffer == null || _emissionBuffer == null)
            return;

        var windowWidth = Math.Max(_game.Window.WidthInPixels, 1);
        var windowHeight = Math.Max(_game.Window.HeightInPixels, 1);
        var width = Math.Max((int)MathF.Round(windowWidth * _renderScale), 1);
        var height = Math.Max((int)MathF.Round(windowHeight * _renderScale), 1);
        EnsureTargets(width, height);
        var worldWidth = windowWidth / _camera.PPU;
        var worldHeight = windowHeight / _camera.PPU;
        var cascadeCount = _autoCascadeCount ? CalculateCascadeCount(width, height) : _cascadeCount;
        var selectedCascade = _cascadeIndex < 0 ? -1 : Math.Min(_cascadeIndex, cascadeCount - 1);
        UploadGpuBuffers();
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
            // SDL's graphics storage buffers are bound by register slot. The
            // shader uses t2/t3, so slots 0/1 need valid harmless buffers too.
            _passBatcher.FragmentStorageBuffers.Add(_lightBuffer);
            _passBatcher.FragmentStorageBuffers.Add(_lightBuffer);
            _passBatcher.FragmentStorageBuffers.Add(_lightBuffer);
            _passBatcher.FragmentStorageBuffers.Add(_emissionBuffer);
            _passBatcher.PushSampler(new TextureSampler(
                _linearFilter ? TextureFilter.Linear : TextureFilter.Nearest,
                TextureWrap.Clamp));
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
        _batcher.PushMatrix(Matrix3x2.Identity, relative: false);
        _batcher.ImageStretch(new Subtexture(finalTexture), new Rect(0f, 0f, windowWidth, windowHeight), Color.White);
        _batcher.PopMatrix();
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

    private void HandleCanvasInput()
    {
        if (ImGui.GetIO().WantCaptureMouse || !_camera.TryScreenToWorld(_input.Mouse.Position, out var world))
            return;

        var drawEmission = _input.Keyboard.Ctrl;
        if (_input.Mouse.LeftPressed)
        {
            if (drawEmission)
            {
                _drawingEmission = true;
                _draggingLight = false;
                _lastDrawPoint = world;
                AddEmissionSegment(world, world + new Vector2(0.015f, 0f));
                return;
            }

            var lightIndex = FindLight(world, 1.1f);
            _draggingLight = lightIndex >= 0;
            if (_draggingLight)
                _selectedLight = lightIndex;
            _lastDrawPoint = world;
            if (!_draggingLight && _input.Keyboard.Shift)
                AddObstacle(_lastDrawPoint, _lastDrawPoint + new Vector2(0.01f, 0f));
        }

        if (!_input.Mouse.LeftDown)
        {
            _draggingLight = false;
            _drawingEmission = false;
            return;
        }

        if (_drawingEmission)
        {
            if (Vector2.DistanceSquared(world, _lastDrawPoint) > 0.01f)
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

    private void AddEmissionSegment(Vector2 start, Vector2 end)
    {
        _emissionSegments.Add(new EmissionSegment(start, end, _emissionColor, _emissionIntensity, _emissionRadius));
    }

    private void AddObstacle(Vector2 a, Vector2 b)
    {
        if (_obstacleCount >= MaxObstacles)
            return;
        _obstacles[_obstacleCount++] = new Vector4(a.X, a.Y, b.X, b.Y);
    }

    private void DrawControls()
    {
        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(viewport.WorkPos + new Vector2(viewport.WorkSize.X - 360f, 16f), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(344f, 0f), ImGuiCond.FirstUseEver);
        ImGui.Begin("Radiance Cascades 2D");
        ImGui.TextUnformatted("Noiseless 2D global illumination");
        ImGui.TextUnformatted("Drag a point light. Ctrl + LMB draws emissive strokes.");
        ImGui.TextUnformatted("Shift + LMB draws occluders.");
        ImGui.Separator();
        if (ImGui.Button("Reset"))
            ResetScene();
        ImGui.SameLine();
        if (ImGui.Button("Clear drawn lines"))
        {
            Array.Clear(_obstacles);
            _obstacleCount = 0;
        }
        ImGui.SameLine();
        if (ImGui.Button("Clear light strokes"))
            _emissionSegments.Clear();
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
        ImGui.Checkbox("Use Linear Filter", ref _linearFilter);
        ImGui.Checkbox("Correct sRGB", ref _correctSrgb);
        ImGui.Checkbox("Show Probes", ref _showProbes);
        ImGui.Checkbox("Show SDF scene", ref _showScene);
        ImGui.Separator();
        ImGui.Text($"Light strokes: {_emissionSegments.Count} (GPU storage buffer)");
        ImGui.ColorEdit3("Stroke color", ref _emissionColor);
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
        ImGui.Text($"Lights: {_lights.Count} (GPU storage buffer)");
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
        _baseRayCount = 4;
        _cascadeCount = 4;
        _autoCascadeCount = true;
        _cascadeIndex = -1;
        _linearFilter = true;
        _correctSrgb = true;
        _showProbes = false;
        _showScene = true;
        _emissionColor = new Vector3(1f, 0.35f, 0.08f);
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
        _fragmentUniformVectors[WorldUniformIndex] = new Vector4(_worldColor, _obstacleCount);
        _fragmentUniformVectors[TuningUniformIndex] = new Vector4(
            _intervalOverlap,
            _lights.Count,
            _emissionSegments.Count,
            _raymarchSteps);
        for (var i = 0; i < MaxObstacles; i++)
            _fragmentUniformVectors[ObstacleUniformIndex + i] = _obstacles[i];
    }

    private void UploadGpuBuffers()
    {
        if (_lightBuffer == null || _emissionBuffer == null)
            return;

        var lightCount = Math.Max(_lights.Count, 1);
        if (_lightUpload.Length < lightCount)
            _lightUpload = new LightGpuData[lightCount];
        for (var i = 0; i < _lights.Count; i++)
        {
            var light = _lights[i];
            _lightUpload[i] = new LightGpuData
            {
                Position = new Vector4(light.Position, light.Intensity, light.Radius),
                Color = new Vector4(light.Color, light.Enabled ? 1f : 0f),
            };
        }
        _lightBuffer.Upload(_lightUpload.AsSpan(0, lightCount));

        var emissionCount = Math.Max(_emissionSegments.Count, 1);
        if (_emissionUpload.Length < emissionCount)
            _emissionUpload = new EmissionGpuData[emissionCount];
        for (var i = 0; i < _emissionSegments.Count; i++)
        {
            var segment = _emissionSegments[i];
            _emissionUpload[i] = new EmissionGpuData
            {
                Segment = new Vector4(segment.Start.X, segment.Start.Y, segment.End.X, segment.End.Y),
                Color = new Vector4(segment.Color * segment.Intensity, Math.Max(segment.Radius, 0.01f)),
            };
        }
        _emissionBuffer.Upload(_emissionUpload.AsSpan(0, emissionCount));
    }

    private static Matrix4x4 ToMatrix4x4(in Matrix3x2 matrix) => new(
        matrix.M11, matrix.M12, 0f, 0f,
        matrix.M21, matrix.M22, 0f, 0f,
        0f, 0f, 1f, 0f,
        matrix.M31, matrix.M32, 0f, 1f);
}
