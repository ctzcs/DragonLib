using System.Numerics;
using System.Runtime.InteropServices;
using DCFApixels.DragonECS;
using Engine.Assets;
using Engine.Assets.Dasset;
using Engine.ECS;
using Engine.Rendering;
using Engine.World;
using Foster.Framework;
using ImGuiNET;

namespace Game0.Content.Demos;

/// <summary>
/// Renders a .dasset model (baked from glTF by Tools/FbxToGltf cook) loaded through
/// DassetModelScanner with the Standard3D shader (albedo + normal map sampling,
/// directional lambert light).
/// </summary>
public sealed class GltfModelDemoSystem : IEcsInit, IEcsDestroy, IUpdateSystem, IRenderSystem
{
    private const string ShaderResourceBase = "Game0/Shaders/Standard3D";
    private const string ModelAssetName = "Models/testscene";

    [DI] private SceneRouter<RuntimeScene> _sceneRouter = null!;
    [DI] private Camera3D _camera = null!;
    [DI] private Batcher _batcher = null!;
    [DI] private Input _input = null!;
    [DI] private MyGame _game = null!;
    [DI] private AssetDatabase _assets = null!;

    private EmbeddedShaderMaterial? _shader;
    private Renderer3D? _renderer;
    private RenderTarget3D? _renderTarget;
    private Texture? _whiteTexture;
    private readonly List<(Mesh Mesh, Material Material, RenderState3D State, DassetBounds Bounds)> _drawList = [];
    private bool _wasActive;

    private float _yaw = 0.9f;
    private float _pitch = 0.35f;
    private float _distance = 9f;
    private Vector3 _target = new(0.6f, 0.8f, 0f);
    private float _elapsed;

    private float _lightAzimuth = -2.1f;
    private float _lightElevation = 0.7f;
    private Vector3 _ambientLightColor = new(0.34f, 0.37f, 0.44f);
    private Vector3 _diffuseLightColor = new(1.00f, 0.94f, 0.85f);
    private float _ambientLightIntensity = 1f;
    private float _diffuseLightIntensity = 1f;
    private float _normalStrength = 1f;
    private readonly float[] _packedPointLights = new float[PointLight3D.PackedFloatCount];

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct LightUniforms
    {
        public Vector4 LightDirection;
        public Vector4 Ambient;
        public Vector4 Diffuse;
        public Vector4 CameraPosition; // xyz: 相机世界位置（PBR 镜面项要 view 方向）
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct MaterialUniforms
    {
        public Vector4 BaseColorFactor;
        public Vector4 Flags; // x: has albedo, y: has normal map, z: normal strength, w: alpha mode (0/1/2)
        public Vector4 AlphaParams; // x: alpha cutoff（Mask 模式）
        public Vector4 PbrParams; // x: metallic, y: roughness
    }

    public void Init()
    {
        _shader = EmbeddedShaderMaterial.Load(
            _game.GraphicsDevice,
            typeof(GltfModelDemoSystem).Assembly,
            ShaderResourceBase,
            new ShaderStageSpec(3, 4, "fragment_main"),
            new ShaderStageSpec(0, 2, "vertex_main"));

        _whiteTexture = new Texture(_game.GraphicsDevice, 1, 1, [Color.White], name: "White 1x1");
        _renderTarget = new RenderTarget3D(_game.GraphicsDevice);
        _renderer = new Renderer3D(_game.GraphicsDevice);

        var model = _assets.Get<DassetModelAsset>(ModelAssetName);
        foreach (var primitive in model.Primitives)
        {
            var albedo = ResolveTexture(model, primitive.Material.AlbedoTextureIndex);
            var normal = ResolveTexture(model, primitive.Material.NormalTextureIndex);
            var material = _shader.Material.Clone();
            var sampler = new TextureSampler(TextureFilter.Linear, TextureWrap.Repeat);
            material.Fragment.Samplers[0] = new BoundSampler(albedo ?? _whiteTexture, sampler);
            material.Fragment.Samplers[1] = new BoundSampler(normal ?? _whiteTexture, sampler);
            material.Fragment.SetUniformBuffer(new MaterialUniforms
            {
                BaseColorFactor = primitive.Material.BaseColorFactor,
                Flags = new Vector4(
                    albedo != null ? 1f : 0f,
                    normal != null ? 1f : 0f,
                    _normalStrength,
                    (float)primitive.Material.AlphaMode),
                AlphaParams = new Vector4(primitive.Material.AlphaCutoff, 0f, 0f, 0f),
                PbrParams = new Vector4(primitive.Material.Metallic, primitive.Material.Roughness, 0f, 0f),
            }, 1);
            _drawList.Add((primitive.Mesh, material, primitive.Material.ToRenderState(), primitive.Bounds));
        }
    }

    private static Texture? ResolveTexture(DassetModelAsset model, int index)
        => index >= 0 && index < model.Textures.Count ? model.Textures[index] : null;

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
        _drawList.Clear();
    }

    public void Update()
    {
        var active = _sceneRouter.Current == RuntimeScene.GltfModel;
        if (active && !_wasActive)
        {
            _yaw = 0.9f;
            _pitch = 0.35f;
            _distance = 9f;
            _target = new Vector3(0.6f, 0.8f, 0f);
        }

        _wasActive = active;
        if (!active)
            return;

        _elapsed += _game.Time.Delta;
        var io = ImGui.GetIO();
        if (!io.WantCaptureMouse)
        {
            if (_input.Mouse.RightDown)
            {
                // orbit 方向约定：向右拖 = 相机向右绕（看到物体右侧），固定点屏幕左移（锁定：CameraOrbitTests）。
                _yaw += _input.Mouse.Delta.X * 0.008f;
                _pitch = Math.Clamp(_pitch - _input.Mouse.Delta.Y * 0.008f, -0.05f, 1.2f);
            }

            _distance = Math.Clamp(_distance - _input.Mouse.Wheel.Y * 0.8f, 3f, 30f);
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
        if (!_wasActive || _drawList.Count == 0 || _renderTarget == null || _shader == null || _renderer == null)
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
            Ambient = new Vector4(_ambientLightColor * _ambientLightIntensity, 1f),
            Diffuse = new Vector4(_diffuseLightColor * _diffuseLightIntensity, 1f),
            CameraPosition = new Vector4(_camera.Position, 1f),
        };

        var world = Matrix4x4.CreateRotationY(_elapsed * 0.2f);
        _renderer.Begin(_renderTarget.Target, _camera);
        foreach (var (mesh, material, state, bounds) in _drawList)
        {
            var materialUniforms = material.Fragment.GetUniformBuffer<MaterialUniforms>(1);
            material.Fragment.SetUniformBuffer(lightUniforms);
            material.Fragment.SetUniformBuffer(new MaterialUniforms
            {
                BaseColorFactor = materialUniforms.BaseColorFactor,
                Flags = new Vector4(
                    materialUniforms.Flags.X,
                    materialUniforms.Flags.Y,
                    _normalStrength,
                    materialUniforms.Flags.W),
                AlphaParams = materialUniforms.AlphaParams,
                PbrParams = materialUniforms.PbrParams,
            }, 1);
            // 本 demo 不开点光：零值 buffer（count=0）也要绑上，cbuffer 不能空槽。
            material.Fragment.SetUniformBuffer(_packedPointLights.AsSpan(), 3);
            _renderer.Draw(mesh, material, world, state, bounds);
        }
        _renderer.End();

        _renderTarget.Composite(_batcher, width, height);
    }

    private void DrawControls()
    {
        ImGui.SetNextWindowPos(new Vector2(16f, 136f), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(360f, 0f), ImGuiCond.FirstUseEver);
        ImGui.Begin("glTF Model Demo");
        ImGui.TextUnformatted("RMB drag: orbit   Wheel: zoom");
        ImGui.Separator();

        ImGui.SliderFloat("Light azimuth", ref _lightAzimuth, -MathF.PI, MathF.PI);
        ImGui.SliderFloat("Light elevation", ref _lightElevation, 0.05f, 1.5f);
        ImGui.ColorEdit3("Ambient", ref _ambientLightColor);
        ImGui.ColorEdit3("Diffuse", ref _diffuseLightColor);
        ImGui.SliderFloat("Ambient intensity", ref _ambientLightIntensity, 0f, 2f);
        ImGui.SliderFloat("Diffuse intensity", ref _diffuseLightIntensity, 0f, 2f);
        ImGui.SliderFloat("Normal strength", ref _normalStrength, 0f, 3f);
        ImGui.End();
    }
}
