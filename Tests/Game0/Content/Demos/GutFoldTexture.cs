using System.Numerics;
using Foster.Framework;

namespace Game0.Content.Demos;

/// <summary>A reaction-diffusion tissue field with locally oriented, winding folds.
/// Two interacting concentrations grow connected ridges and creases across the surface.
/// Their shared profile also drives contour bands, fine striations and tangent normals.</summary>
internal static class GutFoldTexture
{
    public const int Width = 1400, Height = 800;
    public const float WorldWidth = 28f, WorldHeight = 16f;
    public const float HeightRange = 4.5f;
    private const float PixelSize = WorldWidth / Width;

    public sealed record Maps(Color[] Albedo, Color[] Normal, Color[] Material, double BakeMilliseconds);

    public static Maps Generate(int seed, float spacing, float bend, float grooveWidth, float detail,
        float wallArch, float foldPuffiness,
        CancellationToken cancellation = default)
    {
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        var field = GrowFoldField(seed, spacing, bend, cancellation);
        cancellation.ThrowIfCancellationRequested();
        var heights = new float[Width * Height];
        var profiles = new float[heights.Length];
        var striations = new float[heights.Length];
        var variations = new float[heights.Length];
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            int i = x + y * Width;
            var p = World(x, y);
            float d = p.X - Boundary(p.Y);
            if (d <= 0f) continue;
            // The pattern grows as one field. There are no nearest-cell boundaries,
            // independent capsule endpoints, or holes between fixed-width strips.
            var sampleOffset = new Vector2(Fbm(p * .70f, seed) - .5f,
                Fbm(p * .70f + new Vector2(17.3f, -4.2f), seed) - .5f) * (spacing * .9f);
            float concentration = SampleField(field.Values, field.Width, field.Height,
                (p.X + sampleOffset.X + 14f) / WorldWidth,
                (p.Y + sampleOffset.Y + 8f) / WorldHeight);
            // A continuous, expansive profile fills the space between grown bands.
            // The concave response gives broad soft shoulders and narrow shared creases,
            // while keeping the concentration peaks rounded without a binary contour.
            float v = Math.Clamp(concentration / .52f, 0f, 1f);
            float exponent = .28f + .38f * grooveWidth;
            v = MathF.Pow(MathF.Sin(v * MathF.PI * .5f), exponent);
            profiles[i] = v;
            variations[i] = Fbm(p * .43f, unchecked(seed + 573));
            float bulge = v;
            // Nested contour lines follow the actual grown ridge instead of cutting
            // across it in a separate coordinate domain.
            float fine = MathF.Sin(bulge * 12.57f + .45f * Noise(p * 1.4f, seed));
            striations[i] = fine;
            float shoulder = MathF.Sin(bulge * MathF.PI);
            float tissue = .12f + foldPuffiness * (.85f + .30f * variations[i]) * bulge
                + detail * .004f * fine * shoulder;
            // A broad, softly varying arch supports the small folds. Its gradient is
            // included in normals, so the entire wall turns towards/away from a lamp.
            float arch = wallArch * MathF.Exp(-MathF.Pow((d - 5f) / 6f, 2))
                * Smooth(0f, 1.2f, d) * (.88f + .12f * MathF.Sin(p.Y * .48f + .6f * MathF.Sin(p.X * .22f)));
            float cushions = wallArch * .08f * Fbm(p * .55f, unchecked(seed + 97)) * Smooth(0f, 1f, d);
            float rim = .38f * MathF.Exp(-MathF.Pow((d - .24f) / .24f, 2));
            heights[i] = Smooth(0f, .10f, d) * (arch + cushions + tissue + rim);
        }

        // Smooth the height source before taking derivatives, keeping shared creases continuous.
        var temporary = new float[heights.Length];
        ReadOnlySpan<float> kernel = [.0625f, .25f, .375f, .25f, .0625f];
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
            for (int k = -2; k <= 2; k++)
                temporary[x + y * Width] += heights[Math.Clamp(x + k, 0, Width - 1) + y * Width] * kernel[k + 2];
        Array.Clear(heights);
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
            for (int k = -2; k <= 2; k++)
                heights[x + y * Width] += temporary[x + Math.Clamp(y + k, 0, Height - 1) * Width] * kernel[k + 2];

        var albedo = new Color[heights.Length];
        var normal = new Color[heights.Length];
        var material = new Color[heights.Length];
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            int i = x + y * Width;
            var p = World(x, y);
            float d = p.X - Boundary(p.Y);
            float mask = Smooth(0f, .10f, d);
            float v = profiles[i], h = heights[i];
            float grain = Noise(p * 9f, seed), mottling = Fbm(p * .7f, seed);
            // Let the crown carry the lighter flesh color. Bright outlines and nearly
            // black low regions made the previous surface read as engraved grooves.
            var flesh = Vector3.Lerp(new(.24f, .15f, .080f), new(.48f, .35f, .19f), Smooth(.08f, .92f, v));
            float shoulderBand = MathF.Exp(-MathF.Pow((v - .28f) / .11f, 2f));
            flesh = Vector3.Lerp(flesh, new(.38f, .29f, .145f), shoulderBand * .12f);
            flesh *= .76f + .14f * variations[i] + .18f * mottling + .08f * grain;
            flesh *= 1f - detail * .09f * Smooth(.25f, 1f, -striations[i]);
            float rim = MathF.Exp(-MathF.Pow((d - .20f) / .30f, 2));
            flesh = Vector3.Lerp(flesh, new(.62f, .36f, .065f), rim * .8f);
            albedo[i] = Encode(flesh, mask);
            float dx = (heights[Math.Min(x + 1, Width - 1) + y * Width]
                - heights[Math.Max(x - 1, 0) + y * Width]) / (2f * PixelSize);
            float dy = (heights[x + Math.Min(y + 1, Height - 1) * Width]
                - heights[x + Math.Max(y - 1, 0) * Width]) / (2f * PixelSize);
            // World Y and texture Y both point down. Positive Z faces the viewer.
            var n = Vector3.Normalize(new(-dx, -dy, 1f));
            normal[i] = Encode(n * .5f + new Vector3(.5f), 1f);
            material[i] = Encode(new(h / HeightRange, .46f + .15f * grain,
                .72f + .28f * Smooth(.02f, .70f, v)), mask);
        }
        return new(albedo, normal, material, System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    }

    private static (float[] Values, int Width, int Height) GrowFoldField(int seed, float scale, float bend,
        CancellationToken cancellation)
    {
        // Simulation resolution sets fold width. Baking at a smaller resolution keeps
        // regeneration bounded; bilinear sampling and height smoothing supply the final maps.
        int width = Math.Clamp((int)(WorldWidth / (scale * .20f)), 160, 560);
        int height = (int)(width * WorldHeight / WorldWidth);
        int size = width * height;
        var u = new float[size]; var v = new float[size];
        var nextU = new float[size]; var nextV = new float[size];
        var weights = new Vector4[size];
        var feed = new float[size]; var kill = new float[size];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int i = x + y * width;
            var p = new Vector2((x + .5f) * WorldWidth / width - 14f,
                (y + .5f) * WorldHeight / height - 8f);
            // Local active patches survive diffusion and grow into the resting tissue.
            // A nearly uniform mixture can collapse to the empty equilibrium before
            // the spatial instability has time to amplify its small perturbations.
            int seedX = x / 20, seedY = y / 20;
            var source = new Vector2((seedX + .5f + .65f * (Hash(seedX, seedY, seed) - .5f)) * 20f,
                (seedY + .5f + .65f * (Hash(seedX, seedY, unchecked(seed + 101)) - .5f)) * 20f);
            float activation = 1f - Smooth(5f, 6f, Vector2.Distance(new Vector2(x, y), source));
            u[i] = 1f - .5f * activation;
            v[i] = .25f * activation;
            float angle = bend * (.62f * MathF.Sin(p.Y * .85f + MathF.Sin(p.X * .65f))
                + .42f * MathF.Sin(p.X * 1.05f - p.Y * .45f));
            float s = MathF.Sin(angle), c = MathF.Cos(angle);
            // Positive stencil weights bias growth along a slowly turning local direction.
            weights[i] = new(.60f + .55f * s * s, .60f + .55f * c * c,
                .10f + .18f * s * c, .10f - .18f * s * c);
            float region = Fbm(p * .65f, unchecked(seed + 271));
            feed[i] = .025f + .030f * region;
            kill[i] = .055f + .010f * region;
        }
        // Gray-Scott reaction terms generate winding, branching bands. Ping-pong updates
        // avoid traversal-order bias. Periodic neighbors eliminate artificial edge borders.
        for (int step = 0; step < 1600; step++)
        {
            cancellation.ThrowIfCancellationRequested();
            for (int y = 0; y < height; y++)
            {
                int row = y * width, above = (y == 0 ? height - 1 : y - 1) * width;
                int below = (y == height - 1 ? 0 : y + 1) * width;
                for (int x = 0; x < width; x++)
                {
                    int i = row + x, left = x == 0 ? width - 1 : x - 1;
                    int right = x == width - 1 ? 0 : x + 1;
                    var w = weights[i];
                    float centerWeight = 2f * (w.X + w.Y + w.Z + w.W);
                    float lapU = w.X * (u[row + left] + u[row + right])
                        + w.Y * (u[above + x] + u[below + x])
                        + w.Z * (u[above + left] + u[below + right])
                        + w.W * (u[above + right] + u[below + left]) - centerWeight * u[i];
                    float lapV = w.X * (v[row + left] + v[row + right])
                        + w.Y * (v[above + x] + v[below + x])
                        + w.Z * (v[above + left] + v[below + right])
                        + w.W * (v[above + right] + v[below + left]) - centerWeight * v[i];
                    float reaction = u[i] * v[i] * v[i];
                    nextU[i] = Math.Clamp(u[i] + .16f * lapU - reaction + feed[i] * (1f - u[i]), 0f, 1f);
                    nextV[i] = Math.Clamp(v[i] + .07f * lapV + reaction - (feed[i] + kill[i]) * v[i], 0f, 1f);
                }
            }
            (u, nextU) = (nextU, u); (v, nextV) = (nextV, v);
        }
        return (v, width, height);
    }

    private static float SampleField(float[] values, int width, int height, float u, float v)
    {
        float px = u * width - .5f, py = v * height - .5f;
        int x = (int)MathF.Floor(px), y = (int)MathF.Floor(py);
        float tx = px - x, ty = py - y;
        int x0 = Math.Clamp(x, 0, width - 1), x1 = Math.Clamp(x + 1, 0, width - 1);
        int y0 = Math.Clamp(y, 0, height - 1), y1 = Math.Clamp(y + 1, 0, height - 1);
        return float.Lerp(float.Lerp(values[x0 + y0 * width], values[x1 + y0 * width], tx),
            float.Lerp(values[x0 + y1 * width], values[x1 + y1 * width], tx), ty);
    }

    private static float Boundary(float y) => -6f + 1.5f * MathF.Sin(y * .26f) + .16f * y;
    private static Vector2 World(int x, int y) => new((x + .5f) * PixelSize - 14f, (y + .5f) * PixelSize - 8f);
    private static float Smooth(float a, float b, float value)
    {
        float t = Math.Clamp((value - a) / (b - a), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)x * 0x8da6b343u ^ (uint)y * 0xd8163841u ^ (uint)seed * 0xcb1ab31fu;
            h = (h ^ (h >> 16)) * 0x7feb352du;
            h = (h ^ (h >> 15)) * 0x846ca68bu;
            return ((h ^ (h >> 16)) & 0xffffffu) / 16777215f;
        }
    }

    private static float Noise(Vector2 p, int seed)
    {
        int x = (int)MathF.Floor(p.X), y = (int)MathF.Floor(p.Y);
        var f = p - new Vector2(x, y);
        f = f * f * (new Vector2(3f) - 2f * f);
        float a = float.Lerp(Hash(x, y, seed), Hash(x + 1, y, seed), f.X);
        float b = float.Lerp(Hash(x, y + 1, seed), Hash(x + 1, y + 1, seed), f.X);
        return float.Lerp(a, b, f.Y);
    }

    private static float Fbm(Vector2 p, int seed) => (Noise(p, seed)
        + .5f * Noise(p * 2.03f + new Vector2(7.1f), seed)
        + .25f * Noise(p * 4.07f + new Vector2(-3.7f), seed)) / 1.75f;

    private static Color Encode(Vector3 value, float alpha) => new(
        (byte)(Math.Clamp(value.X, 0f, 1f) * 255f), (byte)(Math.Clamp(value.Y, 0f, 1f) * 255f),
        (byte)(Math.Clamp(value.Z, 0f, 1f) * 255f), (byte)(Math.Clamp(alpha, 0f, 1f) * 255f));
}
