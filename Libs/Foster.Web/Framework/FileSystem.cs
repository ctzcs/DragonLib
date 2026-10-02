namespace Foster.Framework;

public class FileSystem
{
    private readonly App app;
    internal FileSystem(App app) => this.app = app;
    private void Check() { if (app.Disposed) throw app.DisposedException; }
    public void OpenUserStorage(Action<Storage> onReady) { Check(); onReady(Storage.OpenUserStorage(app.Name)); }
    public Task<Storage> OpenUserStorageAsync() { Check(); return Task.FromResult(Storage.OpenUserStorage(app.Name)); }
    public void OpenTitleStorage(Action<Storage> onReady) => OpenTitleStorage(null, onReady);
    public void OpenTitleStorage(string? path, Action<Storage> onReady) { Check(); onReady(Storage.OpenTitleStorage(path)); }
    public Task<Storage> OpenTitleStorageAsync(string? path = null) { Check(); return Task.FromResult(Storage.OpenTitleStorage(path)); }
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
