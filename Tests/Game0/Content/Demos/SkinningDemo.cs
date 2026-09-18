using System.Numerics;
using System.Runtime.InteropServices;
using DCFApixels.DragonECS;
using DragonLib.Gltf;
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
/// 蒙皮骨骼动画 MVP：程序化三节关节柱（<see cref="ProceduralSkinnedModel"/>，无外部资产/Blender 依赖），
/// ECS 驱动播放（AnimatorComp + AnimationSystem → SkinPaletteComp），GPU 蒙皮（Standard3DSkinned）。
/// 三个实例以不同速度/相位播同一条 "wave" 剪辑。后续路径：混合/过渡、状态机、根运动（见 SkeletonAnimator 注释）。
/// </summary>
public sealed class SkinningDemoSystem : IEcsInit, IEcsDestroy, IUpdateSystem, IRenderSystem
{
    private const string ShaderResourceBase = "Game0/Shaders/Standard3DSkinned";
    private const string ModelAssetName = "Models/procedural_skin";

    [DI] private SceneRouter<RuntimeScene> _sceneRouter = null!;
    [DI] private EcsDefaultWorld _world = null!;
    [DI] private AssetDatabase _assets = null!;
    [DI] private Camera3D _camera = null!;
    [DI] private Batcher _batcher = null!;
    [DI] private Input _input = null!;
    [DI] private MyGame _game = null!;

    private EmbeddedShaderMaterial? _shader;
    private EmbeddedShaderMaterial? _staticShader;
    private Renderer3D? _renderer;
    private RenderTarget3D? _renderTarget;
    private Texture? _whiteTexture;
    private Material? _material;
    private Material? _gridMaterial;
    private Mesh<PositionNormalUvVertex, uint>? _gridMesh;
    private DassetBounds _gridBounds;
    private DassetModelAsset? _model;
    private readonly List<entlong> _entities = [];
    private bool _wasActive;

    private float _yaw = 0.6f;
    private float _pitch = 0.3f;
    private float _distance = 9f;
    private Vector3 _target = new(0f, 1.5f, 0f);
    private float _timeScale = 1f;
    private readonly PointLight3D[] _pointLights = new PointLight3D[1];
    private readonly float[] _packedPointLights = new float[PointLight3D.PackedFloatCount];

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 4)]
    private struct LightUniforms
    {
        public Vector4 LightDirection;
        public Vector4 Ambient;
        public Vector4 Diffuse;
        public Vector4 CameraPosition;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 4)]
    private struct MaterialUniforms
    {
        public Vector4 BaseColorFactor;
        public Vector4 Flags; // x: has albedo, y: has normal map, z: normal strength, w: alpha mode
        public Vector4 AlphaParams;
        public Vector4 PbrParams; // x: metallic, y: roughness
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 4)]
    private struct ShadowSettingsUniforms
    {
        public Vector4 Settings; // x: enabled=0（本 demo 不开阴影 pass）
    }

    public void Init()
    {
        // 蒙皮变体：fragment 3 sampler + 4 cbuffer（同 Standard3D），vertex 多一个 palette cbuffer（slot 2）。
        _shader = EmbeddedShaderMaterial.Load(
            _game.GraphicsDevice,
            typeof(SkinningDemoSystem).Assembly,
            ShaderResourceBase,
            new ShaderStageSpec(3, 4, "fragment_main"),
            new ShaderStageSpec(0, 3, "vertex_main"));

        _whiteTexture = new Texture(_game.GraphicsDevice, 1, 1, [Color.White], name: "White 1x1");
        _renderTarget = new RenderTarget3D(_game.GraphicsDevice);
        _renderer = new Renderer3D(_game.GraphicsDevice);

        // 程序化资产：内存中构建 glTF → cook → 直接上传 GPU → 注册进资产库（ECS 按名字解析）。
        var cooked = GltfModelCooker.Cook(ProceduralSkinnedModel.Build(), ModelAssetName);
        _model = DassetModelLoader.Load(_game.GraphicsDevice, cooked, ModelAssetName);
        _assets.Register(ModelAssetName, _model);

        // 单 primitive 单材质：无贴图，白色占位 sampler。
        _material = _shader.Material.Clone();
        var sampler = new TextureSampler(TextureFilter.Linear, TextureWrap.Clamp);
        _material.Fragment.Samplers[0] = new BoundSampler(_whiteTexture, sampler);
        _material.Fragment.Samplers[1] = new BoundSampler(_whiteTexture, sampler);
        _material.Fragment.Samplers[2] = new BoundSampler(_whiteTexture, sampler);
        var dassetMaterial = _model.Primitives[0].Material;
        _material.Fragment.SetUniformBuffer(new MaterialUniforms
        {
            BaseColorFactor = dassetMaterial.BaseColorFactor,
            Flags = Vector4.Zero,
            AlphaParams = new Vector4(dassetMaterial.AlphaCutoff, 0f, 0f, 0f),
            PbrParams = new Vector4(dassetMaterial.Metallic, dassetMaterial.Roughness, 0f, 0f),
        }, 1);

        // 三个实例：不同速度/相位播同一条剪辑，直观对照动画驱动。
        SpawnEntity(new Vector3(-2.2f, 0f, 0f), speed: 0.7f, startTime: 0f);
        SpawnEntity(new Vector3(0f, 0f, 0f), speed: 1.0f, startTime: 0.33f);
        SpawnEntity(new Vector3(2.2f, 0f, 0f), speed: 1.4f, startTime: 0.66f);

        // 静止参照物：纯黑背景 + orbit 缺参照系会把「相机在动」误感成「模型在转」。
        // 地面网格走静态 Standard3D 管线（无 palette），绝对静止、不受动画影响。
        _staticShader = EmbeddedShaderMaterial.Load(
            _game.GraphicsDevice,
            typeof(SkinningDemoSystem).Assembly,
            "Game0/Shaders/Standard3D",
            new ShaderStageSpec(3, 4, "fragment_main"),
            new ShaderStageSpec(0, 2, "vertex_main"));
        (_gridMesh, _gridBounds) = BuildGroundGrid(_game.GraphicsDevice);
        _gridMaterial = _staticShader.Material.Clone();
        _gridMaterial.Fragment.Samplers[0] = new BoundSampler(_whiteTexture, sampler);
        _gridMaterial.Fragment.Samplers[1] = new BoundSampler(_whiteTexture, sampler);
        _gridMaterial.Fragment.Samplers[2] = new BoundSampler(_whiteTexture, sampler);
        _gridMaterial.Fragment.SetUniformBuffer(new MaterialUniforms
        {
            BaseColorFactor = new Vector4(0.32f, 0.35f, 0.40f, 1f),
            Flags = Vector4.Zero,
            AlphaParams = new Vector4(0.5f, 0f, 0f, 0f),
            PbrParams = new Vector4(0f, 1f, 0f, 0f),
        }, 1);
    }

    /// <summary>y=0 地面的细条网格（21×21 条 0.5m 间隔细盒合并成单 mesh，一次 draw call）。</summary>
    private static (Mesh<PositionNormalUvVertex, uint> Mesh, DassetBounds Bounds) BuildGroundGrid(GraphicsDevice device)
    {
        var vertices = new List<PositionNormalUvVertex>();
        var indices = new List<uint>();
        const float half = 5f;
        const float thickness = 0.03f;
        for (var i = -10; i <= 10; i++)
        {
            var offset = i * 0.5f;
            AddBox(vertices, indices, new Vector3(0f, 0f, offset), new Vector3(half * 2f, thickness, thickness));
            AddBox(vertices, indices, new Vector3(offset, 0f, 0f), new Vector3(thickness, thickness, half * 2f));
        }

        var mesh = new Mesh<PositionNormalUvVertex, uint>(device, "SkinningDemo Ground Grid");
        mesh.SetVertices(CollectionsMarshal.AsSpan(vertices));
        mesh.SetIndices(CollectionsMarshal.AsSpan(indices));
        return (mesh, new DassetBounds { Min = new Vector3(-half, -thickness, -half), Max = new Vector3(half, thickness, half) });
    }

    /// <summary>追加一个轴对齐盒子的 24 顶点/36 索引（外侧 CCW，面法线朝外，约定见 Rendering/README.md）。</summary>
    private static void AddBox(List<PositionNormalUvVertex> vertices, List<uint> indices, Vector3 center, Vector3 size)
    {
        var h = size * 0.5f;
        AddFace(vertices, indices, Vector3.UnitX,
            center + new Vector3(h.X, -h.Y, h.Z), center + new Vector3(h.X, -h.Y, -h.Z),
            center + new Vector3(h.X, h.Y, -h.Z), center + new Vector3(h.X, h.Y, h.Z));
        AddFace(vertices, indices, -Vector3.UnitX,
            center + new Vector3(-h.X, -h.Y, -h.Z), center + new Vector3(-h.X, -h.Y, h.Z),
            center + new Vector3(-h.X, h.Y, h.Z), center + new Vector3(-h.X, h.Y, -h.Z));
        AddFace(vertices, indices, Vector3.UnitY,
            center + new Vector3(-h.X, h.Y, h.Z), center + new Vector3(h.X, h.Y, h.Z),
            center + new Vector3(h.X, h.Y, -h.Z), center + new Vector3(-h.X, h.Y, -h.Z));
        AddFace(vertices, indices, -Vector3.UnitY,
            center + new Vector3(-h.X, -h.Y, -h.Z), center + new Vector3(h.X, -h.Y, -h.Z),
            center + new Vector3(h.X, -h.Y, h.Z), center + new Vector3(-h.X, -h.Y, h.Z));
        AddFace(vertices, indices, Vector3.UnitZ,
            center + new Vector3(-h.X, -h.Y, h.Z), center + new Vector3(h.X, -h.Y, h.Z),
            center + new Vector3(h.X, h.Y, h.Z), center + new Vector3(-h.X, h.Y, h.Z));
        AddFace(vertices, indices, -Vector3.UnitZ,
            center + new Vector3(h.X, -h.Y, -h.Z), center + new Vector3(-h.X, -h.Y, -h.Z),
            center + new Vector3(-h.X, h.Y, -h.Z), center + new Vector3(h.X, h.Y, -h.Z));
    }

    private static void AddFace(
        List<PositionNormalUvVertex> vertices,
        List<uint> indices,
        Vector3 normal,
        Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        // (a,b,c) 叉积与法线同向：外侧 CCW 即正面。
        var start = (uint)vertices.Count;
        vertices.Add(new PositionNormalUvVertex(a, normal, Vector2.Zero, Vector4.UnitX));
        vertices.Add(new PositionNormalUvVertex(b, normal, Vector2.Zero, Vector4.UnitX));
        vertices.Add(new PositionNormalUvVertex(c, normal, Vector2.Zero, Vector4.UnitX));
        vertices.Add(new PositionNormalUvVertex(d, normal, Vector2.Zero, Vector4.UnitX));
        indices.Add(start);
        indices.Add(start + 1);
        indices.Add(start + 2);
        indices.Add(start);
        indices.Add(start + 2);
        indices.Add(start + 3);
    }

    private readonly List<float> _baseSpeeds = [];

    private void SpawnEntity(Vector3 position, float speed, float startTime)
    {
        var e = _world.NewEntity();
        _world.GetPool<Transform3DComp>().Add(e) = new Transform3DComp(position, Quaternion.Identity, Vector3.One);
        _world.GetPool<MeshRendererComp>().Add(e) = new MeshRendererComp(AssetId.FromName(ModelAssetName));
        _world.GetPool<AnimatorComp>().Add(e) = new AnimatorComp(0, speed) { Time = startTime };
        _entities.Add(_world.GetEntityLong(e));
        _baseSpeeds.Add(speed);
    }

    public void Destroy()
    {
        foreach (var e in _entities)
            if (e.IsAlive)
                _world.DelEntity(e);
        _entities.Clear();
        _baseSpeeds.Clear();

        if (_model != null)
        {
            _assets.Unregister(AssetId.FromName(ModelAssetName));
            _model.Dispose();
            _model = null;
        }
        _whiteTexture?.Dispose();
        _renderTarget?.Dispose();
        _renderer?.Dispose();
        _shader?.Dispose();
        _staticShader?.Dispose();
        _gridMesh?.Dispose();
        _whiteTexture = null;
        _renderTarget = null;
        _renderer = null;
        _shader = null;
        _staticShader = null;
        _gridMaterial = null;
        _gridMesh = null;
        _material = null;
    }

    public void Update()
    {
        var active = _sceneRouter.Current == RuntimeScene.Skinning;
        if (active && !_wasActive)
        {
            _yaw = 0.6f;
            _pitch = 0.3f;
            _distance = 9f;
            _target = new Vector3(0f, 1.5f, 0f);
        }

        _wasActive = active;
        if (!active)
            return;

        var io = ImGui.GetIO();
        if (!io.WantCaptureMouse && _input.Mouse.RightDown)
        {
            // orbit 方向约定：向右拖 = 相机向右绕（看到物体右侧），固定点屏幕左移（锁定：CameraOrbitTests）。
            _yaw += _input.Mouse.Delta.X * 0.008f;
            _pitch = Math.Clamp(_pitch - _input.Mouse.Delta.Y * 0.008f, -0.05f, 1.2f);
            _distance = Math.Clamp(_distance - _input.Mouse.Wheel.Y * 0.7f, 3f, 30f);
        }

        var horizontal = MathF.Cos(_pitch) * _distance;
        _camera.Target = _target;
        _camera.Position = _target + new Vector3(
            MathF.Sin(_yaw) * horizontal,
            MathF.Sin(_pitch) * _distance,
            MathF.Cos(_yaw) * horizontal);

        ImGui.SetNextWindowPos(new Vector2(16f, 136f), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(320f, 0f), ImGuiCond.FirstUseEver);
        ImGui.Begin("Skinning (GPU + ECS)");
        ImGui.TextUnformatted("RMB drag: orbit   Wheel: zoom");
        ImGui.TextUnformatted($"clip: \"{ProceduralSkinnedModel.ClipName}\" x3 instances");
        ImGui.SliderFloat("Time scale", ref _timeScale, 0f, 3f);
        // 诊断：orbit 是否生效——拖拽时这两个值应实时变化。
        ImGui.TextUnformatted($"yaw: {_yaw:F2}   camera: {_camera.Position:F2}");
        ImGui.End();
    }

    public void Render()
    {
        if (!_wasActive || _renderer == null || _renderTarget == null || _shader == null ||
            _material == null || _model == null)
            return;

        var width = _game.Window.WidthInPixels;
        var height = _game.Window.HeightInPixels;
        _renderTarget.Resize(width, height);
        _renderTarget.Clear(new Color(0x0A0E17));
        _camera.ViewportSize = new Point2(width, height);

        var lightUniforms = new LightUniforms
        {
            LightDirection = new Vector4(Vector3.Normalize(new Vector3(-0.5f, -1f, 0.35f)), 0f),
            Ambient = new Vector4(0.36f, 0.39f, 0.46f, 1f),
            Diffuse = new Vector4(1.0f, 0.95f, 0.88f, 1f),
            CameraPosition = new Vector4(_camera.Position, 1f),
        };
        _material.Fragment.SetUniformBuffer(lightUniforms);
        _material.Fragment.SetUniformBuffer(new ShadowSettingsUniforms(), 2);

        // 一盏弱补光从背光侧（-X/-Z）照亮暗面；同时保证点光 cbuffer（slot 3）必有绑定——
        // 不绑会读到未定义内容（本机恰好为零无害，跨后端不确定）。
        _pointLights[0] = new PointLight3D(new Vector3(-2.5f, 2.5f, 2.0f), 8f, new Vector3(0.45f, 0.55f, 0.8f), 0.9f);
        PointLight3D.Pack(_pointLights, _packedPointLights);
        _material.Fragment.SetUniformBuffer(_packedPointLights.AsSpan(), 3);

        // 速度倍率直接写回 AnimatorComp.Speed（基础速度在 SpawnEntity 里记录）。
        var animatorPool = _world.GetPool<AnimatorComp>();
        var localToWorldPool = _world.GetPool<LocalToWorldComp>();
        var palettePool = _world.GetPool<SkinPaletteComp>();

        _renderer.Begin(_renderTarget.Target, _camera);

        // 静止参照物先画：纯静态管线（无 palette、identity world、不受动画/相机 orbit 之外任何驱动）。
        if (_gridMesh != null && _gridMaterial != null)
        {
            _gridMaterial.Fragment.SetUniformBuffer(lightUniforms);
            _gridMaterial.Fragment.SetUniformBuffer(new ShadowSettingsUniforms(), 2);
            _gridMaterial.Fragment.SetUniformBuffer(_packedPointLights.AsSpan(), 3);
            _renderer.Draw(_gridMesh, _gridMaterial, Matrix4x4.Identity, RenderState3D.Opaque, _gridBounds);
        }

        for (var i = 0; i < _entities.Count; i++)
        {
            if (!_entities[i].TryGetID(out int e))
                continue;
            if (animatorPool.Has(e))
                animatorPool.Get(e).Speed = _baseSpeeds[i] * _timeScale;
            if (!localToWorldPool.Has(e) || !palettePool.Has(e))
                continue;

            var primitive = _model.Primitives[0];
            _renderer.Draw(primitive.Mesh, _material, localToWorldPool.Get(e).Value,
                RenderState3D.Opaque, palettePool.Get(e).Matrices, primitive.Bounds);
        }
        _renderer.End();

        _renderTarget.Composite(_batcher, width, height);
    }
}
