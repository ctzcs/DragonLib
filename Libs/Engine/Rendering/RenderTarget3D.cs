using System.Numerics;
using Foster.Framework;

namespace Engine.Rendering;

/// <summary>
/// Owns an off-screen color and depth target for 3D rendering, then composites
/// its color attachment through Foster's existing 2D batcher.
/// </summary>
public sealed class RenderTarget3D : IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly TextureFormat _depthFormat;
    private Target? _target;
    private Subtexture _color;

    public Target Target => _target ?? throw new InvalidOperationException("The 3D render target has not been sized.");
    public Texture ColorTexture => Target.Attachments[0];
    /// <summary>
    /// The depth attachment produced while rendering the 3D target.
    /// It can be bound as a shader texture after the 3D render pass ends.
    /// </summary>
    public Texture DepthTexture => Target.Attachments[1];
    public int Width => _target?.Width ?? 0;
    public int Height => _target?.Height ?? 0;

    public RenderTarget3D(GraphicsDevice graphicsDevice, TextureFormat depthFormat = TextureFormat.Depth32)
    {
        _graphicsDevice = graphicsDevice;
        // 默认 32 位深度（远距离场景 Depth16 精度不够会 z-fighting）；不支持时退回全平台可用的 Depth16。
        _depthFormat = graphicsDevice.IsTextureFormatSupported(depthFormat) ? depthFormat : TextureFormat.Depth16;
    }

    public void Resize(int width, int height)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        if (_target is { Width: var currentWidth, Height: var currentHeight } &&
            currentWidth == width && currentHeight == height)
            return;

        _target?.Dispose();
        _target = new Target(
            _graphicsDevice,
            width,
            height,
            [TextureFormat.Color, _depthFormat],
            name: "Engine 3D Target");
        _color = new Subtexture(_target.Attachments[0]);
    }

    public void Clear(Color color)
    {
        Target.Clear(color, 1f, 0, ClearMask.Color | ClearMask.Depth);
    }

    public void Composite(Batcher batcher, int width, int height)
    {
        batcher.PushMatrix(Matrix3x2.Identity, relative: false);
        try
        {
            batcher.ImageStretch(_color, new Rect(0f, 0f, width, height), Color.White);
        }
        finally
        {
            batcher.PopMatrix();
        }
    }

    public void Dispose()
    {
        _target?.Dispose();
        _target = null;
        _color = default;
    }
}
