using Prowl.Quill;
using Prowl.Scribe;
using Prowl.Scribe.Sdf;
using Prowl.Vector;
using Xunit;

namespace Paper.Msdf.Tests;

public class MsdfTests
{
    private static FontFile Font() => new(Path.Combine(AppContext.BaseDirectory, "TestFont.ttf"));

    [Theory]
    [InlineData('A')]
    [InlineData('中')]
    [InlineData('文')]
    [InlineData('g')]
    [InlineData('@')]
    public void GeneratesRealRgbFieldsWithCorrectOrientationAndExtent(char c)
    {
        var font = Font();
        int index = font.FindGlyphIndex(c);
        Assert.True(MsdfGlyphGenerator.TryGenerate(font, index, 64, 4, out var field));
        Assert.Equal(field.Width * field.Height * 4, field.Rgba.Length);
        var pixels = Enumerable.Range(0, field.Width * field.Height).ToArray();
        Assert.Contains(pixels, i => field.Rgba[4*i] != field.Rgba[4*i+1] || field.Rgba[4*i+1] != field.Rgba[4*i+2]);
        Assert.All(pixels, i => Assert.Equal(255, field.Rgba[4*i+3]));
        double scale = font.ScaleForPixelHeight(64);
        Assert.Equal(field.Width, (field.Rx1 - field.Rx0) * scale, 6);
        Assert.Equal(field.Height, (field.Ry1 - field.Ry0) * scale, 6);

        // Independent non-zero winding oracle on the original Bezier outlines.
        // Texel centers sufficiently far from the boundary must have the correct sign.
        var shape = SdfScanlineGenerator.BuildShape(font, index);
        double[] xs = new double[3]; int[] dirs = new int[3];
        int checkedPixels = 0;
        for (int y = 0; y < field.Height; y++)
        for (int x = 0; x < field.Width; x++)
        {
            int offset = 4 * (y * field.Width + x);
            int median = Median(field.Rgba[offset], field.Rgba[offset+1], field.Rgba[offset+2]);
            if (Math.Abs(median - 128) < 45) continue;
            double px = field.Rx0 + (x + .5) / scale, py = field.Ry1 - (y + .5) / scale;
            int winding = 0;
            foreach (var contour in shape.Contours)
            foreach (var edge in contour.Edges)
            {
                int n = edge.ScanlineIntersections(xs, dirs, py);
                for (int k = 0; k < n; k++) if (xs[k] <= px) winding += dirs[k];
            }
            Assert.True((median >= 128) == (winding != 0), $"Wrong sign/orientation for {c} at {x},{y}");
            checkedPixels++;
        }
        Assert.True(checkedPixels > 50);
    }

    [Fact]
    public void WhitespaceAndCacheKeepTheirLayoutSemantics()
    {
        var renderer = new RecordingRenderer();
        var system = new FontSystem(renderer);
        var font = Font();
        Assert.False(system.GetOrCreateGlyph(' ', font, FontQuality.Normal).IsInAtlas);
        var first = system.GetOrCreateGlyph('中', font, FontQuality.Normal);
        int uploads = renderer.Uploads.Count;
        Assert.Same(first, system.GetOrCreateGlyph('中', font, FontQuality.Normal));
        Assert.Equal(uploads, renderer.Uploads.Count);
        Assert.Same(first, system.GetOrCreateGlyphByIndex(font.FindGlyphIndex('中'), font, FontQuality.Normal));
        Assert.Throws<InvalidOperationException>(() => system.DistanceRange = 8);
        system.AddFallbackFont(font); // Clearing the lookup cache must not unlock an existing atlas's range.
        Assert.Throws<InvalidOperationException>(() => system.DistanceRange = 8);
    }

    [Theory]
    [InlineData(12)]
    [InlineData(24)]
    [InlineData(72)]
    public void MsdfPreservesMeasurementWrappingAndCaretPositions(int size)
    {
        var font = Font();
        var msdf = new FontSystem(new RecordingRenderer());
        var sdf = new FontSystem(new RecordingRenderer(), 512, 512, true, FontDistanceFieldMode.Sdf, 4);
        var settings = TextLayoutSettings.Default;
        settings.Font = font; settings.PixelSize = size; settings.MaxWidth = 180;
        settings.WrapMode = TextWrapMode.Wrap;
        const string text = "AV fi 中文测试\nHello world 123";
        var a = msdf.CreateLayout(text, settings);
        var b = sdf.CreateLayout(text, settings);
        Assert.Equal(b.Size, a.Size);
        Assert.Equal(b.Lines.Count, a.Lines.Count);
        for (int i = 0; i <= text.Length; i++) Assert.Equal(b.GetCursorPosition(i), a.GetCursorPosition(i));
    }

    [Fact]
    public void LayoutBuiltDuringAtlasExpansionReferencesCurrentGlyphs()
    {
        var system = new FontSystem(new RecordingRenderer(), 32, 32);
        var font = Font();
        var settings = TextLayoutSettings.Default; settings.Font = font;
        var layout = system.CreateLayout("ABCDEFGHIJKLMNOPQRSTUVWXYZ中文测试", settings);
        layout.EnsureUpToDate(system);
        Assert.True(system.AtlasVersion > 0);
        foreach (var glyph in layout.Lines.SelectMany(line => line.Glyphs))
            Assert.Same(system.GetOrCreateGlyphByIndex(glyph.Glyph.GlyphIndex, font, FontQuality.Normal), glyph.Glyph);
    }

    [Fact]
    public void ExistingPaperBinaryUsesMsdfForTextWithoutChangingControlApi()
    {
        using var renderer = new RecordingRenderer();
        var paper = new Prowl.PaperUI.Paper(renderer, 640, 480, new FontAtlasSettings { DistanceRange = 8 });
        var font = Font();
        paper.BeginFrame(1f / 60f);
        paper.Box("Label").Width(300).Height(60).Text("中文 MSDF", font).FontSize(24);
        paper.EndFrame();
        Assert.Equal(FontDistanceFieldMode.Msdf, paper.Canvas.Text.FontEngine.DistanceFieldMode);
        Assert.Equal(8, paper.Canvas.Text.FontEngine.DistanceRange);
        Assert.True(renderer.TextVertices > 0);
        Assert.Contains(renderer.Uploads, bytes => Enumerable.Range(0, bytes.Length / 4)
            .Any(i => bytes[4*i] != bytes[4*i+1] || bytes[4*i+1] != bytes[4*i+2]));
    }

    private static int Median(int r, int g, int b) => Math.Max(Math.Min(r, g), Math.Min(Math.Max(r, g), b));

    private sealed class RecordingRenderer : IFontRenderer, ICanvasRenderer
    {
        public List<byte[]> Uploads { get; } = [];
        public int TextVertices;
        public object CreateTexture(int width, int height) => new Int2(width, height);
        public object CreateTexture(uint width, uint height) => CreateTexture((int)width, (int)height);
        public Int2 GetTextureSize(object texture) => (Int2)texture;
        public void UpdateTextureRegion(object texture, AtlasRect bounds, byte[] data) => Uploads.Add(data);
        public void SetTextureData(object texture, IntRect bounds, byte[] data) => Uploads.Add(data);
        public void DrawQuads(object texture, ReadOnlySpan<IFontRenderer.Vertex> vertices, ReadOnlySpan<int> indices) { }
        public void RenderCalls(Canvas canvas, IReadOnlyList<DrawCall> drawCalls)
            => TextVertices += canvas.Vertices.Count(v => v.u >= 2);
        public void Dispose() { }
    }
}
