using System.Numerics;
using System.Runtime.InteropServices;
using DCFApixels.DragonECS;
using DragonLib.Gltf;
using Engine.Assets;
using Engine.ECS;
using Engine.Rendering;
using Engine.World;
using Foster.Framework;
using ImGuiNET;

namespace Game0.Content.Demos;

/// <summary>
/// Renders a glTF model loaded through GltfModelScanner with the Standard3D
/// shader (albedo + normal map sampling, directional lambert light).
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
    private readonly List<(Mesh Mesh, Material Material)> _drawList = [];
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

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct LightUniforms
    {
        public Vector4 LightDirection;
        public Vector4 Ambient;
        public Vector4 Diffuse;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct MaterialUniforms
    {
        public Vector4 BaseColorFactor;
        public Vector4 Flags; // x: has albedo, y: has normal map, z: normal strength, w: unused
    }

    public void Init()
    {
        _shader = EmbeddedShaderMaterial.Load(
            _game.GraphicsDevice,
            typeof(GltfModelDemoSystem).Assembly,
            ShaderResourceBase,
            new ShaderStageSpec(3, 3, "fragment_main"),
            new ShaderStageSpec(0, 2, "vertex_main"));

        _whiteTexture = new Texture(_game.GraphicsDevice, 1, 1, [Color.White], name: "White 1x1");
        _renderTarget = new RenderTarget3D(_game.GraphicsDevice);
        _renderer = new Renderer3D(_game.GraphicsDevice);

        var model = _assets.Get<GltfModelAsset>(ModelAssetName);
        foreach (var primitive in model.Primitives)
        {
            var material = _shader.Material.Clone();
            var sampler = new TextureSampler(TextureFilter.Linear, TextureWrap.Repeat);
            material.Fragment.Samplers[0] = new BoundSampler(primitive.Material.AlbedoTexture ?? _whiteTexture, sampler);
            material.Fragment.Samplers[1] = new BoundSampler(primitive.Material.NormalTexture ?? _whiteTexture, sampler);
            material.Fragment.SetUniformBuffer(new MaterialUniforms
            {
                BaseColorFactor = primitive.Material.BaseColorFactor,
                Flags = new Vector4(
                    primitive.Material.AlbedoTexture != null ? 1f : 0f,
                    primitive.Material.NormalTexture != null ? 1f : 0f,
                    _normalStrength,
                    0f),
            }, 1);
            _drawList.Add((primitive.Mesh, material));
        }
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
                _yaw -= _input.Mouse.Delta.X * 0.008f;
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
        };

        var world = Matrix4x4.CreateRotationY(_elapsed * 0.2f);
        _renderer.Begin(_renderTarget.Target, _camera);
        foreach (var (mesh, material) in _drawList)
        {
            material.Fragment.SetUniformBuffer(lightUniforms);
            material.Fragment.SetUniformBuffer(new MaterialUniforms
            {
                BaseColorFactor = material.Fragment.GetUniformBuffer<MaterialUniforms>(1).BaseColorFactor,
                Flags = new Vector4(
                    material.Fragment.GetUniformBuffer<MaterialUniforms>(1).Flags.X,
                    material.Fragment.GetUniformBuffer<MaterialUniforms>(1).Flags.Y,
                    _normalStrength,
                    0f),
            }, 1);
            _renderer.Draw(mesh, material, world);
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
