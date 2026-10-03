using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Foster.Framework;

/// <summary>Browser App implementation. Run returns after Startup; requestAnimationFrame drives the game.</summary>
public abstract class App : IDisposable
{
    public static readonly Version FosterVersion = typeof(App).Assembly.GetName().Version!;
    private readonly AppConfig config;
    private readonly Stopwatch timer = new();
    private readonly ConcurrentQueue<Action> actions = new();
    private readonly List<Window> windows = [];
    private TimeSpan accumulator;
    private readonly InputProviderWeb provider;
    public string Name => config.ApplicationName;
    public Time Time { get; private set; }
    public TimeSpan Now => timer.Elapsed;
    public UpdateMode UpdateMode;
    public readonly Window Window;
    public readonly ReadOnlyCollection<Window> Windows;
    public readonly Input Input;
    public readonly GraphicsDevice GraphicsDevice;
    public readonly FileSystem FileSystem;
    public bool Running { get; private set; }
    public bool Exiting { get; private set; }
    public bool Disposed { get; private set; }
    public event Action<AppEvents>? OnEvent;
    public Action? OnExitRequested;
    public string UserPath => $"/foster/{Name}/";
    internal readonly Exception NotRunningException = new("The Application is not Running");
    internal readonly Exception DisposedException = new("The Application is Disposed");

    public App(string name, int width, int height) : this(new(name, name, width, height)) { }
    public App(in AppConfig config)
    {
        if (!OperatingSystem.IsBrowser())
            throw new PlatformNotSupportedException("Reference desktop Foster for native applications.");
        if (string.IsNullOrWhiteSpace(config.ApplicationName)) throw new ArgumentException("Invalid Application Name");
        if (config.Width <= 0 || config.Height <= 0) throw new ArgumentOutOfRangeException(nameof(config));
        if (WebRuntime.Active != null) throw new InvalidOperationException("Only one browser App can exist at a time.");
        if (config.PreferredGraphicsDriver is not (GraphicsDriver.None or GraphicsDriver.WebGL))
            throw new PlatformNotSupportedException("The Web backend requires WebGL2.");
        this.config = config;
        WebInterop.Init(config.WindowTitle, config.Width, config.Height, config.Resizable,
            config.Flags.Has(AppFlags.MultiSampledBackBuffer));
        Windows = new(windows);
        UpdateMode = config.UpdateMode ?? UpdateMode.FixedStep(60);
        provider = new(this);
        Input = provider.Input;
        FileSystem = new(this);
        GraphicsDevice = new GraphicsDeviceWeb(this);
        GraphicsDevice.CreateDevice(config.Flags);
        Window = new(this, config.WindowTitle, config.Width, config.Height, config.Fullscreen, config.Resizable);
        WebRuntime.Active = this;
        if (!config.Flags.Has(AppFlags.NoHeaderLog)) Log.Info($"Foster Web {FosterVersion} / WebGL2");
    }

    protected abstract void Startup();
    protected abstract void Shutdown();
    protected abstract void Update();
    protected abstract void Render();

    public void Run()
    {
        if (Disposed) throw DisposedException;
        if (Running) throw new InvalidOperationException("Application is already running");
        Running = true;
        Exiting = false;
        Time = new();
        accumulator = TimeSpan.Zero;
        timer.Restart();
        try
        {
            provider.Poll();
            provider.Update(Time);
            Window.Show();
            Startup();
            WebInterop.StartLoop();
        }
        catch { Running = false; timer.Stop(); throw; }
    }

    internal bool Step(double seconds)
    {
        if (!Running) return false;
        try
        {
            if (Exiting) { Stop(); return false; }
            var delta = TimeSpan.FromSeconds(Math.Clamp(seconds, 0, .25));
            bool updated = false;
            void UpdateStep(TimeSpan elapsed)
            {
                updated = true;
                Time = Time.Advance(elapsed);
                provider.Poll();
                provider.Update(Time);
                FramePool.NextFrame();
                while (actions.TryDequeue(out var action)) action();
                Update();
            }
            if (UpdateMode.Mode == UpdateMode.Modes.Fixed)
            {
                if (UpdateMode.FixedTargetTime <= TimeSpan.Zero)
                    throw new InvalidOperationException("Fixed update interval must be positive.");
                accumulator += delta;
                if (accumulator > UpdateMode.FixedMaxTime) accumulator = UpdateMode.FixedMaxTime;
                while (accumulator >= UpdateMode.FixedTargetTime && !Exiting)
                {
                    accumulator -= UpdateMode.FixedTargetTime;
                    UpdateStep(UpdateMode.FixedTargetTime);
                }
            }
            else UpdateStep(delta);
            // Desktop Foster (FixedWaitEnabled) sleeps until a fixed update is due, so every Render follows an Update.
            // The browser cannot block: on >60 Hz displays or rAF jitter a frame may have no update. Skip rendering it
            // (the canvas keeps the previous frame) instead of drawing state that games only prepare in Update.
            bool render = UpdateMode.Mode != UpdateMode.Modes.Fixed || !UpdateMode.FixedWaitEnabled || updated;
            if (!Exiting && render)
            {
                Time = Time.AdvanceRenderFrame();
                Render();
                GraphicsDevice.Present();
            }
            if (Exiting) Stop();
            return Running;
        }
        catch
        {
            try { Stop(); } finally { Dispose(); }
            throw;
        }
    }

    internal void Stop()
    {
        if (!Running) return;
        try
        {
            while (actions.TryDequeue(out var action)) action();
            Shutdown();
        }
        finally { Running = false; Exiting = false; timer.Stop(); Window.Hide(); }
    }
    public void Exit() { if (Running) Exiting = true; }
    public bool IsMainThread() => true;
    public void RunOnMainThread(Action action) { if (Running) action(); else actions.Enqueue(action); }
    public void SetMouseVisible(bool enabled) => WebInterop.WindowSet("mouseVisible", enabled ? "1" : "0");
    public void SetMouseCursor(Cursor? cursor)
    {
        if (cursor?.Disposed == true) throw new ObjectDisposedException(nameof(cursor));
        WebInterop.WindowSet("cursor", cursor?.Css ?? "default");
    }
    internal void WindowCreated(Window window) => windows.Add(window);
    internal void NotifyBackground(bool background) => OnEvent?.Invoke(background ? AppEvents.EnterBackground : AppEvents.EnterForeground);
    public void Dispose()
    {
        if (Disposed) return;
        if (Running) throw new InvalidOperationException("Call Exit and wait for Shutdown before disposing the browser App.");
        GraphicsDevice.Shutdown();
        GraphicsDevice.DestroyDevice();
        Window.Destroyed();
        windows.Clear();
        actions.Clear();
        if (WebRuntime.Active == this) WebRuntime.Active = null;
        WebInterop.Dispose();
        Disposed = true;
        GC.SuppressFinalize(this);
    }
}
