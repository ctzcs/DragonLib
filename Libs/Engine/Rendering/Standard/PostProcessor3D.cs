using System.Numerics;
using System.Runtime.InteropServices;
using Engine.World;
using Foster.Framework;

namespace Engine.Rendering;

public enum FogMode3D { Off, Linear, Exponential, ExponentialSquared }

/// <summary>场景后处理：深度 AO/雾 → HDR Bloom → tonemap → FXAA；UI 在其后绘制。</summary>
public sealed class PostProcessor3D : IDisposable
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct DepthUniforms
    {
        public Matrix4x4 InverseProjection;
        public Vector4 FogColor, Fog, Ao, Viewport;
    }
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct BloomUniforms { public Vector4 Blur, Extract; }
    private readonly GraphicsDevice _device;
    private readonly Batcher _batch;
    private readonly EmbeddedShaderMaterial _depth, _bloom, _composite, _fxaa;
    private readonly Texture _black;
    private Target? _depthOutput, _bloomA, _bloomB, _ldr, _aa;
    private bool _disposed;
    public bool FxaaEnabled { get; set; } = true;
    public bool BloomEnabled { get; set; }
    public float BloomThreshold { get; set; } = 1;
    public float BloomSoftKnee { get; set; } = .5f;
    public float BloomIntensity { get; set; } = .25f;
    public bool SsaoEnabled { get; set; }
    public float SsaoRadius { get; set; } = .6f;
    public float SsaoStrength { get; set; } = 2;
    public float SsaoBias { get; set; } = .02f;
    public FogMode3D FogMode { get; set; }
    /// <summary>线性空间的雾色；LDR 输入也会解码后混合。</summary>
    public Vector3 FogColor { get; set; } = new(.35f, .42f, .55f);
    public float FogStart { get; set; } = 8;
    public float FogEnd { get; set; } = 30;
    public float FogDensity { get; set; } = .035f;
    public float Exposure { get; set; } = 1;
    public TonemapMode TonemapMode { get; set; }

    public PostProcessor3D(GraphicsDevice device)
    {
        _device = device;
        _batch = new Batcher(device);
        _depth = Load("PostDepth", 2);
        _bloom = Load("PostBloom", 1);
        _composite = Load("PostComposite", 2);
        _fxaa = Load("PostFxaa", 1);
        _black = new Texture(device, 1, 1, [Color.Black], name: "Post black");
    }
    private EmbeddedShaderMaterial Load(string name, int samplers) => EmbeddedShaderMaterial.Load(_device,
        typeof(PostProcessor3D).Assembly, $"Engine/Shaders/{name}",
        new ShaderStageSpec(samplers, 1, "fragment_main"), new ShaderStageSpec(0, 1, "vertex_main"));

    /// <summary>返回本对象拥有的 gamma 编码 Color 贴图；下次处理/Resize/Dispose 后不可继续使用。</summary>
    public Texture Process(RenderTarget3D scene, Camera3D camera)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var width = scene.Width; var height = scene.Height;
        if (width <= 0 || height <= 0) throw new ArgumentException("Scene target must be sized.", nameof(scene));
        camera.ViewportSize = new(width, height);
        var source = scene.ColorTexture;
        if (SsaoEnabled || FogMode != FogMode3D.Off)
        {
            Resize(ref _depthOutput, width, height, source.Format);
            if (!Matrix4x4.Invert(camera.Projection, out var inverse)) throw new InvalidOperationException("Camera projection is singular.");
            _depth.Material.Fragment.SetUniformBuffer(new DepthUniforms
            {
                InverseProjection = inverse, FogColor = new Vector4(Vector3.Max(FogColor, Vector3.Zero), 1),
                Fog = new Vector4((float)FogMode, FogStart, MathF.Max(FogStart + .001f, FogEnd), MathF.Max(0, FogDensity)),
                Ao = new Vector4(SsaoEnabled ? 1 : 0, MathF.Max(.001f, SsaoRadius), MathF.Max(0, SsaoStrength), MathF.Max(0, SsaoBias)),
                Viewport = new Vector4(width, height, camera.Projection.M22, scene.IsHdr ? 1 : 0),
            });
            _depth.Material.Fragment.Samplers[1] = new BoundSampler(scene.DepthTexture, new TextureSampler(TextureFilter.Nearest, TextureWrap.Clamp));
            Pass(source, _depthOutput!, _depth.Material);
            source = _depthOutput!.Attachments[0];
        }
        Texture bloom = _black;
        if (BloomEnabled && scene.IsHdr)
        {
            var bloomWidth = Math.Max(1, width / 4); var bloomHeight = Math.Max(1, height / 4);
            Resize(ref _bloomA, bloomWidth, bloomHeight, source.Format);
            Resize(ref _bloomB, bloomWidth, bloomHeight, source.Format);
            _bloom.Material.Fragment.SetUniformBuffer(new BloomUniforms
            {
                Blur = new Vector4(0, 0, MathF.Max(0, BloomThreshold), 1),
                Extract = new Vector4(1f / width, 1f / height, Math.Clamp(BloomSoftKnee, 0, 1), 0),
            });
            Pass(source, _bloomA!, _bloom.Material);
            _bloom.Material.Fragment.SetUniformBuffer(new BloomUniforms { Blur = new Vector4(1f / bloomWidth, 0, 0, 0) });
            Pass(_bloomA!.Attachments[0], _bloomB!, _bloom.Material);
            _bloom.Material.Fragment.SetUniformBuffer(new BloomUniforms { Blur = new Vector4(0, 1f / bloomHeight, 0, 0) });
            Pass(_bloomB!.Attachments[0], _bloomA, _bloom.Material);
            bloom = _bloomA.Attachments[0];
        }
        Resize(ref _ldr, width, height, TextureFormat.Color);
        _composite.Material.Fragment.SetUniformBuffer(new Vector4(MathF.Max(0, Exposure), (float)TonemapMode,
            scene.IsHdr ? 1 : 0, BloomEnabled && scene.IsHdr ? MathF.Max(0, BloomIntensity) : 0));
        _composite.Material.Fragment.Samplers[1] = new BoundSampler(bloom, new TextureSampler(TextureFilter.Linear, TextureWrap.Clamp));
        Pass(source, _ldr!, _composite.Material);
        if (!FxaaEnabled) return _ldr!.Attachments[0];
        Resize(ref _aa, width, height, TextureFormat.Color);
        _fxaa.Material.Fragment.SetUniformBuffer(new Vector4(1f / width, 1f / height, 0, 0));
        Pass(_ldr!.Attachments[0], _aa!, _fxaa.Material);
        return _aa!.Attachments[0];
    }

    public void Composite(RenderTarget3D scene, Camera3D camera, Batcher batcher, int width, int height)
    {
        var result = Process(scene, camera);
        batcher.PushMatrix(Matrix3x2.Identity, relative: false);
        try { batcher.ImageStretch(new Subtexture(result), new Rect(0, 0, width, height), Color.White); }
        finally { batcher.PopMatrix(); }
    }

    private void Pass(Texture source, Target output, Material material)
    {
        _batch.Clear();
        _batch.PushMaterial(material);
        _batch.PushSampler(new TextureSampler(TextureFilter.Linear, TextureWrap.Clamp));
        _batch.PushBlend(new BlendMode(BlendOp.Add, BlendFactor.One, BlendFactor.Zero));
        try
        {
            _batch.ImageStretch(new Subtexture(source), new Rect(0, 0, output.Width, output.Height), Color.White);
            _batch.Render(output);
        }
        finally { _batch.PopBlend(); _batch.PopSampler(); _batch.PopMaterial(); _batch.Clear(); }
    }
    private void Resize(ref Target? target, int width, int height, TextureFormat format)
    {
        if (target != null && target.Width == width && target.Height == height && target.Attachments[0].Format == format) return;
        target?.Dispose();
        target = new Target(_device, width, height, [format], name: "3D post process");
    }
    public void Dispose()
    {
        if (_disposed) return;
        _depthOutput?.Dispose(); _bloomA?.Dispose(); _bloomB?.Dispose(); _ldr?.Dispose(); _aa?.Dispose();
        _black.Dispose(); _depth.Dispose(); _bloom.Dispose(); _composite.Dispose(); _fxaa.Dispose(); _batch.Dispose();
        _disposed = true;
    }
}
