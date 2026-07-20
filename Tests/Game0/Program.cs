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
using Game0;
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
    private SpriteAtlas _uiAtlas = null!;
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
    private SceneRouter<RuntimeScene> _sceneRouter;

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
        _uiAtlas = KenneyXmlAtlasSource.Load(GraphicsDevice, storage,
            "Resources/SpriteSheets/uipack_rpg_sheet.png",
            "Resources/SpriteSheets/uipack_rpg_sheet.xml");

        _batcher = new Batcher(this.GraphicsDevice);
        _camera = new Camera2D();
        _sceneRouter = new SceneRouter<RuntimeScene>(RuntimeScene.Main);

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
            .Inject(_sceneRouter)
            .Inject(Input)
            .Inject(_imGui)
            .Inject(_paper)
            .Inject(_paperFont)
            .Inject(_uiAtlas)
            .Inject(this)
            .AddModule(new SimpleModule())
            .AddModule(new SceneModule())
            .AddModule(new GameMenuModule())
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
        _uiAtlas?.Dispose();
        _paperRenderer?.Dispose();
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
        if (!_appState.IsEditorMode)
            _sceneRouter.Update(Time.Delta);
        ActivePipeline.Update();

        _imGui.EndLayout();
    }

    protected override void Render()
    {
        var clearColor = _appState.IsEditorMode
            ? new Color(0x22, 0x26, 0x2b, 0xff)
            : _sceneRouter.Current == RuntimeScene.DreamBlockShader
                ? new Color(0x08, 0x0c, 0x14, 0xff)
                : Color.AliceBlue;
        Window.Clear(clearColor);

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

public class GameMenuModule : IEcsModule
{
    public void Import(EcsPipeline.Builder b)
    {
        b.Add(new GameMenuSystem());
    }
}

/// <summary>
/// Paper 测试用的游戏菜单：Esc 开关，主菜单（返回游戏 / 设置 / 退出），
/// 设置页可调分辨率（真实改窗口）、主音量（可拖拽滑块，仅 UI 值）、全屏。
/// </summary>
public class GameMenuSystem : IUpdateSystem
{
    [DI] private Paper _paper = null!;
    [DI] private FontFile _font = null!;
    [DI] private MyGame _game = null!;   // 读 Window / Input / Exit
    [DI] private SpriteAtlas _atlas = null!;
    [DI] private SceneRouter<RuntimeScene> _sceneRouter = null!;

    private GraphicsDevice Gpu => _game.GraphicsDevice;

    private enum MenuPage { Closed, Main, Settings }

    private static readonly (int W, int H)[] Resolutions =
    [
        (1280, 720),
        (1920, 1080),
        (2560, 1440),
    ];

    // 设置页的“暂存值”：点“应用”才真正写入窗口。
    private int _pendingResIndex = 2;      // 默认 2560x1440，与启动一致
    private bool _pendingFullscreen;
    private float _volume = 0.8f;          // 0..1，仅 UI（TODO: 接 Audio.Volume）

    // 按钮 hover 震动：记录“上一帧是否 hover”和“本次 hover 触发时刻”，用于一次性衰减抖动。
    private readonly Dictionary<string, bool> _btnHoverPrev = new();
    private readonly Dictionary<string, float> _btnHoverTime = new();

    // ---- 页面过渡（主菜单 <-> 设置）：顺序式，旧页滑出淡出后，新页滑入淡入 ----
    private MenuPage _displayPage = MenuPage.Closed;  // 当前实际绘制的页
    private MenuPage? _queuedPage;                     // 等旧页退场后要切到的页
    private float _pageT;                              // 0=页面完全就位，1=完全退场
    private float _slideSign = 1f;                     // 退场滑动方向：+1 向右，-1 向左
    private bool _leaving;                             // 本帧面板是在退场还是入场
    private float _backdropT;                          // 遮罩显隐进度（整菜单开关时才动）
    private const float TransitionDuration = 0.18f;    // 单程时长（秒）
    private const float SlideDistance = 60f;           // 横向滑动像素

    public void Update()
    {
        if (_sceneRouter.Current != RuntimeScene.Main)
            return;

        HandleToggle();

        // 关键：AnimateBool 把动画状态存在“当前元素”上，而根元素每帧重建、其存储会被
        // EndFrame 清理，导致状态不跨帧、动画不推进。因此必须先进入一个「稳定 string id」的
        // 持久容器（MenuRoot 每帧同 id => 同 ID => 存储跨帧保留），在其内部再调 AnimateBool。
        // MenuRoot 始终绘制（即使菜单关闭），以维持动画存储；关闭且动画归零时内部不画东西。
        using (_paper.Column("MenuRoot")
                   .Width(_paper.Stretch())
                   .Height(_paper.Stretch())
                   .Enter())
        {
            UpdateTransition();

            // 菜单完全关闭且过渡结束 => 不绘制任何面板（但 MenuRoot 仍在，保住存储）。
            if (_displayPage == MenuPage.Closed && _backdropT <= 0.001f)
                return;

            switch (_displayPage)
            {
                case MenuPage.Main: DrawOverlay("MainPanel", 380, DrawMainMenu); break;
                case MenuPage.Settings: DrawOverlay("SettingsPanel", 470, DrawSettings); break;
                case MenuPage.Closed:
                    // 关闭中但遮罩还在淡出：只画遮罩，不画面板。
                    DrawBackdropOnly();
                    break;
            }
        }
    }

    // 请求切到某页。Closed 也是合法目标（关闭菜单）。
    // 若当前有页在显示，先让它退场（_queuedPage 记住目标），退场完成后在 UpdateTransition 里交换。
    private void GoToPage(MenuPage target)
    {
        // 方向：往“更深”的设置页 => 旧页向左退、新页从右来（-1）；返回/关闭 => 反向（+1）。
        _slideSign = target == MenuPage.Settings ? -1f : 1f;

        if (_displayPage == MenuPage.Closed)
            _displayPage = target;          // 无页在显示，直接成为当前页（AnimateBool 会把它滑入）
        else if (_displayPage != target)
            _queuedPage = target;           // 有页在显示，排队等它退场
    }

    // 用 Paper 的 AnimateBool 驱动进度。两条独立动画：
    // _pageT   —— 面板滑动+淡出（换页、开关都用）；
    // _backdropT —— 遮罩显隐，只在“整菜单开/关”时变化，换页时保持常亮，避免中途闪一下。
    private void UpdateTransition()
    {
        _leaving = _queuedPage.HasValue || _displayPage == MenuPage.Closed;
        _pageT = _paper.AnimateBool(_leaving, TransitionDuration, Easing.CubicInOut, id: "MenuPageT");

        var menuVisible = EffectivePage != MenuPage.Closed;
        _backdropT = _paper.AnimateBool(menuVisible, TransitionDuration, Easing.CubicInOut, id: "MenuBackdrop");

        // 诊断：值在 0/1 之间变化时打印，确认动画在推进。
        if (_backdropT > 0.001f && _backdropT < 0.999f || _pageT > 0.001f && _pageT < 0.999f)
            Log.Info($"[MenuAnim] pageT={_pageT:F3} backdropT={_backdropT:F3} disp={_displayPage} queued={_queuedPage} dt={_paper.DeltaTime:F4}");

        // 旧页退到位 => 交换到排队页；下一帧 _leaving 变 false，新页自动滑入。
        if (_queuedPage.HasValue && _pageT > 0.98f)
        {
            _displayPage = _queuedPage.Value;
            _queuedPage = null;
        }
    }

    private void HandleToggle()
    {
        // Esc 边沿（Pressed = 本帧按下）。菜单开着 => 关；关着 => 开主菜单。
        // 设置页时 Esc 先回主菜单（带过渡）。
        if (!_game.Input.Keyboard.Pressed(Keys.Escape))
            return;

        if (_displayPage == MenuPage.Closed && !_queuedPage.HasValue)
            GoToPage(MenuPage.Main);
        else if (EffectivePage == MenuPage.Settings)
            GoToPage(MenuPage.Main);
        else
            GoToPage(MenuPage.Closed);
    }

    // 正在退场时“意图中的页”是排队页，否则是当前显示页。
    private MenuPage EffectivePage => _queuedPage ?? _displayPage;

    // 半透明遮罩 + 居中面板容器。面板用固定高度（panelHeight），这样上下 Stretch spacer
    // 只负责把它压到垂直居中，而不会随分辨率缩放面板本身、导致内容溢出。
    // 过渡：面板按 _pageT 做横向滑动 + 整体淡入淡出（淡出靠 _uiAlpha 乘到所有颜色上）。
    private void DrawOverlay(string panelId, float panelHeight, Action body)
    {
        // 退场时按 _slideSign 方向滑走；入场时反向滑入。_pageT: 0=就位, 1=离场。
        var dir = _leaving ? _slideSign : -_slideSign;
        var offsetX = dir * SlideDistance * _pageT;
        var panelAlpha = 1f - _pageT;

        // 遮罩：独立的 _backdropT，换页时保持常亮；这里不走 _uiAlpha。
        _uiAlpha = 1f;
        using (_paper.Column("MenuOverlay")
                   .Width(_paper.Stretch())
                   .Height(_paper.Stretch())
                   .BackgroundColor(new PaperColor((byte)0, (byte)0, (byte)0, (byte)(160 * _backdropT)))
                   .Enter())
        {
            // 上留白
            _paper.Box("MenuTopSpacer").Height(_paper.Stretch());

            // 面板行：高度固定 = 面板高度，水平方向用左右 spacer 居中
            using (_paper.Row("MenuPanelRow").Height(panelHeight).Enter())
            {
                _paper.Box("MenuLeftSpacer").Width(_paper.Stretch());

                // 从这里开始，面板内所有颜色都乘 _uiAlpha 实现整体淡入淡出。
                _uiAlpha = panelAlpha;
                using (_paper.Column(panelId)
                           .Width(460)
                           .Height(panelHeight)
                           .TranslateX(offsetX)
                           .Padding(34)
                           .Enter())
                {
                    // 面板背景：Kenney panel_brown 九宫格（角不变形、边单向拉）。
                    _paper.DrawNineSlice(Gpu, _atlas, "panel_brown", 32, panelAlpha);
                    body();
                }
                _uiAlpha = 1f;

                _paper.Box("MenuRightSpacer").Width(_paper.Stretch());
            }

            // 下留白
            _paper.Box("MenuBottomSpacer").Height(_paper.Stretch());
        }
    }

    // 关闭过渡中：只画正在淡出的遮罩，不画面板。
    private void DrawBackdropOnly()
    {
        _uiAlpha = 1f;
        _paper.Box("MenuOverlay")
            .Width(_paper.Stretch())
            .Height(_paper.Stretch())
            .BackgroundColor(new PaperColor((byte)0, (byte)0, (byte)0, (byte)(160 * _backdropT)));
    }

    private void DrawMainMenu()
    {
        _paper.Box("MenuTitle")
            .Height(48)
            .Text("游戏菜单", _font)
            .TextColor(C(74, 44, 20))
            .FontSize(28)
            .Alignment(PaperAlign.MiddleCenter);

        _paper.Box("MenuSubtitle")
            .Height(28)
            .Margin(0, 0, 0, 12)
            .Text("Esc 关闭 · Paper UI 测试", _font)
            .TextColor(C(120, 90, 60))
            .FontSize(14)
            .Alignment(PaperAlign.MiddleCenter);

        MenuButton("BtnResume", "返回游戏", () => GoToPage(MenuPage.Closed));
        MenuButton("BtnSettings", "设置", () => GoToPage(MenuPage.Settings));
        MenuButton("BtnExit", "退出游戏", () => _game.Exit());
    }

    private void DrawSettings()
    {
        _paper.Box("SettingsTitle")
            .Height(44)
            .Margin(0, 0, 0, 12)
            .Text("设置", _font)
            .TextColor(C(74, 44, 20))
            .FontSize(26)
            .Alignment(PaperAlign.MiddleCenter);

        // --- 分辨率：预设档位，点击选中（暂存），用 panelInset 九宫格做底 ---
        _paper.Box("ResLabel")
            .Height(28)
            .Text("分辨率", _font)
            .TextColor(C(120, 90, 60))
            .FontSize(15)
            .Alignment(PaperAlign.MiddleLeft);

        using (_paper.Row("ResRow").Height(50).Margin(0, 0, 4, 14).Enter())
        {
            for (var i = 0; i < Resolutions.Length; i++)
            {
                var (w, h) = Resolutions[i];
                var selected = i == _pendingResIndex;
                using (_paper.Box("ResOption", i)
                           .Width(_paper.Stretch())
                           .Height(_paper.Stretch())
                           .Margin(i == 0 ? 0 : 4, i == Resolutions.Length - 1 ? 0 : 4, 0, 0)
                           .OnClick(i, (idx, _) => _pendingResIndex = idx)
                           .Enter())
                {
                    // 选中用普通 panel（凸起），未选用 panelInset（凹陷），形成明显区分。
                    _paper.DrawNineSlice(Gpu, _atlas, selected ? "panel_brown" : "panelInset_brown", 20, _uiAlpha);
                    _paper.Box("ResText", i)
                        .Width(_paper.Stretch())
                        .Height(_paper.Stretch())
                        .Text($"{w}×{h}", _font)
                        .TextColor(selected ? C(74, 44, 20) : C(120, 96, 68))
                        .FontSize(14)
                        .Alignment(PaperAlign.MiddleCenter);
                }
            }
        }

        // --- 主音量：可拖拽滑块 ---
        using (_paper.Row("VolLabelRow").Height(28).Enter())
        {
            _paper.Box("VolLabel")
                .Width(_paper.Stretch())
                .Text("主音量", _font)
                .TextColor(C(120, 90, 60))
                .FontSize(15)
                .Alignment(PaperAlign.MiddleLeft);
            _paper.Box("VolValue")
                .Width(64)
                .Text($"{(int)MathF.Round(_volume * 100)}%", _font)
                .TextColor(C(74, 44, 20))
                .FontSize(14)
                .Alignment(PaperAlign.MiddleRight);
        }

        DrawVolumeSlider();

        // --- 全屏开关：勾选框用 iconCheck 子图 ---
        using (_paper.Row("FsRow").Height(46).Margin(0, 0, 16, 18)
                   .OnClick(_ => _pendingFullscreen = !_pendingFullscreen)
                   .Enter())
        {
            _paper.Box("FsLabel")
                .Width(_paper.Stretch())
                .Text("全屏", _font)
                .TextColor(C(120, 90, 60))
                .FontSize(15)
                .Alignment(PaperAlign.MiddleLeft);

            using (_paper.Box("FsCheck")
                       .Size(38, 38)
                       .Enter())
            {
                // 底：凹陷小面板；勾：iconCheck（仅开启时画）
                _paper.DrawNineSlice(Gpu, _atlas, "panelInset_brown", 14, _uiAlpha);
                if (_pendingFullscreen)
                    using (_paper.Box("FsCheckMark").Width(_paper.Stretch()).Height(_paper.Stretch()).Padding(8).Enter())
                        _paper.DrawSprite(Gpu, _atlas, "iconCheck_bronze", _uiAlpha);
            }
        }

        // --- 底部：应用 / 返回 ---
        using (_paper.Row("SettingsFooter").Height(52).Enter())
        {
            using (_paper.Box("BtnApplyWrap").Width(_paper.Stretch()).Height(_paper.Stretch()).Margin(0, 6, 0, 0)
                       .OnClick(_ => ApplySettings()).Enter())
            {
                _paper.DrawNineSlice(Gpu, _atlas, _paper.IsParentActive ? "buttonLong_brown_pressed" : "buttonLong_brown", 14, _uiAlpha);
                _paper.Box("BtnApplyText").Width(_paper.Stretch()).Height(_paper.Stretch())
                    .Text("应用", _font).TextColor(C(74, 44, 20)).FontSize(16).Alignment(PaperAlign.MiddleCenter);
            }

            using (_paper.Box("BtnBackWrap").Width(_paper.Stretch()).Height(_paper.Stretch()).Margin(6, 0, 0, 0)
                       .OnClick(_ => GoToPage(MenuPage.Main)).Enter())
            {
                _paper.DrawNineSlice(Gpu, _atlas, _paper.IsParentActive ? "buttonLong_grey_pressed" : "buttonLong_grey", 14, _uiAlpha);
                _paper.Box("BtnBackText").Width(_paper.Stretch()).Height(_paper.Stretch())
                    .Text("返回", _font).TextColor(C(70, 70, 74)).FontSize(16).Alignment(PaperAlign.MiddleCenter);
            }
        }
    }

    // 拖拽滑块：底槽 barBack 三段式满铺，填充 barYellow 画到 _volume 比例。
    // NormalizedPosition 是指针相对元素矩形的 0..1，直接当音量。
    private void DrawVolumeSlider()
    {
        using (_paper.Box("VolTrack")
                   .Height(24)
                   .Margin(0, 0, 4, 4)
                   .OnDragging(e => _volume = Math.Clamp((float)e.NormalizedPosition.X, 0f, 1f))
                   .OnClick(e => _volume = Math.Clamp((float)e.NormalizedPosition.X, 0f, 1f))
                   .Enter())
        {
            _paper.DrawHBar(Gpu, _atlas, "barBack_horizontalLeft", "barBack_horizontalMid", "barBack_horizontalRight", 1f, _uiAlpha);
            _paper.DrawHBar(Gpu, _atlas, "barYellow_horizontalLeft", "barYellow_horizontalMid", "barYellow_horizontalRight", _volume, _uiAlpha);
        }
    }

    private void ApplySettings()
    {
        var (w, h) = Resolutions[_pendingResIndex];
        var window = _game.Window;

        window.Fullscreen = _pendingFullscreen;
        if (!_pendingFullscreen)
            window.Size = new Point2(w, h);

        // 与 MyGame.Update() 里的同步逻辑一致：Paper 跟随实际像素尺寸。
        _paper.SetResolution(window.WidthInPixels, window.HeightInPixels);

        // TODO: 音量接入音频系统时，这里写 Audio.Volume = _volume;
    }

    private void MenuButton(string id, string label, Action onClick)
    {
        // 外层容器负责探测 hover/active、画按钮九宫格背景、施加旋转震动；内层放文字。
        using (_paper.Row(id + "Wrap")
                   .Height(54)
                   .Margin(0, 0, 6, 6)
                   .Enter())
        {
            var hovered = _paper.IsParentHovered;
            var pressed = _paper.IsParentActive;

            // 只在“刚移上去”的那一帧记录触发时刻，然后播一段衰减振动，抖几下自然停。
            _btnHoverPrev.TryGetValue(id, out var prev);
            if (hovered && !prev)
                _btnHoverTime[id] = _paper.Time;
            _btnHoverPrev[id] = hovered;

            var start = _btnHoverTime.TryGetValue(id, out var t0) ? t0 : -999f;
            var elapsed = _paper.Time - start;
            var wobble = 6f * MathF.Exp(-9f * elapsed) * MathF.Sin(elapsed * 34f);

            // 按钮背景：按下换成 _pressed 子图；用九宫格保持圆角不变形。
            var sprite = pressed ? "buttonLong_brown_pressed" : "buttonLong_brown";
            _paper.DrawNineSlice(Gpu, _atlas, sprite, 14, _uiAlpha);

            // 文字层：旋转震动 + hover 时轻微提亮。按下时随 _pressed 图整体下沉 2px。
            using (_paper.Box(id)
                       .Width(_paper.Stretch())
                       .Height(_paper.Stretch())
                       .Rotate(wobble)
                       .TransformOrigin(0.5f, 0.5f)
                       .OnClick(_ => onClick())
                       .Enter())
            {
                _paper.Box(id + "Label")
                    .Width(_paper.Stretch())
                    .Height(_paper.Stretch())
                    .Margin(0, 0, pressed ? 2 : 0, 0)
                    .Text(label, _font)
                    .TextColor(hovered ? C(255, 249, 222) : C(74, 44, 20))
                    .FontSize(18)
                    .Alignment(PaperAlign.MiddleCenter);
            }
        }
    }

    // 面板整体淡入淡出用的 alpha 乘子（由过渡驱动）。所有颜色经 C()/White() 时都乘上它。
    private float _uiAlpha = 1f;

    private PaperColor C(byte r, byte g, byte b, byte a = 255) => new(r, g, b, (byte)(a * _uiAlpha));

    // 白色（受 _uiAlpha 影响），替代裸 PaperColor.White，让文字也一起淡。
    private PaperColor White() => C(255, 255, 255);
}


public class SimpleSystem : IUpdateSystem, IRenderSystem
{
    [DI] private EcsDefaultWorld _world;
    [DI] private EcsEventWorld _ecsEventWorld;
    [DI] private Batcher _batcher;
    [DI] private Camera2D _camera;
    [DI] private SceneRouter<RuntimeScene> _sceneRouter;
    public void Update()
    {
        if (_sceneRouter.Current != RuntimeScene.Main)
            return;

        Log.Info($"Udpate {_world.Name} {_ecsEventWorld.Name}");
    }

    public void Render()
    {
        if (_sceneRouter.Current != RuntimeScene.Main)
            return;

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











