using System.Runtime.InteropServices.JavaScript;

namespace Foster.Framework;

/// <summary>Exports called by foster.js. App.Run starts an asynchronous browser loop.</summary>
public static partial class WebRuntime
{
    internal static App? Active;

    [JSExport]
    public static bool Step(double seconds) => Active?.Step(seconds) ?? false;

    [JSExport]
    public static void Stop() => Active?.Stop();

    /// <summary>Preload an asset before App.Run; title storage reads these bytes synchronously.</summary>
    [JSExport]
    public static void AddAsset(string path, byte[] bytes) => Storage.Assets[Storage.Normalize(path)] = bytes;

    /// <summary>
    /// Preload a file into the WebAssembly in-memory file system (relative to the current directory),
    /// so existing System.IO code such as File.ReadAllText("Content/x.json") works unchanged.
    /// Writes there are not persisted; use user storage for saves.
    /// </summary>
    [JSExport]
    public static void AddFile(string path, byte[] bytes)
    {
        var full = Path.GetFullPath(Storage.Normalize(path));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, bytes);
    }
}
