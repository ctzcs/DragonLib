using Foster.Framework;

namespace Engine;

public abstract class GameApp : App{

    private CliConsole? cli;

    public GameApp(in AppConfig config) : base(in config)
    {
    }

    /// <summary>CLI 调试控制台(EnableCli 之后可用)。</summary>
    public CliConsole? Cli => cli;

    /// <summary>判断命令行参数是否存在(不区分大小写)。</summary>
    public static bool HasArg(string[] args, string name)
        => args.Contains(name, StringComparer.OrdinalIgnoreCase);

    /// <summary>按命令行参数调整 AppConfig:--no-focus 让窗口显示时不抢前台焦点。</summary>
    public static AppConfig ApplyCliArgs(in AppConfig config, string[] args)
        => HasArg(args, "--no-focus")
            ? config with { Flags = config.Flags | AppFlags.NoWindowFocus }
            : config;

    /// <summary>启动 CLI 后台调试控制台(stdin 读命令,主线程执行,stdout 输出)。</summary>
    public CliConsole EnableCli()
    {
        cli ??= new CliConsole(this);
        cli.Start();
        return cli;
    }
}