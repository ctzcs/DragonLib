using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Foster.Framework;

internal sealed class GraphicsDeviceWeb(App app) : GraphicsDevice(app)
{
    private bool disposed;
    private readonly ConcurrentQueue<int> destroying = new();
    public override GraphicsDriver Driver => GraphicsDriver.WebGL;
    public override bool Disposed => disposed;
    public override bool VSync { get => true; set { if (!value) throw new PlatformNotSupportedException("Browser rendering is scheduled by requestAnimationFrame."); } }
    private static string Json<T>(T value, JsonTypeInfo<T> type) => JsonSerializer.Serialize(value, type);
    private static string Json(int[] region) => JsonSerializer.Serialize(region, WebJson.Default.Int32Array);
    private static int[] Region(RectInt r) => [r.X, r.Y, r.Width, r.Height];
    // JSImport MemoryView takes Span<byte>; JS copies the bytes during the call and never writes them.
    private static Span<byte> Writable(ReadOnlySpan<byte> data) => MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(data), data.Length);
    private static int Handle(ResourceHandle r) => checked((int)r.Id);
    private static Exception Unsupported(string feature) => new PlatformNotSupportedException($"WebGL2 does not support {feature} in Foster.Web.");
    internal override void CreateDevice(in AppFlags flags) { }
    internal override void DestroyDevice() => disposed = true;
    internal override void Shutdown() => Flush();
    internal override void WindowCreated(Window window) { }
    internal override void WindowDestroyed(Window window) { }
    internal override void OnEvent(SDL3.SDL.SDL_EventType type) { }
    internal override void Present() => Flush();
    private void Flush() { while (destroying.TryDequeue(out int handle)) WebInterop.Destroy(handle); }
    internal override void DestroyResource(ResourceHandle resource) { if (!disposed) destroying.Enqueue(Handle(resource)); }
    public override bool IsTextureFormatSupported(TextureFormat format) => WebInterop.TextureFormatSupported((int)format);
    public override bool IsTextureMultiSampleSupported(TextureFormat format, SampleCount sampleCount) => sampleCount == SampleCount.One && IsTextureFormatSupported(format);
    internal override ResourceHandle CreateTexture(string? name, int width, int height, TextureFormat format, TextureFlags flags, SampleCount sampleCount, nint? targetBinding)
    {
        if (!IsTextureFormatSupported(format)) throw Unsupported($"texture format {format}");
        if (sampleCount != SampleCount.One || (flags & ~TextureFlags.GenerateMipmaps) != 0) throw Unsupported("multisampled or compute textures");
        var levels = flags.HasFlag(TextureFlags.GenerateMipmaps) ? Texture.CalculateMipLevelCount(width, height) : 1;
        return (nint)WebInterop.Create("texture", Json(new TextureDesc(width, height, (int)format, (int)(targetBinding ?? 0), levels), WebJson.Default.TextureDesc));
    }
    internal override unsafe void SetTextureData(ResourceHandle texture, nint data, int length, RectInt destRegion)
        => WebInterop.Upload(Handle(texture), new Span<byte>((void*)data, length), 0, Json(Region(destRegion)));
    internal override unsafe void GetTextureData(ResourceHandle texture, nint data, int length, RectInt sourceRegion)
        => WebInterop.ReadTexture(Handle(texture), new Span<byte>((void*)data, length), Json(Region(sourceRegion)));
    internal override void BlitTexture(ResourceHandle sourceTexture, RectInt sourceRegion, ResourceHandle destTexture, RectInt destRegion, TextureFilter filter)
        => WebInterop.Blit(Handle(sourceTexture), Json(Region(sourceRegion)), Handle(destTexture), Json(Region(destRegion)), (int)filter);
    internal override ResourceHandle CreateTarget(int width, int height)
        => (nint)WebInterop.Create("target", Json(new TargetDesc(width, height), WebJson.Default.TargetDesc));
    internal override ResourceHandle CreateShader(Shader shader, byte[] code, string entryPoint)
    {
        if (shader.Stage == ShaderStage.Compute || shader.StorageBufferCount > 0) throw Unsupported("compute shaders or shader storage buffers");
        return (nint)WebInterop.Create("shader", Json(new ShaderDesc((int)shader.Stage, Encoding.UTF8.GetString(code)), WebJson.Default.ShaderDesc));
    }
    internal override ResourceHandle CreateBuffer(string? name, BufferType type, IndexFormat format)
    {
        if (type is BufferType.Compute or BufferType.Storage) throw Unsupported("storage buffers");
        return (nint)WebInterop.Create("buffer", Json(new BufferDesc((int)type), WebJson.Default.BufferDesc));
    }
    internal override unsafe void UploadBufferData(ResourceHandle buffer, nint data, int dataSize, int dataDestOffset)
        => WebInterop.Upload(Handle(buffer), new Span<byte>((void*)data, dataSize), dataDestOffset, "");
    internal override void PerformDispatch(ComputeCommand command) => throw Unsupported("compute dispatch");
    internal override void Clear(IDrawableTarget target, ReadOnlySpan<Color> color, float depth, int stencil, ClearMask mask)
    {
        // 不用 LINQ 处理结构体：wasm AOT 下解释执行的 Select<Color, T> 经 gsharedvt 包装调用 AOT lambda 会越界崩溃。
        var colors = new float[color.Length][];
        for (int i = 0; i < color.Length; i++)
            colors[i] = [color[i].R / 255f, color[i].G / 255f, color[i].B / 255f, color[i].A / 255f];
        WebInterop.Clear(target is Target t ? Handle(t.Resource) : 0, Json(colors, WebJson.Default.SingleArrayArray), depth, stencil, (int)mask);
    }
    internal override void PerformDraw(DrawCommand command)
    {
        if (command.VertexStorageBuffers.Count > 0 || command.FragmentStorageBuffers.Count > 0) throw Unsupported("shader storage buffers");
        // 不用 LINQ 处理结构体(见 Clear)。
        var vertices = new VertexBufferDesc[command.VertexBuffers.Count];
        for (int v = 0; v < vertices.Length; v++)
        {
            var (buffer, instance) = command.VertexBuffers[v];
            var elements = buffer.Format.Elements;
            var attributes = new AttributeDesc[elements.Count];
            int offset = 0;
            for (int i = 0; i < attributes.Length; i++)
            {
                var e = elements[i];
                attributes[i] = new AttributeDesc(e.Index, (int)e.Type, e.Normalized, offset);
                offset += e.Type.SizeInBytes();
            }
            vertices[v] = new VertexBufferDesc(Handle(buffer.Resource), buffer.Format.Stride, instance, attributes);
        }
        static SamplerDesc[] Samplers(StackList16<BoundSampler> samplers)
        {
            var result = new SamplerDesc[samplers.Count];
            for (int i = 0; i < samplers.Count; i++)
            {
                var s = samplers[i];
                result[i] = new SamplerDesc(s.Texture == null ? 0 : Handle(s.Texture.Resource),
                    (int)s.Sampler.Filter, (int)s.Sampler.WrapX, (int)s.Sampler.WrapY, s.Sampler.Mipmaps);
            }
            return result;
        }
        static int[] Stencil(in StencilState s) => [(int)s.FailOp, (int)s.DepthFailOp, (int)s.PassOp, (int)s.CompareOp];
        var blend = command.BlendMode;
        WebInterop.BeginDraw(Json(new DrawDesc(
            target: command.Target is Target t ? Handle(t.Resource) : 0,
            vertexShader: Handle(command.VertexShader!.Resource), fragmentShader: Handle(command.FragmentShader!.Resource),
            vertices: vertices, fragmentSamplers: Samplers(command.FragmentSamplers), vertexSamplers: Samplers(command.VertexSamplers),
            indexBuffer: command.IndexBuffer == null ? 0 : Handle(command.IndexBuffer.Resource),
            indexFormat: (int)(command.IndexBuffer?.Format ?? IndexFormat.Sixteen),
            IndexCount: command.IndexCount, IndexOffset: command.IndexOffset, VertexCount: command.VertexCount,
            VertexOffset: command.VertexOffset, InstanceCount: command.InstanceCount,
            viewport: command.Viewport is {} vp ? Region(vp) : null, scissor: command.Scissor is {} sc ? Region(sc) : null,
            blend: [(int)blend.ColorSource, (int)blend.ColorDestination, (int)blend.ColorOperation, (int)blend.AlphaSource, (int)blend.AlphaDestination, (int)blend.AlphaOperation, (int)blend.Mask],
            blendColor: [blend.Color.R / 255f, blend.Color.G / 255f, blend.Color.B / 255f, blend.Color.A / 255f],
            cull: (int)command.CullMode, DepthTestEnabled: command.DepthTestEnabled, DepthWriteEnabled: command.DepthWriteEnabled,
            depthCompare: (int)command.DepthCompare,
            StencilTestEnabled: command.StencilTestEnabled, StencilCompareMask: command.StencilCompareMask,
            StencilWriteMask: command.StencilWriteMask, StencilReferenceValue: command.StencilReferenceValue,
            frontStencil: Stencil(command.FrontStencilState), backStencil: Stencil(command.BackStencilState)),
            WebJson.Default.DrawDesc));
        for (int i = 0; i < command.VertexUniformBuffers.Count; i++)
            if (command.VertexUniformBuffers[i] is {} u) WebInterop.Uniform(0, i, Writable(u.Get()));
        for (int i = 0; i < command.FragmentUniformBuffers.Count; i++)
            if (command.FragmentUniformBuffers[i] is {} u) WebInterop.Uniform(1, i, Writable(u.Get()));
        WebInterop.Draw();
    }
}
