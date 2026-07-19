using DCFApixels.DragonECS;
using Engine.ECS;
using Foster.Framework;
using Prowl.PaperUI;
using Prowl.Scribe;
using PaperColor = Prowl.Vector.Color;
using PaperAlign = Prowl.PaperUI.TextAlignment;

namespace Game0;

public class PaperTestModule : IEcsModule
{
    public void Import(EcsPipeline.Builder b)
    {
        b.Add(new PaperTestSystem());
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
                    .Text("FPS  60", _font)
                    .TextColor(C(134, 239, 172))
                    .FontSize(14)
                    .Alignment(PaperAlign.MiddleLeft);
                _paper.Box("RuntimeMemory")
                    .Height(28)
                    .Text("内存  384 MB", _font)
                    .TextColor(C(148, 163, 184))
                    .FontSize(14)
                    .Alignment(PaperAlign.MiddleLeft);
                _paper.Box("RuntimeEntities")
                    .Height(28)
                    .Text("实体  51,000", _font)
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
