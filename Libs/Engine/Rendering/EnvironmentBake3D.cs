using System.Numerics;

namespace Engine.Rendering;

/// <summary>无 GPU/ECS 依赖的环境预计算。RGB 输入与输出都是线性辐射亮度。</summary>
public sealed class EnvironmentBake3D
{
    public int Width { get; }
    public int Height { get; }
    public int Levels { get; }
    public int LutSize { get; }
    public Vector3[] Source { get; }
    public Vector3[] Diffuse { get; }
    public Vector3[] SpecularAtlas { get; }
    public Vector3[] BrdfLut { get; }
    public int AtlasHeight => (Height + 2) * Levels;
    private EnvironmentBake3D(int width, int height, int levels, int lutSize)
    {
        Width = width; Height = height; Levels = levels; LutSize = lutSize;
        Source = new Vector3[width * height]; Diffuse = new Vector3[width * height];
        SpecularAtlas = new Vector3[width * AtlasHeight]; BrdfLut = new Vector3[lutSize * lutSize];
    }

    public static EnvironmentBake3D Bake(ReadOnlySpan<Vector3> linearRgb, int width, int height,
        int levels = 6, int samples = 64, int lutSize = 64)
    {
        ValidateDimensions(width, height, levels, lutSize);
        if (linearRgb.Length != width * height) throw new ArgumentException("Environment pixel count does not match dimensions.");
        if (samples < 8 || samples > 4096) throw new ArgumentOutOfRangeException(nameof(samples));
        var result = new EnvironmentBake3D(width, height, levels, lutSize);
        for (var i = 0; i < linearRgb.Length; i++)
        {
            var c = linearRgb[i];
            if (!float.IsFinite(c.X) || !float.IsFinite(c.Y) || !float.IsFinite(c.Z) || Vector3.Min(c, Vector3.Zero) != Vector3.Zero)
                throw new ArgumentException("Environment radiance must be finite and nonnegative.");
            result.Source[i] = c;
        }
        for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
        {
            var normal = Direction(new Vector2((x + .5f) / width, (y + .5f) / height));
            Basis(normal, out var tangent, out var bitangent);
            var diffuse = Vector3.Zero;
            for (var s = 0; s < samples; s++)
            {
                var xi = Sequence(s, samples);
                var phi = MathF.Tau * xi.X;
                var radius = MathF.Sqrt(xi.Y);
                var direction = tangent * (MathF.Cos(phi) * radius) + bitangent * (MathF.Sin(phi) * radius) +
                    normal * MathF.Sqrt(1 - xi.Y);
                diffuse += Sample(result.Source, width, height, direction);
            }
            result.Diffuse[y * width + x] = diffuse / samples; // cos 加权采样已含 Lambert 的 1/pi。
            for (var level = 0; level < levels; level++)
            {
                var roughness = (float)level / (levels - 1);
                var radiance = Vector3.Zero; var weight = 0f;
                if (level == 0) { radiance = result.Source[y * width + x]; weight = 1; }
                else for (var s = 0; s < samples; s++)
                {
                    var hLocal = SampleGgx(Sequence(s, samples), roughness);
                    var h = tangent * hLocal.X + bitangent * hLocal.Y + normal * hLocal.Z;
                    var light = 2 * Vector3.Dot(normal, h) * h - normal;
                    var nDotL = MathF.Max(0, Vector3.Dot(normal, light));
                    if (nDotL > 0) { radiance += Sample(result.Source, width, height, light) * nDotL; weight += nDotL; }
                }
                result.SpecularAtlas[((height + 2) * level + y + 1) * width + x] = radiance / MathF.Max(weight, .00001f);
            }
        }
        // 各 roughness 层单独补边，线性过滤不能串到另一层。
        for (var level = 0; level < levels; level++)
        {
            var start = level * (height + 2) * width;
            Array.Copy(result.SpecularAtlas, start + width, result.SpecularAtlas, start, width);
            Array.Copy(result.SpecularAtlas, start + height * width, result.SpecularAtlas, start + (height + 1) * width, width);
        }
        for (var y = 0; y < lutSize; y++) for (var x = 0; x < lutSize; x++)
        {
            var nv = (x + .5f) / lutSize; var roughness = (y + .5f) / lutSize;
            var view = new Vector3(MathF.Sqrt(1 - nv * nv), 0, nv);
            var a = 0f; var b = 0f;
            for (var s = 0; s < samples; s++)
            {
                var h = SampleGgx(Sequence(s, samples), roughness);
                var vh = MathF.Max(0, Vector3.Dot(view, h));
                var l = 2 * vh * h - view;
                if (l.Z <= 0) continue;
                var k = roughness * roughness * .5f;
                float G(float n) => n / (n * (1 - k) + k);
                var visibility = G(nv) * G(l.Z) * vh / MathF.Max(h.Z * nv, .00001f);
                var fresnel = MathF.Pow(1 - vh, 5);
                a += (1 - fresnel) * visibility; b += fresnel * visibility;
            }
            result.BrdfLut[y * lutSize + x] = new Vector3(a / samples, b / samples, 0);
        }
        return result;
    }

    public static EnvironmentBake3D CreateSky(int width = 64, int height = 32, int samples = 64)
    {
        ValidateDimensions(width, height, 6, 64);
        var pixels = new Vector3[width * height];
        var sun = Vector3.Normalize(new Vector3(-.5f, .45f, -.7f));
        for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
        {
            var d = Direction(new Vector2((x + .5f) / width, (y + .5f) / height));
            var color = d.Y >= 0 ? Vector3.Lerp(new Vector3(.8f, .9f, 1), new Vector3(.12f, .3f, .65f), d.Y) :
                Vector3.Lerp(new Vector3(.5f, .48f, .4f), new Vector3(.07f, .06f, .05f), -d.Y);
            pixels[y * width + x] = color + new Vector3(18, 15, 10) * MathF.Pow(MathF.Max(0, Vector3.Dot(d, sun)), 256);
        }
        return Bake(pixels, width, height, samples: samples);
    }

    public static Vector2 Uv(Vector3 direction)
    {
        direction = Vector3.Normalize(direction);
        return new Vector2(MathF.Atan2(direction.Z, direction.X) / MathF.Tau + .5f,
            MathF.Acos(Math.Clamp(direction.Y, -1, 1)) / MathF.PI);
    }
    public static Vector3 Direction(Vector2 uv)
    {
        var phi = (uv.X - .5f) * MathF.Tau; var theta = uv.Y * MathF.PI;
        return new Vector3(MathF.Cos(phi) * MathF.Sin(theta), MathF.Cos(theta), MathF.Sin(phi) * MathF.Sin(theta));
    }
    private static Vector3 Sample(Vector3[] pixels, int width, int height, Vector3 direction)
    {
        var uv = Uv(direction); var fx = uv.X * width - .5f; var fy = uv.Y * height - .5f;
        var x = (int)MathF.Floor(fx); var y = (int)MathF.Floor(fy);
        Vector3 P(int xx, int yy) => pixels[Math.Clamp(yy, 0, height - 1) * width + (xx % width + width) % width];
        return Vector3.Lerp(Vector3.Lerp(P(x, y), P(x + 1, y), fx - x),
            Vector3.Lerp(P(x, y + 1), P(x + 1, y + 1), fx - x), fy - y);
    }
    private static Vector2 Sequence(int i, int count)
    {
        uint bits = (uint)i;
        bits = bits << 16 | bits >> 16;
        bits = (bits & 0x55555555) << 1 | (bits & 0xAAAAAAAA) >> 1;
        bits = (bits & 0x33333333) << 2 | (bits & 0xCCCCCCCC) >> 2;
        bits = (bits & 0x0F0F0F0F) << 4 | (bits & 0xF0F0F0F0) >> 4;
        bits = (bits & 0x00FF00FF) << 8 | (bits & 0xFF00FF00) >> 8;
        return new Vector2((float)i / count, bits * 2.3283064365386963e-10f);
    }
    private static Vector3 SampleGgx(Vector2 xi, float roughness)
    {
        var alpha = roughness * roughness;
        var z = MathF.Sqrt((1 - xi.Y) / (1 + (alpha * alpha - 1) * xi.Y));
        var radius = MathF.Sqrt(MathF.Max(0, 1 - z * z)); var phi = MathF.Tau * xi.X;
        return new Vector3(MathF.Cos(phi) * radius, MathF.Sin(phi) * radius, z);
    }
    private static void Basis(Vector3 n, out Vector3 tangent, out Vector3 bitangent)
    {
        tangent = Vector3.Normalize(Vector3.Cross(MathF.Abs(n.Y) < .99f ? Vector3.UnitY : Vector3.UnitX, n));
        bitangent = Vector3.Cross(n, tangent);
    }
    private static void ValidateDimensions(int width, int height, int levels, int lutSize)
    {
        if (width < 2 || height < 2 || width > 2048 || height > 1024 || levels < 2 || levels > 16 || lutSize < 2 || lutSize > 512)
            throw new ArgumentOutOfRangeException(nameof(width), "Invalid environment dimensions or bake settings.");
        if ((height + 2) * levels > 8192 || (long)width * (height * 2 + (height + 2) * levels) + (long)lutSize * lutSize > 16_777_216)
            throw new ArgumentOutOfRangeException(nameof(width), "Environment atlas or pixel budget exceeded.");
    }

    /// <summary>离线保存预计算结果，运行时只上传纹理；流由调用者拥有。</summary>
    public void Write(Stream stream)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(0x564E4544); writer.Write(1); writer.Write(Width); writer.Write(Height); writer.Write(Levels); writer.Write(LutSize);
        foreach (var pixels in new[] { Source, Diffuse, SpecularAtlas, BrdfLut })
            foreach (var p in pixels) { writer.Write(p.X); writer.Write(p.Y); writer.Write(p.Z); }
    }
    public static EnvironmentBake3D Read(Stream stream)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        if (reader.ReadInt32() != 0x564E4544 || reader.ReadInt32() != 1) throw new InvalidDataException("Invalid .denv header.");
        var width = reader.ReadInt32(); var height = reader.ReadInt32(); var levels = reader.ReadInt32(); var lutSize = reader.ReadInt32();
        ValidateDimensions(width, height, levels, lutSize);
        var pixelCount = (long)width * (height * 2 + (height + 2) * levels) + (long)lutSize * lutSize;
        if (stream.CanSeek && stream.Length - stream.Position < pixelCount * 12)
            throw new InvalidDataException("Truncated .denv data.");
        var result = new EnvironmentBake3D(width, height, levels, lutSize);
        foreach (var pixels in new[] { result.Source, result.Diffuse, result.SpecularAtlas, result.BrdfLut })
            for (var i = 0; i < pixels.Length; i++)
            {
                var p = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                if (!float.IsFinite(p.X) || !float.IsFinite(p.Y) || !float.IsFinite(p.Z) || Vector3.Min(p, Vector3.Zero) != Vector3.Zero)
                    throw new InvalidDataException("Invalid environment radiance.");
                pixels[i] = p;
            }
        return result;
    }
}
