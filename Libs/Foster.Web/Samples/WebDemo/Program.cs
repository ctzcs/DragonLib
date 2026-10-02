using System.Numerics;
using System.Runtime.InteropServices.JavaScript;
using Foster.Framework;
using System.Runtime.Versioning;

[assembly: SupportedOSPlatform("browser")]

// Keep the instance alive. Do not use `using var app`: Run returns immediately on Web.
var game = new WebDemo();
game.Run();

public partial class WebDemo : App
{
    private Batcher batch = null!;
    private Target preview = null!;
    private SpriteFont font = null!;
    private Texture checker = null!;
    private Vector2 position = new(350, 300);
    private int saves;
    private long frames;
    private static WebDemo? instance;
    private string verification = "starting";
    public WebDemo() : base(new AppConfig("FosterWebDemo", "Foster WebGL2", 960, 600)) => instance = this;
    protected override void Startup()
    {
        batch = new(GraphicsDevice);
        preview = new(GraphicsDevice, 160, 100);
        font = GraphicsDevice.Defaults.SpriteFont;
        FileSystem.OpenTitleStorage(storage => {
            using (storage)
            using (var image = new Image(storage.ReadAllBytes("Content/checker.qoi")))
            {
                if (image.Width != 4 || image.Height != 4 || image[0, 0] != Color.White)
                    throw new Exception("Preloaded title asset failed");
                checker = new(GraphicsDevice, image);
            }
        });
        FileSystem.OpenUserStorage(storage => {
            using (storage) if (storage.FileExists("saves.txt")) int.TryParse(storage.ReadAllText("saves.txt"), out saves);
        });
        Verify();
    }
    private void Verify()
    {
        using var original = new Image(2, 2, Color.White);
        original[0, 0] = Color.Red;
        using var png = new Image(original.WritePng());
        using var qoi = new Image(original.WriteQoi());
        if (!png.Data.SequenceEqual(original.Data) || !qoi.Data.SequenceEqual(original.Data)) throw new Exception("Image codec roundtrip failed");
        preview.Clear(Color.Black);
        batch.Rect(0, 0, 160, 50, Color.Red);
        batch.Rect(0, 50, 160, 50, Color.Blue);
        batch.Render(preview);
        var pixels = new Color[160 * 100];
        preview.Attachments[0].GetData<Color>(pixels);
        if (pixels[10 * 160 + 10] != Color.Red || pixels[80 * 160 + 10] != Color.Blue) throw new Exception("Target orientation/readback failed");
        batch.Clear();
        batch.Rect(0, 0, 160, 100, Color.Green);
        batch.Render(preview, scissor: new RectInt(20, 20, 30, 30));
        preview.Attachments[0].GetData<Color>(pixels);
        if (pixels[25 * 160 + 25] != Color.Green || pixels[10 * 160 + 10] != Color.Red) throw new Exception("Target scissor failed");
        batch.Clear();
        using var copy = new Texture(GraphicsDevice, 160, 100);
        preview.Attachments[0].Blit(copy, TextureFilter.Nearest);
        var copied = new Color[pixels.Length]; copy.GetData<Color>(copied);
        if (!copied.AsSpan().SequenceEqual(pixels)) throw new Exception("Texture blit failed");
        verification = "PASS: assets, PNG/QOI, Batcher, Target, scissor, readback, blit";
    }
    protected override void Update()
    {
        float speed = 220 * (float)Time.Delta;
        if (Input.Keyboard.Down(Keys.Left) || Input.Keyboard.Down(Keys.A)) position.X -= speed;
        if (Input.Keyboard.Down(Keys.Right) || Input.Keyboard.Down(Keys.D)) position.X += speed;
        if (Input.Keyboard.Down(Keys.Up) || Input.Keyboard.Down(Keys.W)) position.Y -= speed;
        if (Input.Keyboard.Down(Keys.Down) || Input.Keyboard.Down(Keys.S)) position.Y += speed;
        if (Input.Keyboard.Pressed(Keys.Space))
        {
            saves++;
            FileSystem.OpenUserStorage(storage => { using (storage) storage.WriteAllText("saves.txt", saves.ToString()); });
        }
        if (Input.Keyboard.Pressed(Keys.F)) Window.Fullscreen = !Window.Fullscreen;
        if (Input.Keyboard.Pressed(Keys.Escape)) Exit();
    }
    protected override void Render()
    {
        frames++;
        Window.Clear(new Color(22, 27, 40, 255));
        batch.Clear();
        batch.Rect(28, 28, 880, 135, new Color(35, 44, 62, 255));
        font.Draw(batch, "Foster / WebGL2 + .NET WASM", new Vector2(48, 48), 30, Color.White);
        font.Draw(batch, "Click canvas | WASD / arrows: move | SPACE: save | F: fullscreen | ESC: stop", new Vector2(48, 100), 18, Color.White);
        font.Draw(batch, $"Persistent saves: {saves}   Render frames: {frames}", new Vector2(48, 130), 18, Color.White);
        batch.Image(preview, new Vector2(48, 200), Color.White);
        batch.Image(checker, new Vector2(230, 200), Vector2.Zero, new Vector2(12), 0, Color.White);
        font.Draw(batch, "Target: red top / blue bottom", new Vector2(48, 315), 16, Color.White);
        batch.PushMatrix(position, new Vector2(1), Time.SecondsF);
        batch.Rect(-35, -35, 70, 70, new Color(255, 180, 70, 255));
        batch.PopMatrix();
        batch.Circle(Window.MousePosition, 12, 24, new Color(90, 220, 200, 255));
        font.Draw(batch, verification, new Vector2(48, 520), 16, Color.White);
        batch.Render(Window);
    }
    protected override void Shutdown() { checker.Dispose(); preview.Dispose(); batch.Dispose(); }
    [JSExport]
    public static string Status() => $"{instance?.verification}; frames={instance?.frames}; saves={instance?.saves}; running={instance?.Running}";
}
