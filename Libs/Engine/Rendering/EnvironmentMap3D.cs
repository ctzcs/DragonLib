using Foster.Framework;

namespace Engine.Rendering;

/// <summary>2D 经纬度环境贴图及 roughness atlas，不要求 Foster cubemap/纹理数组扩展。</summary>
public sealed class EnvironmentMap3D : IDisposable
{
    public Texture Sky { get; }
    public Texture Diffuse { get; }
    public Texture Specular { get; }
    public Texture Brdf { get; }
    public int Width { get; }
    public int Height { get; }
    public int Levels { get; }
    public bool IsDisposed { get; private set; }
    public bool IsHdr => Sky.Format == TextureFormat.R16G16B16A16Float;
    public EnvironmentMap3D(GraphicsDevice device, EnvironmentBake3D bake)
    {
        ArgumentNullException.ThrowIfNull(bake);
        Width = bake.Width; Height = bake.Height; Levels = bake.Levels;
        try
        {
            Sky = Upload(device, bake.Source, Width, Height, "Environment sky");
            Diffuse = Upload(device, bake.Diffuse, Width, Height, "Environment diffuse");
            Specular = Upload(device, bake.SpecularAtlas, Width, bake.AtlasHeight, "Environment GGX atlas");
            Brdf = Upload(device, bake.BrdfLut, bake.LutSize, bake.LutSize, "Environment BRDF LUT");
        }
        catch { Dispose(); throw; }
    }
    private static Texture Upload(GraphicsDevice device, System.Numerics.Vector3[] pixels, int width, int height, string name)
    {
        var format = device.IsTextureFormatSupported(TextureFormat.R16G16B16A16Float) ? TextureFormat.R16G16B16A16Float : TextureFormat.Color;
        var texture = new Texture(device, width, height, format, name: name);
        try
        {
            if (format == TextureFormat.R16G16B16A16Float)
            {
                var packed = new Half[pixels.Length * 4];
                for (var i = 0; i < pixels.Length; i++)
                {
                    packed[i * 4] = (Half)MathF.Min(65504, pixels[i].X);
                    packed[i * 4 + 1] = (Half)MathF.Min(65504, pixels[i].Y);
                    packed[i * 4 + 2] = (Half)MathF.Min(65504, pixels[i].Z);
                    packed[i * 4 + 3] = (Half)1;
                }
                texture.SetData<Half>(packed);
            }
            else
            {
                var packed = new Color[pixels.Length];
                for (var i = 0; i < pixels.Length; i++) packed[i] = new Color(System.Numerics.Vector3.Clamp(pixels[i], System.Numerics.Vector3.Zero, System.Numerics.Vector3.One));
                texture.SetData<Color>(packed);
            }
            return texture;
        }
        catch { texture.Dispose(); throw; }
    }
    public void Dispose()
    {
        if (IsDisposed) return;
        Sky?.Dispose(); Diffuse?.Dispose(); Specular?.Dispose(); Brdf?.Dispose();
        IsDisposed = true;
    }
}
