using System;
using System.Diagnostics;
using System.Numerics;
using DCFApixels.DragonECS;
using Engine;
using Engine.Assets;
using Engine.DearImGui;
using Engine.ECS;
using Engine.World;
using Foster.Framework;
using Game0;
using ImGuiNET;

internal static class Program
{
    public static void Main(string[] args)
    {
        //这里设置的是窗口大小
        using var gameContent = new MyGame(new AppConfig(
            "Game",
            "Game",
            2560,
            1440, Flags: AppFlags.GraphicsDebugging));
        gameContent.Run();

    }
}


public class AppState
{
    public bool IsEditorMode;
}

public class MyGame : GameApp
{
    // 共享：模式标志 + ImGui 层。两条 pipeline 都注入 _appState，系统内也能读当前模式。
    private AppState _appState;
    private Renderer _imGui;

    // ---- Editor 态：编辑器世界，独立的 pipeline / camera / batcher ----
    private EcsEditorWorld _editorWorld;
    private EcsPipeline _editorPipeline;
    private Camera2D _editorCamera;
    private Batcher _editorBatcher;
    private AssetDatabase _assets;

    // ---- Game 态：运行时世界 ----
    private EcsDefaultWorld _world;
    private EcsEventWorld _eventWorld;
    private EcsPipeline _pipeline;
    private Batcher _batcher;
    private Camera2D _camera;

    public MyGame(in AppConfig config) : base(in config)
    {
        _appState = new AppState();
        //codepoints
        int[] codepoints = FontUtility.GetCodepoints(0, FontLanguage.SimplifiedChinese);
        //UI Font
        var storage = StorageUtils.GetDevGameRoot;
        byte[] fontData = storage.ReadAllBytes("Resources/Fonts/MapleMono-CN-Medium.ttf");
        _imGui = new Renderer(this, fontData, codepoints);
        //Game Font
        using var s = storage.OpenRead("Resources/Fonts/MapleMono-CN-Medium.ttf");
        var chineseFont = new SpriteFont(GraphicsDevice, new Font(s), size: 32, codepoints);
        
        
        _batcher = new Batcher(this.GraphicsDevice);
        _camera = new Camera2D();

        _editorBatcher = new Batcher(this.GraphicsDevice);
        _editorCamera = new Camera2D();
        _assets = new AssetDatabase();
    }

    protected override void Startup()
    {
        // 启动时扫内容目录，把磁盘上的预制体载回资产库，否则关卡加载会因查不到预制体而跳过实体。
        // 类型由这里指定（Prefab），name = 相对 "Resources" 的路径（如 Prefabs/enemy），
        // 与创作端算出的 AssetId 一致。以后加别的资产就再扫对应目录：ScanInto<LevelData>(...) 等。
        int loaded = ContentScanner.ScanInto<Prefab>(
            _assets, StorageUtils.GetDevGameRoot,
            scanDir: "Resources/Prefabs", nameRoot: "Resources", options: PrefabSerializer.Options);
        Log.Info($"Loaded {loaded} prefab(s) from Resources/Prefabs.");

        //Font Load
        
        
        
        
        // Game 世界 + pipeline
        _world = new EcsDefaultWorld();
        _eventWorld = new EcsEventWorld();

        _pipeline = EcsPipeline.New()
            .Inject(_appState)
            .Inject(_world)
            .Inject(_eventWorld)
            .Inject(_batcher)
            .Inject(_camera)
            .Inject(_imGui)
            .AddModule(new SimpleModule())
            .AddModule(new TestModule())
            .AddModule(new DebugInspectorModule())
            .Add(new EcsInspectorSystem(() => { Log.Info("Register Custom Drawer");}))
            .AutoInject()
            .BuildAndInit();

        // Editor 世界 + pipeline（各自独立的 batcher / camera）
        _editorWorld = new EcsEditorWorld();

        _editorPipeline = EcsPipeline.New()
            .Inject(_appState)
            .Inject(_editorWorld)
            .Inject(_editorBatcher)
            .Inject(_editorCamera)
            .Inject(_imGui)
            .Inject(_assets)
            .Inject(Input)      // 场景视口需要原始鼠标 / 滚轮输入
            .Inject(Window)     // 屏幕 ↔ 世界坐标换算（相机矩阵按窗口像素尺寸）
            .AddModule(new EditorModule())
            .AutoInject()
            .BuildAndInit();
    }

    protected override void Shutdown()
    {
        _pipeline?.Destroy();
        _world?.Destroy();
        _eventWorld?.Destroy();
        _editorPipeline?.Destroy();
        _editorWorld?.Destroy();
    }

    private void HandleModeToggle()
    {
        // F2 切换 Game / Editor。Pressed = 本帧按下的边沿，不会连续触发。
        if (Input.Keyboard.Pressed(Keys.F2))
        {
            _appState.IsEditorMode = !_appState.IsEditorMode;
            Log.Info(_appState.IsEditorMode ? "Switched to Editor mode" : "Switched to Game mode");
        }
    }

    // 当前激活的那一套。
    private EcsPipeline ActivePipeline => _appState.IsEditorMode ? _editorPipeline : _pipeline;
    private Batcher ActiveBatcher => _appState.IsEditorMode ? _editorBatcher : _batcher;
    private Camera2D ActiveCamera => _appState.IsEditorMode ? _editorCamera : _camera;

    protected override void Update()
    {
        HandleModeToggle();

        _imGui.BeginLayout();
        if (_imGui.WantsTextInput)
            Window.StartTextInput();
        else
            Window.StopTextInput();

        // 只更新激活的那条 pipeline，另一套完全冻结。
        ActivePipeline.Update();

        _imGui.EndLayout();
    }

    protected override void Render()
    {
        Window.Clear(_appState.IsEditorMode ? new Color(0x22, 0x26, 0x2b, 0xff) : Color.AliceBlue);

        var batcher = ActiveBatcher;
        batcher.PushMatrix(ActiveCamera.GetMatrix(Window));
        ActivePipeline.Render();
        batcher.Render(Window);
        batcher.Clear();

        _imGui.Render();
    }
}




public class SimpleModule : IEcsModule
{
    public void Import(EcsPipeline.Builder b)
    {
        b.Add(new SimpleSystem());
    }
}

public class DebugInspectorModule : IEcsModule
{
    public void Import(EcsPipeline.Builder b)
    {
        b.Add(new CameraDebugSystem());
    }
}


public class TestModule : IEcsModule
{
    public void Import(EcsPipeline.Builder b)
    {
        b.Add(new ManySystemsStressSystem());
    }
}

public class SimpleSystem : IUpdateSystem, IRenderSystem
{
    [DI] private EcsDefaultWorld _world;
    [DI] private EcsEventWorld _ecsEventWorld;
    [DI] private Batcher _batcher;
    [DI] private Camera2D _camera;
    public void Update()
    {
        Log.Info($"Udpate {_world.Name} {_ecsEventWorld.Name}");
    }

    public void Render()
    {
        _batcher.Quad(new Quad(new Rect(-1, -1, 2, 2)), Color.Black);
    }
}



public class CameraDebugSystem : IUpdateSystem
{
    [DI] private Camera2D _camera;

    public void Update()
    {
        ImGui.Begin("Camera");
        ImGui.DragFloat("Pos X", ref _camera.Position.X, 0.1f);
        ImGui.DragFloat("Pos Y", ref _camera.Position.Y, 0.1f);
        ImGui.DragFloat("Zoom", ref _camera.Zoom, 0.01f, 0.1f, 10f);
        ImGui.DragFloat("Rotation", ref _camera.Rotation, 0.01f, -MathF.PI, MathF.PI);
        ImGui.DragFloat("PPU", ref _camera.PPU, 0.5f, 1f, 128f);
        ImGui.End();
    }
}


public struct PositionComp : IEcsComponent
{
    public Vector2 Value;
}

/// <summary>绘制形状。编辑器 Render 按此选择画矩形还是圆。</summary>
public enum RenderShape { Rect, Circle }

/// <summary>
/// 可视组件：告诉编辑器 / 渲染这个实体长什么样。颜色来源于这里（不再按名字散列）。
/// Color 用 Vector4(RGBA 0..1) 便于 Inspector 里用颜色选择器编辑。
/// Size 为世界单位：Rect 是宽高，Circle 用 X 当直径。
/// </summary>
public struct RenderComp : IEcsComponent
{
    public Vector4 Color;
    public Vector2 Size;
    public RenderShape Shape;
}

/// <summary>
/// 标记"这是一个网格 Tile"：编辑器摆放时吸附到格、一格一个（覆盖）；渲染时填满整格。
/// 不带此标记的实体是"自由实体"：落在光标精确位置、可重叠、按 RenderComp.Size 画自身形状。
/// </summary>
public struct TileComp : IEcsTagComponent { }

public struct VelocityComp : IEcsComponent
{
    public Vector2 Value;
}

public struct TagAComp : IEcsTagComponent { }
public struct TagBComp : IEcsTagComponent { }

public sealed class MoverAspect : EcsAspect
    {
        public readonly EcsPool<PositionComp> Pos = B.IncludePool<EcsPool<PositionComp>>();
        public readonly EcsPool<VelocityComp> Vel = B.IncludePool<EcsPool<VelocityComp>>();
    }

public struct GroupTag<T> : IEcsTagComponent { }

public struct G0 { } public struct G1 { } public struct G2 { } public struct G3 { }
public struct G4 { } public struct G5 { } public struct G6 { } public struct G7 { }
public struct G8 { } public struct G9 { }

public sealed class GroupAspect<T> : EcsAspect where T : struct
{
    public readonly EcsPool<PositionComp> Pos = B.IncludePool<EcsPool<PositionComp>>();
    public readonly EcsPool<VelocityComp> Vel = B.IncludePool<EcsPool<VelocityComp>>();
    public readonly EcsTagPool<GroupTag<T>> Tag = B.IncludePool<EcsTagPool<GroupTag<T>>>();
}

public class ManySystemsStressSystem : IEcsPreInit, IUpdateSystem
{
    [DI] private EcsDefaultWorld _world;

    private int _noiseEntities = 50_000;
    private int _entitiesPerGroup = 100;
    private int _matched;
    private double _totalMs;
    private double _perGroupUs;

    public void PreInit() => Rebuild();

    private void Rebuild()
    {
        var span = _world.ToSpan();
        for (int i = span.Count - 1; i >= 0; i--)
            _world.DelEntity(span[i]);

        var posPool = _world.GetPool<PositionComp>();
        var velPool = _world.GetPool<VelocityComp>();
        var rng = new Random(42);

        for (int i = 0; i < _noiseEntities; i++)
        {
            var e = _world.NewEntity();
            posPool.Add(e).Value = new Vector2(rng.NextSingle(), rng.NextSingle());
            if (i % 2 == 0)
                velPool.Add(e).Value = new Vector2(rng.NextSingle(), rng.NextSingle());
        }

        AddGroup<G0>(rng); AddGroup<G1>(rng); AddGroup<G2>(rng); AddGroup<G3>(rng); AddGroup<G4>(rng);
        AddGroup<G5>(rng); AddGroup<G6>(rng); AddGroup<G7>(rng); AddGroup<G8>(rng); AddGroup<G9>(rng);
    }

    private void AddGroup<T>(Random rng) where T : struct
    {
        var posPool = _world.GetPool<PositionComp>();
        var velPool = _world.GetPool<VelocityComp>();
        var tagPool = _world.GetPool<GroupTag<T>>();

        for (int i = 0; i < _entitiesPerGroup; i++)
        {
            var e = _world.NewEntity();
            posPool.Add(e).Value = new Vector2(rng.NextSingle(), rng.NextSingle());
            velPool.Add(e).Value = new Vector2(rng.NextSingle(), rng.NextSingle());
            tagPool.Add(e);
        }
    }

    private int RunGroup<T>() where T : struct
    {
        int count = 0;
        foreach (var e in _world.Where(out GroupAspect<T> a))
        {
            ref var p = ref a.Pos.Get(e);
            ref var v = ref a.Vel.Get(e);
            p.Value += v.Value * 0.016f;
            count++;
        }
        return count;
    }

    public void Update()
    {
        var sw = Stopwatch.StartNew();
        int matched = 0;
        matched += RunGroup<G0>(); matched += RunGroup<G1>(); matched += RunGroup<G2>();
        matched += RunGroup<G3>(); matched += RunGroup<G4>(); matched += RunGroup<G5>();
        matched += RunGroup<G6>(); matched += RunGroup<G7>(); matched += RunGroup<G8>();
        matched += RunGroup<G9>();
        sw.Stop();

        _totalMs = sw.Elapsed.TotalMilliseconds;
        _perGroupUs = _totalMs * 1000.0 / 10.0;
        _matched = matched;

        ImGui.Begin("Many Systems Stress");
        ImGui.Text($"Noise Entities   : {_noiseEntities}");
        ImGui.Text($"Groups (Systems) : 10");
        ImGui.Text($"Entities per Grp : {_entitiesPerGroup}");
        ImGui.Text($"Matched Total    : {_matched}");
        ImGui.Separator();
        ImGui.Text($"Total  : {_totalMs:F3} ms");
        ImGui.Text($"Per Sys: {_perGroupUs:F2} us");
        ImGui.Text($"FPS    : {ImGui.GetIO().Framerate:F1}");
        ImGui.Separator();
        ImGui.DragInt("Noise Entities", ref _noiseEntities, 1000, 0, 1_000_000);
        ImGui.DragInt("Entities/Group", ref _entitiesPerGroup, 10, 1, 10000);
        if (ImGui.Button("Rebuild"))
            Rebuild();
        ImGui.End();
    }
}