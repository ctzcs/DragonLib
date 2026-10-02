// Only the shared GraphicsDevice event signature and Input's desktop mapping helper
// mention SDL. No native bindings or SDL binaries are included in the Web assembly.
namespace SDL3;
internal static class SDL
{
    internal enum SDL_EventType { }
    internal static int SDL_AddGamepadMapping(string mapping)
        => throw new PlatformNotSupportedException("Web uses the browser Gamepad API; SDL mappings are not supported.");
}
