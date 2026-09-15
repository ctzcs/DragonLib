using System.Numerics;
using System.Text.Json;
using DCFApixels.DragonECS;
using DragonLib.Gltf;
using Engine;
using Engine.Assets;
using Engine.ECS;
using Engine.Rendering;
using Engine.World;
using Foster.Framework;
using ImGuiNET;

namespace Game0.Content.Demos;

/// <summary>
/// ECS 驱动的 glTF 场景：从 Resources/Levels/gltf_scene.json 加载实体
/// （Transform3DComp / Parent3DComp / MeshRendererComp），Transform3DSystem 算层级矩阵，
/// 本系统遍历 MeshRendererComp 提交渲染。支持存盘（差量）→ 重载的闭环演示。
/// </summary>
public sealed class GltfSceneDemoSystem : IEcsInit, IEcsDestroy, IUpdateSystem, IRenderSystem
{
    private const string ShaderResourceBase = "Game0/Shaders/Standard3D";
    private const string LevelPath = "Resources/Levels/gltf_scene.json";

    [DI] private SceneRouter<RuntimeScene> _sceneRouter = null!;
    [DI] private EcsDefaultWorld _world = null!;
    [DI] private AssetDatabase _assets = null!;
    [DI] private Camera3D _camera = null!;
    [DI] private Batcher _batcher = null!;
    [DI] private Input _input = null!;
    [DI] private MyGame _game = null!;

    private EmbeddedShaderMaterial? _shader;
    private Renderer3D? _renderer;
    private RenderTarget3D? _renderTarget;
    private Texture? _whiteTexture;
    private readonly Dictionary<AssetId, List<Material>> _materialCache = [];
    private readonly List<entlong> _loadedEntities = [];
    private bool _wasActive;
    private int _drawnInstances;

    private float _yaw = 0.75f;
    private float _pitch = 0.4f;
    private float _distance = 8.5f;
    private Vector3 _target = new(0f, 0.9f, 0f);
    private string _status = "not loaded";

    public void Init()
    {
        _shader = EmbeddedShaderMaterial.Load(
            _game.GraphicsDevice,
            typeof(GltfSceneDemoSystem).Assembly,
            ShaderResourceBase,
            new ShaderStageSpec(2, 2, "fragment_main"),
            new ShaderStageSpec(0, 1, "vertex_main"));

        _whiteTexture = new Texture(_game.GraphicsDevice, 1, 1, [Color.White], name: "White 1x1");
        _renderTarget = new RenderTarget3D(_game.GraphicsDevice);
        _renderer = new Renderer3D(_game.GraphicsDevice);
    }

    public void Destroy()
    {
        _whiteTexture?.Dispose();
        _renderTarget?.Dispose();
        _renderer?.Dispose();
        _shader?.Dispose();
        _whiteTexture = null;
        _renderTarget = null;
        _renderer = null;
        _shader = null;
        _materialCache.Clear();
        _loadedEntities.Clear();
    }

    public void Update()
    {
        var active = _sceneRouter.Current == RuntimeScene.GltfScene;
        if (active && !_wasActive)
        {
            _yaw = 0.75f;
            _pitch = 0.4f;
            _distance = 8.5f;
            _target = new Vector3(0f, 0.9f, 0f);
            if (_loadedEntities.Count == 0)
                ReloadLevel();
        }

        _wasActive = active;
        if (!active)
            return;

        var io = ImGui.GetIO();
        if (!io.WantCaptureMouse)
        {
            if (_input.Mouse.RightDown)
            {
                _yaw -= _input.Mouse.Delta.X * 0.008f;
                _pitch = Math.Clamp(_pitch - _input.Mouse.Delta.Y * 0.008f, -0.05f, 1.2f);
            }

            _distance = Math.Clamp(_distance - _input.Mouse.Wheel.Y * 0.7f, 3f, 30f);
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
        if (!_wasActive || _renderTarget == null || _shader == null || _renderer == null)
            return;

        var width = _game.Window.WidthInPixels;
        var height = _game.Window.HeightInPixels;
        _renderTarget.Resize(width, height);
        _renderTarget.Clear(new Color(0x0A0E17));
        _camera.ViewportSize = new Point2(width, height);

        var lightDirection = new Vector3(-0.45f, -0.75f, 0.35f);
        var lightUniforms = new LightUniforms
        {
            LightDirection = new Vector4(lightDirection, 0f),
            Ambient = new Vector4(0.36f, 0.39f, 0.46f, 1f),
            Diffuse = new Vector4(1f, 0.95f, 0.88f, 1f),
        };

        var meshPool = _world.GetPool<MeshRendererComp>();
        var localToWorldPool = _world.GetPool<LocalToWorldComp>();

        _drawnInstances = 0;
        _renderer.Begin(_renderTarget.Target, _camera);
        foreach (int e in _world.Entities)
        {
            if (!meshPool.Has(e) || !localToWorldPool.Has(e)) continue;
            var meshRenderer = meshPool.Get(e);
            var world = localToWorldPool.Get(e).Value;

            var model = _assets.Get<GltfModelAsset>(meshRenderer.Model);
            if (model == null) continue;

            var materials = GetMaterials(model);
            for (var i = 0; i < model.Primitives.Count && i < materials.Count; i++)
            {
                if (meshRenderer.MeshIndex >= 0 && i != meshRenderer.MeshIndex) continue;
                var material = materials[i];
                material.Fragment.SetUniformBuffer(lightUniforms);
                _renderer.Draw(model.Primitives[i].Mesh, material, world);
            }

            _drawnInstances++;
        }
        _renderer.End();

        _renderTarget.Composite(_batcher, width, height);
    }

    private List<Material> GetMaterials(GltfModelAsset model)
    {
        if (_materialCache.TryGetValue(model.Id, out var cached))
            return cached;

        var sampler = new TextureSampler(TextureFilter.Linear, TextureWrap.Repeat);
        var materials = new List<Material>(model.Primitives.Count);
        foreach (var primitive in model.Primitives)
        {
            var material = _shader!.Material.Clone();
            material.Fragment.Samplers[0] = new BoundSampler(primitive.Material.AlbedoTexture ?? _whiteTexture, sampler);
            material.Fragment.Samplers[1] = new BoundSampler(primitive.Material.NormalTexture ?? _whiteTexture, sampler);
            material.Fragment.SetUniformBuffer(new MaterialUniforms
            {
                BaseColorFactor = primitive.Material.BaseColorFactor,
                Flags = new Vector4(
                    primitive.Material.AlbedoTexture != null ? 1f : 0f,
                    primitive.Material.NormalTexture != null ? 1f : 0f,
                    1f,
                    0f),
            }, 1);
            materials.Add(material);
        }

        _materialCache.Add(model.Id, materials);
        return materials;
    }

    private void ReloadLevel()
    {
        foreach (var e in _loadedEntities)
            if (e.IsAlive)
                _world.DelEntity(e);
        _loadedEntities.Clear();
        _materialCache.Clear();

        var storage = StorageUtils.GetDevGameRoot;
        if (!storage.FileExists(LevelPath))
        {
            _status = $"level file missing: {LevelPath}";
            return;
        }

        var level = JsonSerializer.Deserialize<LevelData>(storage.ReadAllText(LevelPath));
        if (level == null)
        {
            _status = "level parse failed";
            return;
        }

        var map = LevelSerializer.Load(_world, level, _assets);
        foreach (var entity in map.Values)
            _loadedEntities.Add(entity);
        _status = $"loaded {map.Count} entities";
    }

    private void SaveLevel()
    {
        var level = LevelSerializer.Save(_world, "gltf_scene", _assets);
        var storage = StorageUtils.GetDevGameRoot;
        using var stream = storage.Create(LevelPath);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        JsonSerializer.Serialize(writer, level, new JsonSerializerOptions { IncludeFields = true });
        _status = $"saved {level.Entities.Count} entities -> {LevelPath}";
    }

    private void DrawControls()
    {
        ImGui.SetNextWindowPos(new Vector2(16f, 136f), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(360f, 0f), ImGuiCond.FirstUseEver);
        ImGui.Begin("glTF Scene (ECS)");
        ImGui.TextUnformatted("RMB drag: orbit   Wheel: zoom");
        ImGui.TextUnformatted(_status);
        ImGui.TextUnformatted($"drawn instances: {_drawnInstances}");
        ImGui.Separator();
        if (ImGui.Button("Reload level"))
            ReloadLevel();
        ImGui.SameLine();
        if (ImGui.Button("Save level (diff)"))
        {
            SaveLevel();
            ReloadLevel();
        }
        ImGui.End();
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 4)]
    private struct LightUniforms
    {
        public Vector4 LightDirection;
        public Vector4 Ambient;
        public Vector4 Diffuse;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 4)]
    private struct MaterialUniforms
    {
        public Vector4 BaseColorFactor;
        public Vector4 Flags;
    }
}
