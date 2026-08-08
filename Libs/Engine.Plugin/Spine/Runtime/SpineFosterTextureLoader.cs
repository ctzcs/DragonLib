using Foster.Framework;
using Spine;

namespace Engine.Spine;

/// <summary>Loads Spine atlas pages into Foster textures.</summary>
public sealed class SpineFosterTextureLoader : TextureLoader, IDisposable
{
    private readonly GraphicsDevice _device;
    private readonly Func<string, Stream> _openImage;
    private readonly bool _premultiplyAlpha;
    private readonly List<Texture> _textures = [];
    private bool _disposed;

    public SpineFosterTextureLoader(
        GraphicsDevice device,
        Func<string, Stream> openImage,
        bool premultiplyAlpha = true)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _openImage = openImage ?? throw new ArgumentNullException(nameof(openImage));
        _premultiplyAlpha = premultiplyAlpha;
    }

    public void Load(AtlasPage page, string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(page);

        using var stream = _openImage(path) ?? throw new FileNotFoundException("Spine atlas page was not found.", path);
        using var image = new Image(stream);
        if (_premultiplyAlpha && page.pma)
            image.Premultiply();

        var texture = new Texture(_device, image, $"Spine/{Path.GetFileName(path)}");
        _textures.Add(texture);
        page.rendererObject = texture;
        page.width = image.Width;
        page.height = image.Height;
    }

    public void Unload(Object texture)
    {
        if (texture is not Texture fosterTexture)
            return;

        _textures.Remove(fosterTexture);
        if (!fosterTexture.IsDisposed)
            fosterTexture.Dispose();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        foreach (var texture in _textures)
        {
            if (!texture.IsDisposed)
                texture.Dispose();
        }
        _textures.Clear();
    }
}
