namespace Foster.Framework;

public sealed class Cursor : IDisposable
{
    public enum SystemTypes { Default, Text, Wait, Crosshair, Progress, ResizeNWSE, ResizeNESW, ResizeHorizontal, ResizeVertical, Move, NotAllowed, Pointer, ResizeNW, ResizeN, ResizeNE, ResizeE, ResizeSE, ResizeS, ResizeSW, ResizeW }
    private static readonly string[] Names = ["default", "text", "wait", "crosshair", "progress", "nwse-resize", "nesw-resize", "ew-resize", "ns-resize", "move", "not-allowed", "pointer", "nw-resize", "n-resize", "ne-resize", "e-resize", "se-resize", "s-resize", "sw-resize", "w-resize"];
    public readonly Point2 FocusPoint;
    public readonly Point2 Size;
    public readonly SystemTypes? SystemType;
    public bool Disposed { get; private set; }
    internal string Css { get; }
    public Cursor(SystemTypes type) { SystemType = type; Css = Names[(int)type]; }
    public Cursor(Image image, Point2 focusPoint)
    {
        FocusPoint = focusPoint;
        Size = image.Size;
        Css = $"url(data:image/png;base64,{Convert.ToBase64String(image.WritePng())}) {focusPoint.X} {focusPoint.Y}, default";
    }
    public void Dispose() => Disposed = true;
}
