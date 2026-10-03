using System.Numerics;

namespace Foster.Framework;

public sealed class Window : IDrawableTarget
{
    private readonly App app;
    private string title;
    object? IDrawableTarget.Surface => this;
    public GraphicsDevice GraphicsDevice => app.GraphicsDevice;
    public bool IsDestroyed { get; private set; }
    public string Title { get => title; set { title = value; WebInterop.WindowSet("title", value); } }
    // The canvas has no window position or maximized state. Setting them to the only state the browser has is a
    // no-op so shared desktop code (e.g. "leave maximized, then resize") runs unchanged; real requests still throw.
    public Point2 Position { get => Point2.Zero; set { if (value != Point2.Zero) throw Unsupported("Window positioning"); } }
    public int Width { get => Size.X; set => Size = new(value, Height); }
    public int Height { get => Size.Y; set => Size = new(Width, value); }
    public Point2 Size
    {
        get => new((int)WebInterop.WindowGet("width"), (int)WebInterop.WindowGet("height"));
        set => WebInterop.WindowSet("size", $"{value.X},{value.Y}");
    }
    public int WidthInPixels => SizeInPixels.X;
    public int HeightInPixels => SizeInPixels.Y;
    public Point2 SizeInPixels => new((int)WebInterop.WindowGet("pixelWidth"), (int)WebInterop.WindowGet("pixelHeight"));
    public Point2 DisplaySize => new((int)WebInterop.WindowGet("displayWidth"), (int)WebInterop.WindowGet("displayHeight"));
    public Vector2 ContentScale => new(WidthInPixels / (float)Width, HeightInPixels / (float)Height);
    public Vector2 MousePosition => new((float)WebInterop.WindowGet("mouseX"), (float)WebInterop.WindowGet("mouseY"));
    public bool Fullscreen { get => WebInterop.WindowGet("fullscreen") != 0; set => WebInterop.WindowSet("fullscreen", value ? "1" : "0"); }
    public bool Resizable { get => WebInterop.WindowGet("resizable") != 0; set => WebInterop.WindowSet("resizable", value ? "1" : "0"); }
    public bool Maximized { get => false; set { if (value) throw Unsupported("Window maximization"); } }
    public bool Focused => WebInterop.WindowGet("focused") != 0;
    public event Action? OnFocusGain, OnFocusLost, OnMouseEnter, OnMouseLeave, OnResize, OnRestore, OnMaximize, OnMinimize, OnFullscreenEnter, OnFullscreenExit;
    public Action? OnCloseRequested;
    public Window(App app, string title, int width, int height, bool fullscreen = false, bool resizable = true)
    {
        if (app.Windows.Count != 0) throw Unsupported("Multiple windows");
        this.app = app;
        this.title = title;
        app.WindowCreated(this);
        // Fullscreen requests are deferred by JS until a user gesture.
        if (fullscreen) Fullscreen = true;
    }
    public void Destroy() { if (!IsDestroyed) app.Exit(); }
    internal void Destroyed() => IsDestroyed = true;
    public void SetMouseVisible(bool enabled) => app.SetMouseVisible(enabled);
    public void SetMouseCursor(Cursor? cursor) => app.SetMouseCursor(cursor);
    public void SetMouseRelativeMode(bool enabled) => WebInterop.WindowSet("relative", enabled ? "1" : "0");
    public void SetMousePosition(Vector2 position) => throw Unsupported("Mouse warping");
    public void SetTextInput(bool enabled) => WebInterop.WindowSet("textInput", enabled ? "1" : "0");
    public void StartTextInput() => SetTextInput(true);
    public void StopTextInput() => SetTextInput(false);
    public void Focus() => WebInterop.WindowSet("focus", "1");
    internal void Show() => WebInterop.WindowSet("visible", "1");
    internal void Hide() => WebInterop.WindowSet("visible", "0");
    internal void Notify(string type)
    {
        switch (type)
        {
            case "focus": OnFocusGain?.Invoke(); break;
            case "blur": OnFocusLost?.Invoke(); break;
            case "enter": OnMouseEnter?.Invoke(); break;
            case "leave": OnMouseLeave?.Invoke(); break;
            case "resize": OnResize?.Invoke(); break;
            case "restore": OnRestore?.Invoke(); break;
            case "maximize": OnMaximize?.Invoke(); break;
            case "background": OnMinimize?.Invoke(); break;
            case "fullscreen": if (Fullscreen) OnFullscreenEnter?.Invoke(); else OnFullscreenExit?.Invoke(); break;
            case "quit": if (OnCloseRequested != null) OnCloseRequested(); else app.Exit(); break;
        }
    }
    private static Exception Unsupported(string feature) => new PlatformNotSupportedException($"{feature} is not available in a browser.");
}
