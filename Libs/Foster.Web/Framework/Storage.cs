using System.Text.Json;
using System.Text.RegularExpressions;

namespace Foster.Framework;

/// <summary>Preloaded title assets and per-application localStorage user files.</summary>
public sealed class Storage : StorageContainer
{
    internal static readonly Dictionary<string, byte[]> Assets = new(StringComparer.Ordinal);
    private readonly string prefix;
    private bool disposed;
    public override bool Writable { get; }
    internal bool Ready => true;
    private Storage(string prefix, bool writable) { this.prefix = prefix; Writable = writable; }
    internal static Storage OpenUserStorage(string name) => new($"foster:{Uri.EscapeDataString(name)}:", true);
    /// <summary>
    /// Web only: title assets are preloaded by main.js before Main runs, so they can be read before an App exists
    /// (e.g. to load configuration that decides how the App is created). Inside an App prefer FileSystem.OpenTitleStorage.
    /// </summary>
    public static Storage OpenTitleStorage(string? path) => new(string.IsNullOrEmpty(path) ? "" : Normalize(path) + "/", false);
    internal static string Normalize(string path)
    {
        var parts = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Any(p => p == "..")) throw new ArgumentException("Storage paths must stay within their root.");
        return string.Join('/', parts.Where(p => p != "."));
    }
    private string Key(string path) { ObjectDisposedException.ThrowIf(disposed, this); return prefix + Normalize(path); }
    private string[] Files()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return Writable ? JsonSerializer.Deserialize(WebInterop.StorageKeys(prefix), WebJson.Default.StringArray)!.Select(k => k[prefix.Length..]).ToArray()
            : Assets.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).Select(k => k[prefix.Length..]).ToArray();
    }
    public override bool FileExists(string path) => Writable ? WebInterop.StorageGet(Key(path)) != null : Assets.ContainsKey(Key(path));
    public override bool DirectoryExists(string path)
    {
        var p = Normalize(path).TrimEnd('/');
        return p.Length == 0 || Files().Any(f => f.StartsWith(p + "/", StringComparison.Ordinal));
    }
    public override Stream OpenRead(string path)
    {
        var key = Key(path);
        byte[] data = Writable ? Convert.FromBase64String(WebInterop.StorageGet(key) ?? throw new FileNotFoundException(path))
            : Assets.TryGetValue(key, out var bytes) ? bytes : throw new FileNotFoundException($"Asset '{path}' was not preloaded. Add it to assets.json.");
        return new MemoryStream(data, false);
    }
    public override Stream Create(string path)
    {
        if (!Writable) return base.Create(path);
        return new SaveStream(Key(path));
    }
    private sealed class SaveStream(string key) : MemoryStream
    {
        private bool closed;
        public override void Flush() { if (!closed) WebInterop.StorageSet(key, Convert.ToBase64String(ToArray())); }
        protected override void Dispose(bool disposing) { if (disposing && !closed) Flush(); closed = true; base.Dispose(disposing); }
    }
    public override bool CreateDirectory(string path)
    {
        if (!Writable) return base.CreateDirectory(path);
        _ = Key(path);
        // Directories are inferred from file paths; no persistent empty directories.
        return true;
    }
    public override bool Remove(string path)
    {
        if (!Writable) return base.Remove(path);
        var p = Normalize(path);
        var keys = Files().Where(f => f == p || f.StartsWith(p + "/", StringComparison.Ordinal)).ToArray();
        foreach (var f in keys) WebInterop.StorageSet(Key(f), null);
        return keys.Length != 0;
    }
    public override IEnumerable<string> EnumerateDirectory(string? path = null, string? searchPattern = null, SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        string p = Normalize(path ?? "");
        if (p.Length > 0) p += "/";
        var all = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Files().Where(f => f.StartsWith(p, StringComparison.Ordinal)))
        {
            var parts = file[p.Length..].Split('/');
            for (int i = 1; i <= parts.Length; i++)
            {
                if (searchOption == SearchOption.TopDirectoryOnly && i > 1) break;
                all.Add(p + string.Join('/', parts.Take(i)));
            }
        }
        if (string.IsNullOrEmpty(searchPattern)) return all;
        var pattern = new Regex("^" + Regex.Escape(searchPattern).Replace("\\*", ".*").Replace("\\?", ".") + "$");
        return all.Where(f => pattern.IsMatch(f[p.Length..])).ToArray();
    }
    public override void Dispose(bool disposing) => disposed = true;
}
