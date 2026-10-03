namespace Foster.Framework;

public class FileSystem
{
    private readonly App app;
    internal FileSystem(App app) => this.app = app;
    private void Check() { if (app.Disposed) throw app.DisposedException; }
    public void OpenUserStorage(Action<ContentStorage> onReady) { Check(); onReady(ContentStorage.OpenUserStorage(app.Name)); }
    public Task<ContentStorage> OpenUserStorageAsync() { Check(); return Task.FromResult(ContentStorage.OpenUserStorage(app.Name)); }
    public void OpenTitleStorage(Action<ContentStorage> onReady) => OpenTitleStorage(null, onReady);
    public void OpenTitleStorage(string? path, Action<ContentStorage> onReady) { Check(); onReady(ContentStorage.OpenTitleStorage(path)); }
    public Task<ContentStorage> OpenTitleStorageAsync(string? path = null) { Check(); return Task.FromResult(ContentStorage.OpenTitleStorage(path)); }
    public enum DialogResult { Success, Cancelled, Failed }
    public delegate void DialogCallback(string[] paths, DialogResult result);
    public delegate void DialogCallbackSingleFile(string path, DialogResult result);
    public readonly record struct DialogFilter(string Name, string Pattern);
    private static Exception DialogUnsupported() => new PlatformNotSupportedException("Native file dialogs are unavailable; use a browser file input or download.");
    public void OpenFileDialog(DialogCallback callback, bool allowMany = false) => throw DialogUnsupported();
    public void OpenFileDialog(DialogCallback callback, DialogFilter[] filters, string? defaultLocation = null, bool allowMany = false) => throw DialogUnsupported();
    public void OpenFolderDialog(DialogCallback callback, bool allowMany = false) => throw DialogUnsupported();
    public void OpenFolderDialog(DialogCallback callback, string? defaultLocation = null, bool allowMany = false) => throw DialogUnsupported();
    public void SaveFileDialog(DialogCallbackSingleFile callback) => throw DialogUnsupported();
    public void SaveFileDialog(DialogCallbackSingleFile callback, DialogFilter[] filters, string? defaultLocation = null) => throw DialogUnsupported();
}
