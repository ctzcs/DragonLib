using System.Numerics;
using Engine.Rendering;
using StbImageSharp;

if (args.Length < 2)
{
    Console.WriteLine("Usage: EnvironmentCooker <input.hdr|input.png|--sky> <output.denv> [width=64] [height=32] [samples=128]");
    return 1;
}
try
{
    var width = args.Length > 2 ? int.Parse(args[2]) : 64;
    var height = args.Length > 3 ? int.Parse(args[3]) : 32;
    var samples = args.Length > 4 ? int.Parse(args[4]) : 128;
    EnvironmentBake3D bake;
    if (args[0] == "--sky") bake = EnvironmentBake3D.CreateSky(width, height, samples);
    else
    {
        if (width < 2 || width > 2048 || height < 2 || height > 1024) throw new ArgumentException("Invalid output dimensions.");
        using var source = File.OpenRead(args[0]);
        Span<byte> header = stackalloc byte[10]; source.ReadExactly(header); source.Position = 0;
        var magic = System.Text.Encoding.ASCII.GetString(header);
        ImageResultFloat image;
        if (magic.StartsWith("#?RADIANCE") || magic.StartsWith("#?RGBE"))
            image = ImageResultFloat.FromStream(source, ColorComponents.RedGreenBlue);
        else
        {
            var ldr = ImageResult.FromStream(source, ColorComponents.RedGreenBlue);
            var linear = new float[ldr.Data.Length];
            for (var i = 0; i < linear.Length; i++) linear[i] = Tonemapper3D.SrgbToLinear(ldr.Data[i] / 255f);
            image = new ImageResultFloat { Width = ldr.Width, Height = ldr.Height, Data = linear };
        }
        var pixels = new Vector3[width * height];
        // 先在线性空间重采样；Radiance 保留高亮值，LDR 显式用 sRGB 曲线而不是近似 gamma 2.2。
        for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
        {
            var sx = (x + .5f) * image.Width / width - .5f;
            var sy = (y + .5f) * image.Height / height - .5f;
            var ix = (int)MathF.Floor(sx); var iy = (int)MathF.Floor(sy);
            Vector3 Read(int px, int py)
            {
                var i = (Math.Clamp(py, 0, image.Height - 1) * image.Width + (px % image.Width + image.Width) % image.Width) * 3;
                return new Vector3(image.Data[i], image.Data[i + 1], image.Data[i + 2]);
            }
            pixels[y * width + x] = Vector3.Lerp(Vector3.Lerp(Read(ix, iy), Read(ix + 1, iy), sx - ix),
                Vector3.Lerp(Read(ix, iy + 1), Read(ix + 1, iy + 1), sx - ix), sy - iy);
        }
        bake = EnvironmentBake3D.Bake(pixels, width, height, samples: samples);
    }
    var output = Path.GetFullPath(args[1]);
    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    using (var stream = File.Create(output)) bake.Write(stream);
    Console.WriteLine($"Baked {bake.Width}x{bake.Height}, {bake.Levels} roughness levels, BRDF {bake.LutSize}: {output}");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
