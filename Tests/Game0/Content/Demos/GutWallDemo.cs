using System.Numerics;
using System.Runtime.InteropServices;
using DCFApixels.DragonECS;
using Engine.ECS;
using Engine.Rendering;
using Engine.World;
using Foster.Framework;
using ImGuiNET;

namespace Game0.Content.Demos;

public sealed class GutWallDemoSystem : IEcsDestroy, IUpdateSystem, IRenderSystem
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Uniforms
    {
        public Vector4 Light, LightColor, Surface, Options;
    }

    [DI] private SceneRouter<RuntimeScene> _sceneRouter = null!;
    [DI] private Batcher _batcher = null!;
    [DI] private Camera2D _camera = null!;
    [DI] private Input _input = null!;
    [DI] private MyGame _game = null!;

    private EmbeddedShaderMaterial? _shader;
    private Texture? _albedo, _normal, _material;
    private GutFoldTexture.Maps? _maps;
    private Task<GutFoldTexture.Maps>? _bakeTask;
    private readonly CancellationTokenSource _bakeCancellation = new();
    private (Vector2 Position, float Zoom, float Rotation, float Ppu) _savedCamera;
    private bool _wasActive, _followMouse = true, _orbit, _fill = true, _marker = true;
    private float _elapsed, _spacing = .28f, _bend = 1.1f, _grooveWidth = .48f, _detail = .65f;
    private float _normalStrength = .85f, _wetness = .55f, _ambient = .38f;
    private float _lightHeight = 2f, _lightIntensity = 5f, _lightRadius = 15f;
    private int _seed = 42, _mode;
    private Vector2 _lightPosition = new(-5f, 0f);
    private Vector3 _lightColor = new(.025f, .95f, .85f);
    private string _exportStatus = "";
    // Optional deterministic GPU verification: capture all map views and two light positions, then exit.
    private readonly string? _captureDirectory = Environment.GetEnvironmentVariable("GUT_DEMO_CAPTURE");
    private int _captureFrame;
    private bool _captureDone;

    public void Destroy()
    {
        _bakeCancellation.Cancel();
        _bakeCancellation.Dispose();
        _shader?.Dispose();
        _albedo?.Dispose();
        _normal?.Dispose();
        _material?.Dispose();
    }

    public void Update()
    {
        if (_captureDone) { _game.Exit(); return; }
        if (_captureDirectory != null && !_wasActive && _sceneRouter.Current == RuntimeScene.Main)
            _sceneRouter.SwitchTo(RuntimeScene.GutWall);
        bool active = _sceneRouter.Current == RuntimeScene.GutWall;
        if (active && !_wasActive)
        {
            _savedCamera = (_camera.Position, _camera.Zoom, _camera.Rotation, _camera.PPU);
            _camera.Position = Vector2.Zero; _camera.Zoom = 1f; _camera.Rotation = 0f;
            _shader ??= EmbeddedShaderMaterial.Load(_game.GraphicsDevice, typeof(GutWallDemoSystem).Assembly,
                "Game0/Shaders/GutWall", new ShaderStageSpec(3, 1, "fragment_main"),
                new ShaderStageSpec(0, 2, "vertex_main"));
            if (_maps == null && _bakeTask == null) Regenerate();
        }
        else if (!active && _wasActive)
        {
            _camera.Position = _savedCamera.Position; _camera.Zoom = _savedCamera.Zoom;
            _camera.Rotation = _savedCamera.Rotation; _camera.PPU = _savedCamera.Ppu;
        }
        _wasActive = active;
        if (!active) return;
        FinishBake();
        _camera.PPU = .94f * MathF.Min(_game.Window.WidthInPixels / GutFoldTexture.WorldWidth,
            _game.Window.HeightInPixels / GutFoldTexture.WorldHeight);
        _elapsed += _game.Time.Delta;
        DrawControls();
        if (_captureDirectory != null) return;
        if (_orbit) _lightPosition = new(-3f + 6f * MathF.Cos(_elapsed * .5f), 4f * MathF.Sin(_elapsed * .5f));
        else if (_followMouse && !ImGui.GetIO().WantCaptureMouse
            && _camera.TryScreenToWorld(_input.Mouse.Position, out var mouse)) _lightPosition = mouse;
    }

    private void Regenerate()
    {
        if (_bakeTask != null) return;
        int seed = _seed;
        float spacing = _spacing, bend = _bend, grooveWidth = _grooveWidth, detail = _detail;
        _exportStatus = "";
        // Only array generation runs on the worker. GPU upload stays on the game thread.
        var cancellation = _bakeCancellation.Token;
        _bakeTask = Task.Run(() => GutFoldTexture.Generate(seed, spacing, bend, grooveWidth, detail, cancellation), cancellation);
    }

    private void FinishBake()
    {
        if (_bakeTask == null || !_bakeTask.IsCompleted) return;
        var completed = _bakeTask;
        _bakeTask = null;
        if (!completed.IsCompletedSuccessfully)
        {
            if (_captureDirectory != null)
                throw new InvalidOperationException("Gut wall capture bake failed.", completed.Exception);
            _exportStatus = "Bake failed: " + completed.Exception?.GetBaseException().Message;
            return;
        }
        _maps = completed.Result;
        _albedo ??= new Texture(_game.GraphicsDevice, GutFoldTexture.Width, GutFoldTexture.Height, name: "Gut albedo");
        _normal ??= new Texture(_game.GraphicsDevice, GutFoldTexture.Width, GutFoldTexture.Height, name: "Gut normal");
        _material ??= new Texture(_game.GraphicsDevice, GutFoldTexture.Width, GutFoldTexture.Height, name: "Gut material");
        _albedo.SetData<Color>(_maps.Albedo);
        _normal.SetData<Color>(_maps.Normal);
        _material.SetData<Color>(_maps.Material);
        Log.Info($"Gut wall: baked continuous folds in {_maps.BakeMilliseconds:F0} ms.");
    }

    public void Render()
    {
        if (!_wasActive || _shader == null || _albedo == null) return;
        ConfigureMaterial(_mode, _lightPosition);
        DrawQuad(_batcher, ToMatrix4x4(_camera.Matrix));
        if (_captureDirectory != null && ++_captureFrame == 3)
        {
            ExportMaps(_captureDirectory);
            using var target = new Target(_game.GraphicsDevice, GutFoldTexture.Width, GutFoldTexture.Height);
            using var captureBatcher = new Batcher(_game.GraphicsDevice);
            var projection = Matrix4x4.CreateOrthographicOffCenter(0, target.Width, target.Height, 0, 0, 1);
            var camera = ToMatrix4x4(Matrix3x2.CreateTranslation(14f, 8f) * Matrix3x2.CreateScale(50f)) * projection;
            Capture("lit-left", 0, new(-5f, 0f));
            Capture("lit-right", 0, new(7f, 2f));
            float savedNormalStrength = _normalStrength, savedWetness = _wetness;
            _normalStrength = 0f;
            Capture("lit-flat", 0, new(-5f, 0f));
            _normalStrength = savedNormalStrength;
            _wetness = 0f;
            Capture("lit-dry", 0, new(-5f, 0f));
            _wetness = savedWetness;
            Capture("albedo", 1, _lightPosition);
            Capture("height", 2, _lightPosition);
            Capture("normal", 3, _lightPosition);
            _captureDone = true;

            void Capture(string name, int mode, Vector2 position)
            {
                ConfigureMaterial(mode, position);
                DrawQuad(captureBatcher, camera);
                captureBatcher.Render(target, Matrix4x4.Identity);
                captureBatcher.Clear();
                var pixels = new Color[target.Width * target.Height];
                target.Attachments[0].GetData<Color>(pixels);
                new Image(target.Width, target.Height, pixels).WritePng(Path.Combine(_captureDirectory, name + ".png"));
            }
        }
    }

    private void ConfigureMaterial(int mode, Vector2 position)
    {
        var sampler = new TextureSampler(TextureFilter.Linear, TextureWrap.Clamp, TextureWrap.Clamp);
        _shader!.Material.Fragment.Samplers[1] = new(_normal, sampler);
        _shader.Material.Fragment.Samplers[2] = new(_material, sampler);
        _shader.Material.Fragment.SetUniformBuffer(new Uniforms
        {
            Light = new(position, _lightHeight, _lightIntensity), LightColor = new(_lightColor, 1f),
            Surface = new(_normalStrength, _wetness, _ambient, mode),
            Options = new(_lightRadius, _fill ? 1f : 0f, _marker ? 1f : 0f, 0f),
        });
    }

    private void DrawQuad(Batcher batcher, Matrix4x4 camera)
    {
        _shader!.Material.Vertex.SetUniformBuffer(camera, 1);
        batcher.PushMaterial(_shader.Material);
        batcher.PushSampler(new TextureSampler(TextureFilter.Linear, TextureWrap.Clamp, TextureWrap.Clamp));
        batcher.PushMatrix(Matrix3x2.Identity, relative: false);
        batcher.Quad(_albedo, new(-14f, -8f), new(14f, -8f), new(14f, 8f), new(-14f, 8f),
            Vector2.Zero, Vector2.UnitX, Vector2.One, Vector2.UnitY, Color.White);
        batcher.PopMatrix(); batcher.PopSampler(); batcher.PopMaterial();
    }

    private static Matrix4x4 ToMatrix4x4(Matrix3x2 m) => new(
        m.M11, m.M12, 0, 0, m.M21, m.M22, 0, 0, 0, 0, 1, 0, m.M31, m.M32, 0, 1);

    private void DrawControls()
    {
        ImGui.SetNextWindowPos(ImGui.GetMainViewport().WorkPos + new Vector2(16, 100), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(350, 0), ImGuiCond.FirstUseEver);
        ImGui.Begin("Procedural Gut Wall");
        ImGui.TextUnformatted("Growing folds -> rounded tissue -> layered contours");
        if (_bakeTask != null) ImGui.TextUnformatted("Growing folds... (background bake)");
        else ImGui.Text($"1400 x 800 | Bake: {_maps?.BakeMilliseconds ?? 0:F0} ms");
        ImGui.Combo("View", ref _mode, "Lit surface\0Albedo\0Height\0Normals\0");
        ImGui.Checkbox("Follow mouse light", ref _followMouse);
        ImGui.Checkbox("Orbit light", ref _orbit);
        ImGui.Checkbox("Warm fill light", ref _fill);
        ImGui.Checkbox("Show light marker", ref _marker);
        ImGui.SliderFloat2("Light XY", ref _lightPosition, -12f, 12f);
        ImGui.SliderFloat("Light height", ref _lightHeight, .7f, 6f);
        ImGui.SliderFloat("Light intensity", ref _lightIntensity, 0f, 12f);
        ImGui.SliderFloat("Light radius", ref _lightRadius, 3f, 24f);
        ImGui.ColorEdit3("Light color", ref _lightColor);
        ImGui.SliderFloat("Normal strength", ref _normalStrength, 0f, 2f);
        ImGui.SliderFloat("Wet highlights", ref _wetness, 0f, 2f);
        ImGui.SliderFloat("Ambient", ref _ambient, 0f, 1f);
        ImGui.Separator();
        ImGui.InputInt("Seed", ref _seed);
        ImGui.SliderFloat("Fold scale", ref _spacing, .25f, .85f);
        ImGui.SliderFloat("Groove width", ref _grooveWidth, .25f, .85f);
        ImGui.SliderFloat("Fine striations", ref _detail, 0f, 1f);
        ImGui.SliderFloat("Tissue bending", ref _bend, .1f, 1.8f);
        ImGui.TextUnformatted("Generation settings apply with Regenerate.");
        ImGui.BeginDisabled(_bakeTask != null);
        if (ImGui.Button("Regenerate")) Regenerate();
        ImGui.SameLine();
        if (ImGui.Button("New seed")) { _seed = Random.Shared.Next(); Regenerate(); }
        ImGui.EndDisabled();
        ImGui.BeginDisabled(_maps == null || _bakeTask != null);
        if (ImGui.Button("Export maps (PNG)"))
        {
            try
            {
                var directory = Path.Combine(AppContext.BaseDirectory, "GutWallExport");
                ExportMaps(directory);
                _exportStatus = directory;
            }
            catch (Exception error) { _exportStatus = "Export failed: " + error.Message; }
        }
        ImGui.EndDisabled();
        if (_exportStatus.Length > 0) ImGui.TextWrapped(_exportStatus);
        ImGui.End();
    }

    private void ExportMaps(string directory)
    {
        if (_maps == null) return;
        Directory.CreateDirectory(directory);
        Write("gut-albedo.png", _maps.Albedo);
        Write("gut-normal.png", _maps.Normal);
        Write("gut-material.png", _maps.Material);
        void Write(string name, Color[] pixels) => new Image(GutFoldTexture.Width, GutFoldTexture.Height, pixels)
            .WritePng(Path.Combine(directory, name));
    }
}
