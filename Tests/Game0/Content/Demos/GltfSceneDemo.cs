using System.Numerics;
using System.Text.Json;
using DCFApixels.DragonECS;
using Engine;
using Engine.Assets;
using Engine.Assets.Dasset;
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
    private const string DepthShaderResourceBase = "Game0/Shaders/DepthOnly";
    private const string LevelPath = "Resources/Levels/gltf_scene.json";

    [DI] private SceneRouter<RuntimeScene> _sceneRouter = null!;
    [DI] private EcsDefaultWorld _world = null!;
    [DI] private AssetDatabase _assets = null!;
    [DI] private Camera3D _camera = null!;
    [DI] private Batcher _batcher = null!;
    [DI] private Input _input = null!;
    [DI] private MyGame _game = null!;

    private EmbeddedShaderMaterial? _shader;
    private EmbeddedShaderMaterial? _depthShader;
    private ShadowMap? _shadowMap;
    private Renderer3D? _renderer;
    private RenderTarget3D? _renderTarget;
    private Texture? _whiteTexture;
    private readonly Dictionary<AssetId, List<(Material Material, RenderState3D State)>> _materialCache = [];
    private readonly List<entlong> _loadedEntities = [];
    private bool _wasActive;
    private int _drawnInstances;

    private float _yaw = 0.75f;
    private float _pitch = 0.4f;
    private float _distance = 8.5f;
    private Vector3 _target = new(0f, 0.9f, 0f);
    private string _status = "not loaded";

    private bool _shadowsEnabled = true;
    private float _shadowBias = 0.0015f;
    private float _shadowDarkness = 0.65f;
    private float _lightAzimuth = -0.9f;
    private float _lightElevation = 0.9f;
    private Vector3 _ambientLightColor = new(0.36f, 0.39f, 0.46f);
    private Vector3 _diffuseLightColor = new(1.00f, 0.95f, 0.88f);
    private bool _pointLightsEnabled = true;
    private readonly PointLight3D[] _pointLights = new PointLight3D[PointLight3D.MaxCount];
    private readonly float[] _packedPointLights = new float[PointLight3D.PackedFloatCount];

    public void Init()
    {
        _shader = EmbeddedShaderMaterial.Load(
            _game.GraphicsDevice,
            typeof(GltfSceneDemoSystem).Assembly,
            ShaderResourceBase,
            new ShaderStageSpec(3, 4, "fragment_main"),
            new ShaderStageSpec(0, 2, "vertex_main"));

        _depthShader = EmbeddedShaderMaterial.Load(
            _game.GraphicsDevice,
            typeof(GltfSceneDemoSystem).Assembly,
            DepthShaderResourceBase,
            new ShaderStageSpec(0, 0, "fragment_main"),
            new ShaderStageSpec(0, 1, "vertex_main"));

        _whiteTexture = new Texture(_game.GraphicsDevice, 1, 1, [Color.White], name: "White 1x1");
        _renderTarget = new RenderTarget3D(_game.GraphicsDevice);
        _shadowMap = new ShadowMap(_game.GraphicsDevice, 2048);
        _renderer = new Renderer3D(_game.GraphicsDevice);
    }

    public void Destroy()
    {
        _whiteTexture?.Dispose();
        _renderTarget?.Dispose();
        _shadowMap?.Dispose();
        _renderer?.Dispose();
        _shader?.Dispose();
        _depthShader?.Dispose();
        _whiteTexture = null;
        _renderTarget = null;
        _shadowMap = null;
        _renderer = null;
        _shader = null;
        _depthShader = null;
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
                // orbit 方向约定：向右拖 = 相机向右绕（看到物体右侧），固定点屏幕左移（锁定：CameraOrbitTests）。
                _yaw += _input.Mouse.Delta.X * 0.008f;
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
        if (!_wasActive || _renderTarget == null || _shader == null || _depthShader == null ||
            _shadowMap == null || _renderer == null)
            return;

        var width = _game.Window.WidthInPixels;
        var height = _game.Window.HeightInPixels;
        _renderTarget.Resize(width, height);
        _renderTarget.Clear(new Color(0x0A0E17));
        _camera.ViewportSize = new Point2(width, height);

        var horizontalLight = MathF.Cos(_lightElevation);
        var lightDirection = new Vector3(
            horizontalLight * MathF.Cos(_lightAzimuth),
            -MathF.Sin(_lightElevation),
            horizontalLight * MathF.Sin(_lightAzimuth));
        var lightUniforms = new LightUniforms
        {
            LightDirection = new Vector4(lightDirection, 0f),
            Ambient = new Vector4(_ambientLightColor, 1f),
            Diffuse = new Vector4(_diffuseLightColor, 1f),
            CameraPosition = new Vector4(_camera.Position, 1f),
        };

        // 两个固定点光展示 PBR 高光：一暖一冷。
        var pointLightCount = 0;
        if (_pointLightsEnabled)
        {
            _pointLights[pointLightCount++] = new PointLight3D(new Vector3(2.5f, 2.2f, 1.8f), 6f, new Vector3(1.0f, 0.55f, 0.25f), 2.5f);
            _pointLights[pointLightCount++] = new PointLight3D(new Vector3(-2.2f, 1.6f, -1.5f), 5f, new Vector3(0.3f, 0.55f, 1.0f), 2.0f);
        }
        PointLight3D.Pack(_pointLights.AsSpan(0, pointLightCount), _packedPointLights);

        // 光照正交框覆盖整个摆放区域。
        _shadowMap.Resize(_shadowMap.Size);
        _shadowMap.UpdateLight(lightDirection, new Vector3(0f, 1f, 0f), 6f);
        var shadowUniforms = new ShadowMatrixUniforms { LightViewProjection = _shadowMap.LightViewProjection };
        var shadowSettings = new ShadowSettingsUniforms
        {
            Settings = new Vector4(
                _shadowsEnabled ? 1f : 0f,
                1f / _shadowMap.Size,
                _shadowBias,
                _shadowDarkness),
        };
        var shadowSampler = new BoundSampler(_shadowMap.DepthTexture, new TextureSampler(TextureFilter.Linear, TextureWrap.Clamp));
        if (_shadowsEnabled)
            _shadowMap.Clear();

        var meshPool = _world.GetPool<MeshRendererComp>();
        var localToWorldPool = _world.GetPool<LocalToWorldComp>();

        _drawnInstances = 0;
        _renderer.Begin(_renderTarget.Target, _camera);
        if (_shadowsEnabled)
            _renderer.SetShadowPass(_shadowMap.Target, _depthShader.Material, _shadowMap.LightViewProjection);

        foreach (int e in _world.Entities)
        {
            if (!meshPool.Has(e) || !localToWorldPool.Has(e)) continue;
            var meshRenderer = meshPool.Get(e);
            var world = localToWorldPool.Get(e).Value;

            var model = _assets.Get<DassetModelAsset>(meshRenderer.Model);
            if (model == null) continue;

            var materials = GetMaterials(model);
            for (var i = 0; i < model.Primitives.Count && i < materials.Count; i++)
            {
                if (meshRenderer.MeshIndex >= 0 && i != meshRenderer.MeshIndex) continue;
                // 蒙皮 primitive 需要 palette（AnimationSystem/SkinPaletteComp）与蒙皮 shader 变体，
                // 本 demo 的管线不演示蒙皮（见 SkinningDemo），遇到跳过。
                if (model.Primitives[i].IsSkinned) continue;
                var (material, state) = materials[i];
                material.Fragment.SetUniformBuffer(lightUniforms);
                material.Vertex.SetUniformBuffer(shadowUniforms, 1);
                material.Fragment.SetUniformBuffer(shadowSettings, 2);
                material.Fragment.SetUniformBuffer(_packedPointLights.AsSpan(), 3);
                if (_shadowsEnabled)
                    material.Fragment.Samplers[2] = shadowSampler;
                _renderer.Draw(model.Primitives[i].Mesh, material, world, state, model.Primitives[i].Bounds);
            }

            _drawnInstances++;
        }
        _renderer.End();

        _renderTarget.Composite(_batcher, width, height);
    }

    private List<(Material Material, RenderState3D State)> GetMaterials(DassetModelAsset model)
    {
        if (_materialCache.TryGetValue(model.Id, out var cached))
            return cached;

        var sampler = new TextureSampler(TextureFilter.Linear, TextureWrap.Repeat);
        var materials = new List<(Material, RenderState3D)>(model.Primitives.Count);
        foreach (var primitive in model.Primitives)
        {
            // AlphaMode 生效：Opaque/Mask 走不透明队列（Mask 由 shader 按 AlphaCutoff clip），
            // Blend 走透明队列（back-to-front、不写深度）。本期资产还没有真透明的。
            var albedo = ResolveTexture(model, primitive.Material.AlbedoTextureIndex);
            var normal = ResolveTexture(model, primitive.Material.NormalTextureIndex);
            var material = _shader!.Material.Clone();
            material.Fragment.Samplers[0] = new BoundSampler(albedo ?? _whiteTexture, sampler);
            material.Fragment.Samplers[1] = new BoundSampler(normal ?? _whiteTexture, sampler);
            material.Fragment.SetUniformBuffer(new MaterialUniforms
            {
                BaseColorFactor = primitive.Material.BaseColorFactor,
                Flags = new Vector4(
                    albedo != null ? 1f : 0f,
                    normal != null ? 1f : 0f,
                    1f,
                    (float)primitive.Material.AlphaMode),
                AlphaParams = new Vector4(primitive.Material.AlphaCutoff, 0f, 0f, 0f),
                PbrParams = new Vector4(primitive.Material.Metallic, primitive.Material.Roughness, 0f, 0f),
            }, 1);
            materials.Add((material, primitive.Material.ToRenderState()));
        }

        _materialCache.Add(model.Id, materials);
        return materials;
    }

    private static Texture? ResolveTexture(DassetModelAsset model, int index)
        => index >= 0 && index < model.Textures.Count ? model.Textures[index] : null;

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
        if (_renderer != null)
            ImGui.TextUnformatted($"frustum culled: {_renderer.LastFrameCulledCount}");
        ImGui.Separator();
        if (ImGui.Button("Reload level"))
            ReloadLevel();
        ImGui.SameLine();
        if (ImGui.Button("Save level (diff)"))
        {
            SaveLevel();
            ReloadLevel();
        }
        ImGui.Separator();
        ImGui.Checkbox("Shadows", ref _shadowsEnabled);
        ImGui.SliderFloat("Shadow bias", ref _shadowBias, 0f, 0.01f);
        ImGui.SliderFloat("Shadow darkness", ref _shadowDarkness, 0f, 1f);
        ImGui.Checkbox("Point lights", ref _pointLightsEnabled);
        ImGui.Separator();
        ImGui.SliderFloat("Light azimuth", ref _lightAzimuth, -MathF.PI, MathF.PI);
        ImGui.SliderFloat("Light elevation", ref _lightElevation, 0.2f, 1.5f);
        ImGui.ColorEdit3("Ambient", ref _ambientLightColor);
        ImGui.ColorEdit3("Diffuse", ref _diffuseLightColor);
        ImGui.End();
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 4)]
    private struct LightUniforms
    {
        public Vector4 LightDirection;
        public Vector4 Ambient;
        public Vector4 Diffuse;
        public Vector4 CameraPosition; // xyz: 相机世界位置（PBR 镜面项要 view 方向）
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 4)]
    private struct MaterialUniforms
    {
        public Vector4 BaseColorFactor;
        public Vector4 Flags; // x: has albedo, y: has normal map, z: normal strength, w: alpha mode (0/1/2)
        public Vector4 AlphaParams; // x: alpha cutoff（Mask 模式）
        public Vector4 PbrParams; // x: metallic, y: roughness
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 4)]
    private struct ShadowMatrixUniforms
    {
        public Matrix4x4 LightViewProjection;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 4)]
    private struct ShadowSettingsUniforms
    {
        public Vector4 Settings; // x: enabled, y: texel size, z: bias, w: darkness
    }
}
