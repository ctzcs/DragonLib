using System.Numerics;
using System.Runtime.InteropServices.JavaScript;
using Foster.Audio;

[assembly: System.Runtime.Versioning.SupportedOSPlatform("browser")]

public static partial class AudioDemo
{
    private static Sound? tone;
    private static SoundGroup? group;
    private static SoundInstance instance;
    private static string checks = "";
    private static bool started;

    public static void Main() => Start();

    [JSExport]
    public static void Start()
    {
        if (started) return;
        Audio.Startup();
        started = true;
        Audio.Volume = 0.25f;
        Require(Math.Abs(Audio.Volume - 0.25f) < 0.001f, "master volume");
        Audio.TimePcmFrames = 12345;
        Require(Audio.TimePcmFrames == 12345, "64-bit clock");
        Audio.TimePcmFrames = 0;
        Audio.Listener.Position = new(1, 2, 3);
        Require(Audio.Listener.Position == new Vector3(1, 2, 3), "listener position");
        Audio.Listener.Position = Vector3.Zero;
        Audio.Listener.Enabled = false;
        Require(!Audio.Listener.Enabled, "listener boolean");
        Audio.Listener.Enabled = true;

        // Make a real encoded WAV so this checks native decoding, not only PCM input.
        const int rate = 48000, frames = rate * 2;
        using var bytes = new MemoryStream();
        using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, true))
        {
            writer.Write("RIFF"u8); writer.Write(36 + frames * 2); writer.Write("WAVEfmt "u8);
            writer.Write(16); writer.Write((short)1); writer.Write((short)1);
            writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
            writer.Write("data"u8); writer.Write(frames * 2);
            for (int i = 0; i < frames; i++) writer.Write((short)(Math.Sin(i * 440 * Math.Tau / rate) * 8000));
        }
        var wav = bytes.ToArray();
        var format = AudioFormat.S16;
        int channels = 0, sampleRate = 0;
        Require(Sound.TryDecode(wav, ref format, ref channels, ref sampleRate, out var decodedFrames, out var pcm)
            && decodedFrames == frames && channels == 1 && sampleRate == rate && pcm!.Length == frames * 2, "WAV decoding");
        Directory.CreateDirectory("Content");
        File.WriteAllBytes("Content/tone.wav", wav);
        foreach (var method in Enum.GetValues<SoundLoadingMethod>())
        {
            using var sound = new Sound("Content/tone.wav", method);
            var test = sound.CreateInstance();
            Require(test.Active && Math.Abs(test.Length.TotalSeconds - 2) < 0.01, $"load mode {method}");
            test.Release();
        }
        group = new SoundGroup("Music") { Volume = 0.8f, Pitch = 1 };
        tone = new Sound(wav);
        instance = tone.CreateInstance3d(new Vector3(0, 0, -1), group);
        instance.Protected = true;
        instance.Looping = true;
        instance.Cone = new SoundCone(1, 2, 0.5f);
        Require(instance.Looping && instance.Spatialized && instance.Position == new Vector3(0, 0, -1)
            && instance.Cone.OuterGain == 0.5f && Math.Abs(group.Volume - 0.8f) < 0.001f, "loop/group/spatial ABI");
        instance.Spatialized = false;
        checks = "PASS: volume, clock, listener, WAV decode, all 5 load modes, loop, group, spatial parameters";
        Audio.Update();
    }

    [JSExport] public static void Play() { if (started) instance.Play(); }
    [JSExport] public static void Pause() { if (started) instance.Pause(); }
    [JSExport] public static void Seek() { if (started) instance.CursorPcmFrames = 24000; }
    [JSExport] public static void Stream()
    {
        if (!started) return;
        instance.Release();
        tone?.Dispose();
        tone = new Sound("Content/tone.wav", SoundLoadingMethod.Stream);
        instance = tone.CreateInstance(group);
        instance.Protected = true;
        instance.Looping = true;
        instance.Play();
    }
    [JSExport] public static void Stop()
    {
        if (!started) return;
        instance.Release();
        tone?.Dispose();
        group?.Dispose();
        Audio.Shutdown();
        started = false;
    }
    [JSExport] public static string Status()
    {
        if (!started) return "Stopped; click Restart to verify cleanup and reinitialization.";
        Audio.Update();
        return $"{checks}\n{Audio.SampleRate} Hz / {Audio.Channels} channels | {tone!.LoadingMethod} | Playing: {instance.Playing} | Cursor: {instance.CursorPcmFrames} | Clock: {Audio.TimePcmFrames}";
    }
    private static void Require(bool condition, string name)
    {
        if (!condition) throw new Exception($"Audio smoke test failed: {name}");
    }
}
