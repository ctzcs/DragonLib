using Engine;
using Foster.Framework;

internal static class Program
{
    public static void Main(string[] args)
    {
        //这里设置的是窗口大小
        using var gameContent = new MyGame(new AppConfig(
            "Game",
            "Game",
            2560,
            1440,Flags:AppFlags.GraphicsDebugging));
        gameContent.Run();

    }
}


public class MyGame : GameApp
{
    public MyGame(in AppConfig config) : base(in config)
    {
    }

    protected override void Startup()
    {
        
    }

    protected override void Shutdown()
    {
        
    }

    protected override void Update()
    {
        
    }

    protected override void Render()
    {
        
    }
}