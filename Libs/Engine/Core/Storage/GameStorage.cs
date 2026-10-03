using Foster.Framework;

namespace Engine;

/// <summary>
/// 游戏资源与用户数据的平台适配：游戏经 <see cref="StorageContainer"/> 读写，不直接用 System.IO，
/// 同一份代码可在桌面和 Web 上运行。
/// <list type="bullet">
/// <item>资源：桌面默认是当前目录(开发时设 RunWorkingDirectory 指向项目根，编辑器改的文件立即可见)；
/// Web 默认是 main.js 在 Main 之前预加载的 title storage(assets.json 列出的文件)。</item>
/// <item>用户数据(设置、存档、截图)：<see cref="FileSystem.OpenUserStorage"/>，桌面在 SDL 的 pref 目录
/// (%APPDATA%/应用名)，Web 在 localStorage。</item>
/// </list>
/// </summary>
public static class GameStorage
{
    /// <summary>资源存储；不需要 App，可以在创建游戏之前(读取配置、地图)使用。</summary>
    public static StorageContainer Resources { get; private set; } = OpenDefaultResources();

    /// <summary>资源存储被替换时通知(例如不依赖 Foster 的数据层需要同步自己的读取入口)。</summary>
    public static event Action<StorageContainer>? ResourcesChanged;

    /// <summary>换成其他资源存储，例如桌面发布版改用 exe 目录：<c>UseResources(StorageUtils.GetReleaseGameRoot)</c>。</summary>
    public static StorageContainer UseResources(StorageContainer storage)
    {
        ArgumentNullException.ThrowIfNull(storage);
        Resources = storage;
        ResourcesChanged?.Invoke(storage);
        return storage;
    }

    private static StorageContainer OpenDefaultResources()
    {
#if BROWSER
        return Storage.OpenTitleStorage(null);
#else
        return StorageUtils.GetDevGameRoot;
#endif
    }

    /// <summary>
    /// 打开用户存储并执行 <paramref name="use"/>，随后立即释放(Foster 要求用户存储不要长期打开)。
    /// 桌面与 Web 的存储都会立即就绪，此时同步执行并返回 true；未就绪时稍后在主线程执行，返回 false。
    /// </summary>
    public static bool WithUserStorage(App app, Action<StorageContainer> use)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(use);
        bool done = false;
        app.FileSystem.OpenUserStorage(storage => { using (storage) use(storage); done = true; });
        return done;
    }

    /// <summary>用户存储中文件的显示路径：桌面是真实路径(可在资源管理器打开)，Web 只有逻辑路径。</summary>
    public static string UserFilePath(App app, string path)
        => OperatingSystem.IsBrowser() ? path : Path.Combine(app.UserPath, path);
}
