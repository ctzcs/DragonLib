using System.Runtime.InteropServices.JavaScript;

namespace Foster.Framework;

internal static partial class WebInterop
{
    // DragonLib 扩展：由实际 WebGL 上下文和扩展结果回答颜色附件能力。
    [JSImport("textureFormatSupported", "foster-web")]
    internal static partial bool TextureFormatSupported(int format);
    [JSImport("init", "foster-web")]
    internal static partial void Init(string title, int width, int height, bool resizable, bool antialias);
    [JSImport("windowGet", "foster-web")]
    internal static partial double WindowGet(string property);
    [JSImport("windowSet", "foster-web")]
    internal static partial void WindowSet(string property, string value);
    [JSImport("pollEvents", "foster-web")]
    internal static partial string PollEvents();
    [JSImport("startLoop", "foster-web")]
    internal static partial void StartLoop();
    [JSImport("dispose", "foster-web")]
    internal static partial void Dispose();
    [JSImport("create", "foster-web")]
    internal static partial int Create(string type, string descriptor);
    [JSImport("destroy", "foster-web")]
    internal static partial void Destroy(int handle);
    [JSImport("upload", "foster-web")]
    internal static partial void Upload(int handle, [JSMarshalAs<JSType.MemoryView>] Span<byte> bytes, int offset, string region);
    [JSImport("readTexture", "foster-web")]
    internal static partial void ReadTexture(int handle, [JSMarshalAs<JSType.MemoryView>] Span<byte> bytes, string region);
    [JSImport("blit", "foster-web")]
    internal static partial void Blit(int source, string sourceRegion, int dest, string destRegion, int filter);
    [JSImport("beginDraw", "foster-web")]
    internal static partial void BeginDraw(string command);
    [JSImport("uniform", "foster-web")]
    internal static partial void Uniform(int stage, int slot, [JSMarshalAs<JSType.MemoryView>] Span<byte> bytes);
    [JSImport("draw", "foster-web")]
    internal static partial void Draw();
    [JSImport("clear", "foster-web")]
    internal static partial void Clear(int target, string colors, float depth, int stencil, int mask);
    [JSImport("storageGet", "foster-web")]
    internal static partial string? StorageGet(string key);
    [JSImport("storageSet", "foster-web")]
    internal static partial void StorageSet(string key, string? value);
    [JSImport("storageKeys", "foster-web")]
    internal static partial string StorageKeys(string prefix);
    [JSImport("clipboardGet", "foster-web")]
    internal static partial string ClipboardGet();
    [JSImport("clipboardSet", "foster-web")]
    internal static partial void ClipboardSet(string value);
}
