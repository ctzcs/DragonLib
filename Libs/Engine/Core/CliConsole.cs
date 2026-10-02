using System.Text;
using Foster.Framework;

namespace Engine;

/// <summary>
/// CLI 后台调试控制台:后台线程读 stdin,命令统一转到主线程执行,输出走 Log(即 stdout)。
/// 配合 AppFlags.NoWindowFocus 使用,调试全程不用把游戏窗口切到前台。
/// </summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("browser")]
public sealed class CliConsole
{
    /// <summary>命令处理函数,args 是命令名之后的剩余文本。在主线程上执行。</summary>
    public delegate void CommandHandler(string args);

    private readonly App app;
    private readonly Dictionary<string, (string description, CommandHandler handler)> commands = new(StringComparer.OrdinalIgnoreCase);
    private Thread? thread;
    private volatile bool running;

    public CliConsole(App app)
    {
        this.app = app;
        Register("help", "列出全部命令", _ => PrintHelp());
        Register("quit", "退出程序", _ => app.Exit());
        Register("history", "输出完整日志历史", _ => Log.Info(Log.GetHistory()));
    }

    /// <summary>注册命令。需在 Start 前或主线程里注册(注册表无锁)。</summary>
    public void Register(string name, string description, CommandHandler handler)
        => commands[name] = (description, handler);

    /// <summary>启动后台读取线程。可在 App.Run 之前调用,此期间输入的命令会排队到第一帧执行。</summary>
    public void Start()
    {
        if (thread != null)
            return;
        running = true;
        thread = new Thread(Loop) { IsBackground = true, Name = "CliConsole" };
        thread.Start();
        Log.Info("[cli] CLI 调试控制台已启动,输入 help 查看命令");
    }

    /// <summary>停止读取线程(不再处理新命令)。</summary>
    public void Stop() => running = false;

    private void Loop()
    {
        while (running)
        {
            string? line;
            try
            {
                line = Console.In.ReadLine();
            }
            catch (Exception)
            {
                break; // stdin 不可用(比如无控制台的调试环境)
            }
            if (line == null)
                break; // stdin 关闭(管道结束等)
            line = line.Trim();
            if (line.Length == 0)
                continue;

            var sep = line.IndexOf(' ');
            var name = sep < 0 ? line : line[..sep];
            var args = sep < 0 ? "" : line[(sep + 1)..].Trim();

            if (!commands.TryGetValue(name, out var cmd))
            {
                Log.Warning($"[cli] 未知命令 '{name}',输入 help 查看命令列表");
                continue;
            }

            // 转到主线程执行,handler 里可以安全读写游戏状态
            app.RunOnMainThread(() =>
            {
                try
                {
                    cmd.handler(args);
                }
                catch (Exception e)
                {
                    Log.Error($"[cli] 命令 '{name}' 执行失败: {e.Message}");
                }
            });
        }
    }

    private void PrintHelp()
    {
        var sb = new StringBuilder("[cli] 命令列表:");
        foreach (var (name, (description, _)) in commands)
            sb.Append($"\n  {name,-12} {description}");
        Log.Info(sb.ToString());
    }
}
