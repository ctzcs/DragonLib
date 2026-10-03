using System.Numerics;
using Foster.Framework;

namespace Engine.Rendering;

public enum TonemapMode { Aces, Reinhard }

/// <summary>在线性 HDR 目标与屏幕之间压缩动态范围；sRGB 目标由附件转换，调用时传 outputSrgb=false。</summary>
public sealed class Tonemapper3D : IDisposable
{
    private readonly EmbeddedShaderMaterial _shader;
    public TonemapMode Mode { get; set; } = TonemapMode.Aces;
    public float Exposure { get; set; } = 1f;

    public Tonemapper3D(GraphicsDevice device)
    {
        _shader = EmbeddedShaderMaterial.Load(device, typeof(Tonemapper3D).Assembly, "Engine/Shaders/Tonemap",
            new ShaderStageSpec(1, 1, "fragment_main"), new ShaderStageSpec(0, 1, "vertex_main"));
    }

    internal void Push(Batcher batcher, bool outputSrgb)
    {
        _shader.Material.Fragment.SetUniformBuffer(new Vector4(MathF.Max(0f, Exposure), (float)Mode, outputSrgb ? 1f : 0f, 0f));
        batcher.PushMaterial(_shader.Material);
    }

    public static float LinearToSrgb(float value) => value <= 0.0031308f ? 12.92f * value : 1.055f * MathF.Pow(value, 1f / 2.4f) - 0.055f;
    public static float SrgbToLinear(float value) => value <= 0.04045f ? value / 12.92f : MathF.Pow((value + 0.055f) / 1.055f, 2.4f);
    public static float Map(float value, float exposure = 1f, TonemapMode mode = TonemapMode.Aces)
    {
        var x = MathF.Max(0f, value * exposure);
        return mode == TonemapMode.Reinhard ? x / (1f + x) : Math.Clamp((x * (2.51f * x + 0.03f)) / (x * (2.43f * x + 0.59f) + 0.14f), 0f, 1f);
    }
    public void Dispose() => _shader.Dispose();
}
