namespace Foster.Framework;

/// <summary>
/// Application Information struct, to be provided to <seealso cref="App(in AppConfig)"/>
/// </summary>
/// <param name="ApplicationName">Application Name used for storing data and representing the Application</param>
/// <param name="WindowTitle">What to display in the Window Title</param>
/// <param name="Width">The Window Width</param>
/// <param name="Height">The Window Height</param>
/// <param name="Fullscreen">If the Window should default to Fullscreen</param>
/// <param name="Resizable">If the Window should be resizable</param>
/// <param name="UpdateMode">An optional default Update Mode to initialize the App with</param>
/// <param name="PreferredGraphicsDriver">The preferred graphics driver, or None to use the platform-default</param>
/// <param name="Flags">Optional App Initialization Flags</param>
public readonly record struct AppConfig
(
	string ApplicationName,
	string WindowTitle,
	int Width,
	int Height,
	bool Fullscreen = false,
	bool Resizable = true,
	UpdateMode? UpdateMode = null,
	GraphicsDriver PreferredGraphicsDriver = GraphicsDriver.None,
	AppFlags Flags = AppFlags.None
);

/// <summary>
/// Application-level events that will be notified through <see cref="App.OnEvent"/>
/// </summary>
public enum AppEvents
{
	/// <summary>
	/// When the Application enters the Background (usually on mobile devices)
	/// </summary>
	EnterBackground,

	/// <summary>
	/// When the Application enters the Foreground (usually on mobile devices)
	/// </summary>
	EnterForeground
}

/// <summary>
/// App Initialization Flags
/// </summary>
[Flags]
public enum AppFlags
{
	None = 0,

	/// <summary>
	/// Enabled Graphics Debugging properties and validation
	/// </summary>
	GraphicsDebugging = 1 << 0,

	/// <summary>
	/// Enables MultiSampling of the BackBuffer
	/// </summary>
	MultiSampledBackBuffer = 1 << 1,

	/// <summary>
	/// Doesn't log Foster's header (version number, gpu, SDL version, etc)
	/// </summary>
	NoHeaderLog = 1 << 2,

	/// <summary>
	/// 窗口显示时不抢占前台焦点(Windows 上等同 SW_SHOWNOACTIVATE),
	/// 用于 CLI 后台调试:窗口照常渲染,但终端保持输入焦点
	/// </summary>
	NoWindowFocus = 1 << 3,
}
