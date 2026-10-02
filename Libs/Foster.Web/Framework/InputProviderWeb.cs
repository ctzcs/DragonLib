using System.Numerics;
using System.Text.Json;

namespace Foster.Framework;

internal sealed class InputProviderWeb(App app) : InputProvider
{
    public override string GetClipboard() => WebInterop.ClipboardGet();
    public override void SetClipboard(string text) => WebInterop.ClipboardSet(text);
    public override void Rumble(ControllerID id, float lowIntensity, float highIntensity, float duration)
        => throw new PlatformNotSupportedException("Browser controller vibration is not implemented.");
    internal void Poll()
    {
        using var document = JsonDocument.Parse(WebInterop.PollEvents());
        foreach (var e in document.RootElement.EnumerateArray())
        {
            string type = e.GetProperty("type").GetString()!;
            int Int(string name) => e.GetProperty(name).GetInt32();
            float Float(string name) => e.GetProperty(name).GetSingle();
            switch (type)
            {
                case "key": Key(Int("key"), e.GetProperty("down").GetBoolean(), app.Now); break;
                case "button": MouseButton(Int("button"), e.GetProperty("down").GetBoolean(), app.Now); break;
                case "move": MouseMove(new(Float("x"), Float("y")), new(Float("dx"), Float("dy")), app.Now); break;
                case "wheel": MouseWheel(new(Float("x"), Float("y"))); break;
                case "text": Text(e.GetProperty("text").GetString().AsSpan(), app.Window); break;
                case "connect": ConnectController(new((uint)Int("id")), e.GetProperty("name").GetString()!, 15, 6, true, GamepadTypes.Standard, 0, 0, 0); break;
                case "disconnect": DisconnectController(new((uint)Int("id"))); break;
                case "padButton": ControllerButton(new((uint)Int("id")), Int("button"), e.GetProperty("down").GetBoolean(), app.Now); break;
                case "padAxis": ControllerAxis(new((uint)Int("id")), Int("axis"), Float("value"), app.Now); break;
                case "background": app.NotifyBackground(true); app.Window.Notify(type); break;
                case "foreground": app.NotifyBackground(false); app.Window.Notify("restore"); break;
                case "quit": if (app.OnExitRequested != null) app.OnExitRequested(); else app.Window.Notify(type); break;
                default: app.Window.Notify(type); break;
            }
        }
    }
}
