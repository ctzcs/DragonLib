using Engine.Paper;
using Foster.Framework;
using Prowl.OrigamiUI;
using Prowl.Quill;
using Prowl.Scribe;
using Paper = Prowl.PaperUI.Paper;

// Real GPU smoke test. Writes a render-target readback and exits automatically.
// Usage: dotnet run --project Tests/Paper.Msdf.Smoke -- <output.png>
string output = Path.GetFullPath(args.Length > 0 ? args[0] : "paper-msdf.png");
float dpi = args.Length > 1 ? float.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture) : 1;
if (!float.IsFinite(dpi) || dpi < 1 || dpi > 3) throw new ArgumentOutOfRangeException(nameof(dpi));
using var app = new MsdfSmoke(output, dpi);
app.Run();

sealed class MsdfSmoke(string output, float dpi) : App(new AppConfig("PaperMsdfSmoke", "Paper MSDF smoke", 1000, 700,
    Flags: AppFlags.NoWindowFocus))
{
    private FosterCanvasRenderer renderer = null!;
    private Target target = null!;
    private Paper paper = null!;
    private FontFile font = null!;
    private int frames;
    private string value = "中文输入 / MSDF 123";

    protected override void Startup()
    {
        target = new Target(GraphicsDevice, (int)(1000 * dpi), (int)(700 * dpi));
        renderer = new FosterCanvasRenderer(GraphicsDevice, target);
        paper = new Paper(renderer, 1000, 700, new FontAtlasSettings { DistanceRange = 6 });
        font = new FontFile(Path.Combine(AppContext.BaseDirectory, "TestFont.ttf"));
        var theme = OrigamiTheme.CreateDefaults();
        theme.Font = font;
        Origami.SetTheme(theme);
    }

    protected override void Update() { }

    protected override void Render()
    {
        target.Clear(new Color(22, 26, 35, 255));
        paper.BeginFrame(1f / 60f, dpiScale: dpi);
        using (paper.Column("Root").Width(960).Height(660).Padding(24).Enter())
        {
            paper.Box("Title").Height(75).Text("Paper UI · 全控件 MSDF", font).FontSize(42);
            paper.Box("Small").Height(32).Text("12 px: 中文字体 / AVMW fi @ 0123456789", font).FontSize(12);
            paper.Box("Body").Height(42).Text("24 px: 中文字体 / AVMW fi @ 0123456789", font).FontSize(24);
            paper.Box("Large").Height(100).Text("尖角 AVMW 中文", font).FontSize(72);
            Origami.Button(paper, "Button", "确认 / Confirm").Show();
            Origami.TextField(paper, "Input", value, text => value = text).Show();
            paper.Box("Rich").Height(55).RichText("Normal <b>Bold</b> <color=#66ccff>彩色富文本</color>", font, font, font, font, font).FontSize(24);
            paper.Box("Wrap").Width(360).Height(120)
                .Text("自动换行与光标位置保持一致。 MSDF keeps the original font metrics and layout.", font)
                .FontSize(24).Wrap(TextWrapMode.Wrap);
        }
        paper.EndFrame();
        Window.Clear(Color.Black);
        // Read back after several frames, allowing retained UI layout to settle.
        if (++frames >= 3)
        {
            using var image = new Image(target.Width, target.Height);
            target.Attachments[0].GetData<Color>(image.Data);
            if (image.Data.ToArray().Count(c => c.R > 100 && c.G > 100 && c.B > 100) < 1000)
                throw new InvalidOperationException("GPU smoke test did not render sufficient text pixels.");
            int inputPixels = 0;
            for (int y = (int)(308 * dpi); y < (int)(330 * dpi); y++)
            for (int x = (int)(28 * dpi); x < (int)(300 * dpi); x++)
            {
                var pixel = image.Data[y * image.Width + x];
                if (pixel.R > 100 && pixel.G > 100 && pixel.B > 100) inputPixels++;
            }
            if (inputPixels < 30 * dpi)
                throw new InvalidOperationException("Input field text was clipped or missing at the requested DPI.");
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            image.WritePng(output);
            Console.WriteLine($"GPU MSDF render saved to {output}");
            Exit();
        }
    }

    protected override void Shutdown()
    {
        renderer.Dispose();
        target.Dispose();
    }
}
