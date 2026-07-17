using System;
using System.Diagnostics;
using System.Numerics;
using DCFApixels.DragonECS;
using Engine;
using Engine.Assets;
using Engine.DearImGui;
using Engine.ECS;
using Engine.Paper;
using Engine.World;
using Foster.Framework;
using Game0.Editor;
using ImGuiNET;
using Prowl.PaperUI;
using Prowl.Quill;
using Prowl.Scribe;
using PaperColor = Prowl.Vector.Color;
using PaperAlign = Prowl.PaperUI.TextAlignment;

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

    // Paper UI（FosterCanvasRenderer + Paper）
    private FosterCanvasRenderer _paperRenderer = null!;
    private Paper _paper = null!;
    private FontFile _paperFont = null!;
    private Texture _paperDemoImage = null!;
    private Point2 _paperResolution;

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

        _paperRenderer = new FosterCanvasRenderer(this);
        _paperResolution = new Point2(Window.WidthInPixels, Window.HeightInPixels);
        _paper = new Paper(_paperRenderer, _paperResolution.X, _paperResolution.Y, new FontAtlasSettings());
        _paperFont = new FontFile(fontData);
        _paperDemoImage = LoadPaperImage(GraphicsDevice, storage, "Resources/Images/Poster.png");

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
            .Inject(_paper)
            .Inject(_paperFont)
            .Inject(_paperDemoImage)
            .AddModule(new SimpleModule())
            .AddModule(new TestModule())
            .AddModule(new DebugInspectorModule())
            .AddModule(new PaperTestModule())
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
        _paperDemoImage?.Dispose();
        _paperRenderer?.Dispose();
    }

    private static Texture LoadPaperImage(GraphicsDevice device, LocalStorage storage, string path)
    {
        // 从磁盘读 PNG -> Foster.Image -> Texture，交给 Paper 的 .Image() 绘制。
        using var stream = storage.OpenRead(path);
        using var image = new Image(stream);
        return new Texture(device, image, name: path);
    }

    private static Texture CreatePaperDemoImage(GraphicsDevice device)
    {
        const int width = 96;
        const int height = 56;
        var pixels = new Color[width * height];

        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var glow = 1f - Vector2.Distance(
                new Vector2(x / (float)(width - 1), y / (float)(height - 1)),
                new Vector2(0.68f, 0.42f));
            glow = Math.Clamp(glow, 0f, 1f);
            var stripe = ((x + y * 2) / 9) % 2 == 0 ? 22 : 0;
            pixels[y * width + x] = new Color(
                (byte)Math.Clamp(45 + stripe + glow * 80, 0, 255),
                (byte)Math.Clamp(55 + glow * 55, 0, 255),
                (byte)Math.Clamp(155 + stripe + glow * 75, 0, 255),
                255);
        }

        return new Texture(device, width, height, pixels, name: "Paper Demo Image");
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

        var size = new Point2(Window.WidthInPixels, Window.HeightInPixels);
        if (size != _paperResolution)
        {
            _paperResolution = size;
            _paper.SetResolution(size.X, size.Y);
        }

        PaperInput.Update(_paper, this);
        _paper.BeginFrame(Time.Delta, dpiScale: 1f);

        _imGui.BeginLayout();
        if (_imGui.WantsTextInput || _paper.WantsCaptureKeyboard)
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

        var camera = ActiveCamera;
        camera.Viewport = new Point2(Window.WidthInPixels, Window.HeightInPixels);

        var batcher = ActiveBatcher;
        batcher.PushMatrix(camera.Matrix);
        ActivePipeline.Render();
        batcher.Render(Window);
        batcher.Clear();
        _imGui.Render();
        // EndFrame 会走 FosterCanvasRenderer.RenderCalls，叠在世界 / ImGui 之上
        _paper.EndFrame();
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

public class PaperTestModule : IEcsModule
{
    public void Import(EcsPipeline.Builder b)
    {
        b.Add(new PaperTestSystem());
    }
}

public class TestModule : IEcsModule
{
    public void Import(EcsPipeline.Builder b)
    {
        b.Add(new ManySystemsStressSystem());
    }
}

/// <summary>Paper UI 综合面板示例：导航、指标卡、任务进度、活动流与交互按钮。</summary>
public class PaperTestSystem : IUpdateSystem
{
    [DI] private Paper _paper = null!;
    [DI] private FontFile _font = null!;
    [DI] private Texture _demoImage = null!;

    private static readonly string[] Sections = ["总览", "世界", "资源", "性能", "设置"];
    private static readonly (string Name, int Progress, string Status)[] Tasks =
    [
        ("烘焙导航网格", 82, "运行中"),
        ("生成资源索引", 64, "运行中"),
        ("验证关卡引用", 100, "已完成"),
    ];

    private int _selectedSection;
    private int _deployments = 12;
    private bool _autoRefresh = true;
    private string _lastAction = "等待操作";

    public void Update()
    {
        using (_paper.Column("DashboardRoot")
                   .BackgroundColor(C(10, 16, 30))
                   .Padding(24)
                   .Enter())
        {
            using (_paper.Column("DashboardShell")
                       .Width(_paper.Stretch())
                       .Height(_paper.Stretch())
                       .BackgroundColor(C(20, 28, 48))
                       .BorderColor(C(51, 65, 92))
                       .BorderWidth(1)
                       .Rounded(18)
                       .Enter())
            {
                DrawHeader();

                using (_paper.Row("DashboardBody")
                           .Width(_paper.Stretch())
                           .Height(_paper.Stretch())
                           .Padding(18)
                           .Enter())
                {
                    DrawSidebar();
                    DrawMainContent();
                }

                _paper.Box("DashboardFooter")
                    .Height(42)
                    .Padding(18, 0)
                    .BackgroundColor(C(16, 23, 40))
                    .Text($"● 系统在线    |    自动刷新：{(_autoRefresh ? "开启" : "关闭")}    |    最近操作：{_lastAction}", _font)
                    .TextColor(C(139, 158, 190))
                    .FontSize(14)
                    .Alignment(PaperAlign.MiddleLeft);
            }
        }
    }

    private void DrawHeader()
    {
        using (_paper.Row("DashboardHeader")
                   .Height(78)
                   .Padding(22, 14)
                   .BackgroundColor(C(27, 38, 64))
                   .Enter())
        {
            _paper.Box("BrandArtwork")
                .Size(140, 50)
                .Margin(0, 16, 0, 0)
                .Padding(5)
                .BackgroundColor(C(15, 23, 42))
                .Rounded(10)
                .Clip()
                .Image(_demoImage, scaleMode: ImageScaleMode.Fit);

            using (_paper.Column("BrandBlock")
                       .Width(_paper.Stretch())
                       .Enter())
            {
                _paper.Box("BrandTitle")
                    .Height(32)
                    .Text("DRAGON CONTROL CENTER", _font)
                    .TextColor(C(240, 245, 255))
                    .FontSize(24)
                    .Alignment(PaperAlign.MiddleLeft);

                _paper.Box("BrandSubtitle")
                    .Height(22)
                    .Text("Paper UI · Runtime Dashboard", _font)
                    .TextColor(C(125, 148, 184))
                    .FontSize(14)
                    .Alignment(PaperAlign.MiddleLeft);
            }

            _paper.Box("EnvironmentBadge")
                .Size(156, 42)
                .Margin(8, 8, 4, 4)
                .BackgroundColor(C(21, 94, 75))
                .Rounded(21)
                .Text("●  DEVELOPMENT", _font)
                .TextColor(C(167, 243, 208))
                .FontSize(14)
                .Alignment(PaperAlign.MiddleCenter);

            _paper.Box("DeployButton")
                .Size(166, 46)
                .Margin(8, 0, 2, 2)
                .BackgroundColor(C(79, 70, 229))
                .Rounded(10)
                .Text("发布新版本", _font)
                .TextColor(PaperColor.White)
                .FontSize(16)
                .Alignment(PaperAlign.MiddleCenter)
                .Hovered.BackgroundColor(C(99, 102, 241)).End()
                .Active.BackgroundColor(C(67, 56, 202)).End()
                .OnClick(_ =>
                {
                    _deployments++;
                    _lastAction = $"创建发布 #{_deployments}";
                });
        }
    }

    private void DrawSidebar()
    {
        using (_paper.Column("DashboardSidebar")
                   .Width(220)
                   .Height(_paper.Stretch())
                   .Padding(12)
                   .BackgroundColor(C(16, 24, 42))
                   .Rounded(12)
                   .Enter())
        {
            _paper.Box("NavigationLabel")
                .Height(38)
                .Padding(12, 0)
                .Text("工作空间", _font)
                .TextColor(C(105, 126, 160))
                .FontSize(13)
                .Alignment(PaperAlign.MiddleLeft);

            for (var i = 0; i < Sections.Length; i++)
                DrawNavigationItem(i);

            _paper.Box("SidebarSpacer").Height(_paper.Stretch());

            using (_paper.Column("RuntimeCard")
                       .Height(142)
                       .Padding(14)
                       .BackgroundColor(C(28, 41, 67))
                       .BorderColor(C(48, 64, 94))
                       .BorderWidth(1)
                       .Rounded(10)
                       .Enter())
            {
                _paper.Box("RuntimeTitle")
                    .Height(28)
                    .Text("运行时状态", _font)
                    .TextColor(C(226, 232, 240))
                    .FontSize(15)
                    .Alignment(PaperAlign.MiddleLeft);
                _paper.Box("RuntimeFps")
                    .Height(28)
                    .Text("FPS                         60", _font)
                    .TextColor(C(134, 239, 172))
                    .FontSize(14)
                    .Alignment(PaperAlign.MiddleLeft);
                _paper.Box("RuntimeMemory")
                    .Height(28)
                    .Text("内存                    384 MB", _font)
                    .TextColor(C(148, 163, 184))
                    .FontSize(14)
                    .Alignment(PaperAlign.MiddleLeft);
                _paper.Box("RuntimeEntities")
                    .Height(28)
                    .Text("实体                     51,000", _font)
                    .TextColor(C(148, 163, 184))
                    .FontSize(14)
                    .Alignment(PaperAlign.MiddleLeft);
            }
        }
    }

    private void DrawNavigationItem(int index)
    {
        var selected = index == _selectedSection;
        _paper.Box("NavigationItem", index)
            .Height(46)
            .Margin(0, 0, 3, 3)
            .Padding(14, 0)
            .BackgroundColor(selected ? C(67, 56, 202) : C(16, 24, 42))
            .Rounded(8)
            .Text($"{(selected ? "●" : "○")}   {Sections[index]}", _font)
            .TextColor(selected ? PaperColor.White : C(148, 163, 184))
            .FontSize(15)
            .Alignment(PaperAlign.MiddleLeft)
            .Hovered.BackgroundColor(selected ? C(79, 70, 229) : C(35, 48, 73)).End()
            .Active.BackgroundColor(C(55, 48, 163)).End()
            .OnClick(index, (section, _) =>
            {
                _selectedSection = section;
                _lastAction = $"切换到{Sections[section]}";
            });
    }

    private void DrawMainContent()
    {
        using (_paper.Column("DashboardMain")
                   .Width(_paper.Stretch())
                   .Height(_paper.Stretch())
                   .Margin(18, 0, 0, 0)
                   .Enter())
        {
            using (_paper.Row("PageHeading")
                       .Height(62)
                       .Enter())
            {
                using (_paper.Column("PageHeadingText")
                           .Width(_paper.Stretch())
                           .Enter())
                {
                    _paper.Box("PageTitle")
                        .Height(34)
                        .Text(Sections[_selectedSection], _font)
                        .TextColor(C(241, 245, 249))
                        .FontSize(26)
                        .Alignment(PaperAlign.MiddleLeft);
                    _paper.Box("PageDescription")
                        .Height(24)
                        .Text("监控世界运行状态、内容流水线与构建任务", _font)
                        .TextColor(C(125, 145, 178))
                        .FontSize(14)
                        .Alignment(PaperAlign.MiddleLeft);
                }

                _paper.Box("RefreshToggle")
                    .Size(148, 40)
                    .Margin(0, 0, 8, 8)
                    .BackgroundColor(_autoRefresh ? C(21, 128, 92) : C(51, 65, 85))
                    .Rounded(20)
                    .Text(_autoRefresh ? "自动刷新  ON" : "自动刷新  OFF", _font)
                    .TextColor(PaperColor.White)
                    .FontSize(13)
                    .Alignment(PaperAlign.MiddleCenter)
                    .Hovered.BackgroundColor(_autoRefresh ? C(16, 150, 105) : C(71, 85, 105)).End()
                    .OnClick(_ =>
                    {
                        _autoRefresh = !_autoRefresh;
                        _lastAction = _autoRefresh ? "开启自动刷新" : "暂停自动刷新";
                    });
            }

            using (_paper.Row("MetricCards")
                       .Height(138)
                       .Margin(0, 0, 6, 10)
                       .Enter())
            {
                DrawMetricCard(0, "活跃实体", "51,000", "+12.4%", C(96, 165, 250));
                DrawMetricCard(1, "系统耗时", "2.84 ms", "稳定", C(52, 211, 153));
                DrawMetricCard(2, "待处理资源", "128", "-18", C(251, 191, 36));
                DrawMetricCard(3, "发布次数", _deployments.ToString(), "本周", C(167, 139, 250));
            }

            using (_paper.Row("DashboardPanels")
                       .Width(_paper.Stretch())
                       .Height(_paper.Stretch())
                       .Margin(0, 0, 8, 0)
                       .Enter())
            {
                DrawWorkColumn();
                DrawServiceColumn();
            }
        }
    }

    private void DrawMetricCard(int index, string label, string value, string delta, PaperColor accent)
    {
        using (_paper.Column("MetricCard", index)
                   .Width(_paper.Stretch())
                   .Height(_paper.Stretch())
                   .Margin(6)
                   .Padding(16)
                   .BackgroundColor(C(27, 38, 62))
                   .BorderColor(C(47, 61, 88))
                   .BorderWidth(1)
                   .Rounded(12)
                   .Hovered.BackgroundColor(C(34, 47, 74)).End()
                   .Enter())
        {
            _paper.Box("MetricLabel")
                .Height(24)
                .Text(label, _font)
                .TextColor(C(136, 153, 181))
                .FontSize(14)
                .Alignment(PaperAlign.MiddleLeft);
            _paper.Box("MetricValue")
                .Height(46)
                .Text(value, _font)
                .TextColor(C(241, 245, 249))
                .FontSize(28)
                .Alignment(PaperAlign.MiddleLeft);
            _paper.Box("MetricDelta")
                .Height(24)
                .Text($"●  {delta}", _font)
                .TextColor(accent)
                .FontSize(13)
                .Alignment(PaperAlign.MiddleLeft);
        }
    }

    private void DrawWorkColumn()
    {
        using (_paper.Column("WorkColumn")
                   .Width(_paper.Stretch())
                   .Height(_paper.Stretch())
                   .Enter())
        {
            using (_paper.Column("TaskPanel")
                       .Height(252)
                       .Padding(18)
                       .BackgroundColor(C(27, 38, 62))
                       .BorderColor(C(47, 61, 88))
                       .BorderWidth(1)
                       .Rounded(12)
                       .Enter())
            {
                _paper.Box("TaskPanelTitle")
                    .Height(34)
                    .Text("构建任务", _font)
                    .TextColor(C(235, 241, 250))
                    .FontSize(18)
                    .Alignment(PaperAlign.MiddleLeft);

                for (var i = 0; i < Tasks.Length; i++)
                    DrawTaskRow(i, Tasks[i]);
            }

            using (_paper.Column("ActivityPanel")
                       .Height(_paper.Stretch())
                       .Margin(0, 0, 14, 0)
                       .Padding(18)
                       .BackgroundColor(C(27, 38, 62))
                       .BorderColor(C(47, 61, 88))
                       .BorderWidth(1)
                       .Rounded(12)
                       .Enter())
            {
                _paper.Box("ActivityTitle")
                    .Height(34)
                    .Text("最近活动", _font)
                    .TextColor(C(235, 241, 250))
                    .FontSize(18)
                    .Alignment(PaperAlign.MiddleLeft);

                DrawActivity(0, C(52, 211, 153), "关卡 level_1 验证通过", "刚刚");
                DrawActivity(1, C(96, 165, 250), "重新加载 24 个 Prefab", "2 分钟前");
                DrawActivity(2, C(251, 191, 36), "检测到 3 个资源警告", "8 分钟前");
                DrawActivity(3, C(167, 139, 250), "ECS Pipeline 构建完成", "12 分钟前");
            }
        }
    }

    private void DrawTaskRow(int index, (string Name, int Progress, string Status) task)
    {
        using (_paper.Row("TaskRow", index)
                   .Height(54)
                   .Margin(0, 0, 3, 3)
                   .Padding(10, 0)
                   .BackgroundColor(C(22, 32, 53))
                   .Rounded(8)
                   .Enter())
        {
            _paper.Box("TaskName")
                .Width(_paper.Stretch())
                .Text(task.Name, _font)
                .TextColor(C(203, 213, 225))
                .FontSize(14)
                .Alignment(PaperAlign.MiddleLeft);

            using (_paper.Box("TaskProgressTrack")
                       .Size(190, 10)
                       .Margin(10, 10, 22, 22)
                       .BackgroundColor(C(48, 61, 84))
                       .Rounded(5)
                       .Enter())
            {
                _paper.Box("TaskProgressFill")
                    .Width(_paper.Percent(task.Progress))
                    .Height(10)
                    .BackgroundColor(task.Progress == 100 ? C(52, 211, 153) : C(96, 165, 250))
                    .Rounded(5);
            }

            _paper.Box("TaskPercent")
                .Width(52)
                .Text($"{task.Progress}%", _font)
                .TextColor(task.Progress == 100 ? C(110, 231, 183) : C(147, 197, 253))
                .FontSize(13)
                .Alignment(PaperAlign.MiddleCenter);
        }
    }

    private void DrawActivity(int index, PaperColor color, string message, string time)
    {
        using (_paper.Row("ActivityRow", index)
                   .Height(44)
                   .Enter())
        {
            _paper.Box("ActivityDot")
                .Size(10, 10)
                .Margin(2, 12, 17, 17)
                .BackgroundColor(color)
                .Rounded(5);
            _paper.Box("ActivityMessage")
                .Width(_paper.Stretch())
                .Text(message, _font)
                .TextColor(C(190, 202, 220))
                .FontSize(14)
                .Alignment(PaperAlign.MiddleLeft);
            _paper.Box("ActivityTime")
                .Width(92)
                .Text(time, _font)
                .TextColor(C(100, 116, 145))
                .FontSize(12)
                .Alignment(PaperAlign.MiddleCenter);
        }
    }

    private void DrawServiceColumn()
    {
        using (_paper.Column("ServiceColumn")
                   .Width(330)
                   .Height(_paper.Stretch())
                   .Margin(16, 0, 0, 0)
                   .Enter())
        {
            using (_paper.Column("ServicePanel")
                       .Height(304)
                       .Padding(18)
                       .BackgroundColor(C(27, 38, 62))
                       .BorderColor(C(47, 61, 88))
                       .BorderWidth(1)
                       .Rounded(12)
                       .Enter())
            {
                _paper.Box("ServiceTitle")
                    .Height(34)
                    .Text("服务健康度", _font)
                    .TextColor(C(235, 241, 250))
                    .FontSize(18)
                    .Alignment(PaperAlign.MiddleLeft);

                DrawService(0, "World Server", "正常", C(52, 211, 153));
                DrawService(1, "Asset Database", "正常", C(52, 211, 153));
                DrawService(2, "Build Worker", "繁忙", C(251, 191, 36));
                DrawService(3, "Telemetry", "正常", C(52, 211, 153));
            }

            using (_paper.Column("QuickActions")
                       .Height(_paper.Stretch())
                       .Margin(0, 0, 14, 0)
                       .Padding(18)
                       .BackgroundColor(C(27, 38, 62))
                       .BorderColor(C(47, 61, 88))
                       .BorderWidth(1)
                       .Rounded(12)
                       .Enter())
            {
                _paper.Box("QuickTitle")
                    .Height(34)
                    .Text("快捷操作", _font)
                    .TextColor(C(235, 241, 250))
                    .FontSize(18)
                    .Alignment(PaperAlign.MiddleLeft);

                DrawActionButton(0, "重新扫描内容", C(37, 99, 235));
                DrawActionButton(1, "保存运行快照", C(88, 80, 180));
                DrawActionButton(2, "清理缓存", C(159, 54, 71));
            }
        }
    }

    private void DrawService(int index, string name, string state, PaperColor color)
    {
        using (_paper.Row("ServiceRow", index)
                   .Height(50)
                   .Padding(8, 0)
                   .Enter())
        {
            _paper.Box("ServiceName")
                .Width(_paper.Stretch())
                .Text(name, _font)
                .TextColor(C(190, 202, 220))
                .FontSize(14)
                .Alignment(PaperAlign.MiddleLeft);
            _paper.Box("ServiceState")
                .Size(72, 28)
                .Margin(0, 0, 11, 11)
                .BackgroundColor(new PaperColor(color.R, color.G, color.B, 0.16f))
                .Rounded(14)
                .Text(state, _font)
                .TextColor(color)
                .FontSize(12)
                .Alignment(PaperAlign.MiddleCenter);
        }
    }

    private void DrawActionButton(int index, string label, PaperColor color)
    {
        _paper.Box("QuickActionButton", index)
            .Height(42)
            .Margin(0, 0, 5, 5)
            .BackgroundColor(color)
            .Rounded(8)
            .Text(label, _font)
            .TextColor(PaperColor.White)
            .FontSize(14)
            .Alignment(PaperAlign.MiddleCenter)
            .Hovered.BackgroundColor(new PaperColor(
                MathF.Min(color.R + 0.08f, 1f),
                MathF.Min(color.G + 0.08f, 1f),
                MathF.Min(color.B + 0.08f, 1f),
                1f)).End()
            .Active.BackgroundColor(new PaperColor(color.R * 0.8f, color.G * 0.8f, color.B * 0.8f, 1f)).End()
            .OnClick(label, (action, _) => _lastAction = action);
    }

    private static PaperColor C(byte r, byte g, byte b, byte a = 255) => new(r, g, b, a);
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
















