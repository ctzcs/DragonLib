using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace Foster.Framework;

internal sealed class GraphicsDeviceWeb(App app) : GraphicsDevice(app)
{
    private bool disposed;
    private readonly ConcurrentQueue<int> destroying = new();
    public override GraphicsDriver Driver => GraphicsDriver.WebGL;
    public override bool Disposed => disposed;
    public override bool VSync { get => true; set { if (!value) throw new PlatformNotSupportedException("Browser rendering is scheduled by requestAnimationFrame."); } }
    private static string Json(object value) => JsonSerializer.Serialize(value);
    private static int[] Region(RectInt r) => [r.X, r.Y, r.Width, r.Height];
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
    public override bool IsTextureFormatSupported(TextureFormat format) => format is TextureFormat.Color or TextureFormat.R8 or TextureFormat.R8G8 or TextureFormat.Depth24Stencil8 or TextureFormat.Depth16 or TextureFormat.Depth24 or TextureFormat.Depth32;
    public override bool IsTextureMultiSampleSupported(TextureFormat format, SampleCount sampleCount) => sampleCount == SampleCount.One && IsTextureFormatSupported(format);
    internal override ResourceHandle CreateTexture(string? name, int width, int height, TextureFormat format, TextureFlags flags, SampleCount sampleCount, nint? targetBinding)
    {
        if (!IsTextureFormatSupported(format)) throw Unsupported($"texture format {format}");
        if (sampleCount != SampleCount.One || flags != TextureFlags.None) throw Unsupported("multisampled or compute textures");
        return (nint)WebInterop.Create("texture", Json(new { width, height, format = (int)format, target = (int)(targetBinding ?? 0) }));
    }
    internal override unsafe void SetTextureData(ResourceHandle texture, nint data, int length, RectInt destRegion)
        => WebInterop.Upload(Handle(texture), new Span<byte>((void*)data, length), 0, Json(Region(destRegion)));
    internal override unsafe void GetTextureData(ResourceHandle texture, nint data, int length, RectInt sourceRegion)
        => WebInterop.ReadTexture(Handle(texture), new Span<byte>((void*)data, length), Json(Region(sourceRegion)));
    internal override void BlitTexture(ResourceHandle sourceTexture, RectInt sourceRegion, ResourceHandle destTexture, RectInt destRegion, TextureFilter filter)
        => WebInterop.Blit(Handle(sourceTexture), Json(Region(sourceRegion)), Handle(destTexture), Json(Region(destRegion)), (int)filter);
    internal override ResourceHandle CreateTarget(int width, int height)
        => (nint)WebInterop.Create("target", Json(new { width, height }));
    internal override ResourceHandle CreateShader(Shader shader, byte[] code, string entryPoint)
    {
        if (shader.Stage == ShaderStage.Compute || shader.StorageBufferCount > 0) throw Unsupported("compute shaders or shader storage buffers");
        return (nint)WebInterop.Create("shader", Json(new { stage = (int)shader.Stage, code = Encoding.UTF8.GetString(code) }));
    }
    internal override ResourceHandle CreateBuffer(string? name, BufferType type, IndexFormat format)
    {
        if (type is BufferType.Compute or BufferType.Storage) throw Unsupported("storage buffers");
        return (nint)WebInterop.Create("buffer", Json(new { type = (int)type }));
    }
    internal override unsafe void UploadBufferData(ResourceHandle buffer, nint data, int dataSize, int dataDestOffset)
        => WebInterop.Upload(Handle(buffer), new Span<byte>((void*)data, dataSize), dataDestOffset, "");
    internal override void PerformDispatch(ComputeCommand command) => throw Unsupported("compute dispatch");
    internal override void Clear(IDrawableTarget target, ReadOnlySpan<Color> color, float depth, int stencil, ClearMask mask)
    {
        var colors = color.ToArray().Select(c => new[] { c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f }).ToArray();
        WebInterop.Clear(target is Target t ? Handle(t.Resource) : 0, Json(colors), depth, stencil, (int)mask);
    }
    internal override void PerformDraw(DrawCommand command)
    {
        if (command.VertexStorageBuffers.Count > 0 || command.FragmentStorageBuffers.Count > 0) throw Unsupported("shader storage buffers");
        var vertices = new List<object>();
        foreach (var (buffer, instance) in command.VertexBuffers)
        {
            var attributes = new List<object>();
            int offset = 0;
            foreach (var e in buffer.Format.Elements)
            {
                attributes.Add(new { location = e.Index, type = (int)e.Type, normalized = e.Normalized, offset });
                offset += e.Type.SizeInBytes();
            }
            vertices.Add(new { handle = Handle(buffer.Resource), stride = buffer.Format.Stride, instance, attributes });
        }
        static object[] Samplers(StackList16<BoundSampler> samplers) => samplers.ToArray().Select(s => (object)new {
            handle = s.Texture == null ? 0 : Handle(s.Texture.Resource),
            filter = (int)s.Sampler.Filter, wrapX = (int)s.Sampler.WrapX, wrapY = (int)s.Sampler.WrapY
        }).ToArray();
        var blend = command.BlendMode;
        WebInterop.BeginDraw(Json(new {
            target = command.Target is Target t ? Handle(t.Resource) : 0,
            vertexShader = Handle(command.VertexShader!.Resource), fragmentShader = Handle(command.FragmentShader!.Resource),
            vertices, fragmentSamplers = Samplers(command.FragmentSamplers), vertexSamplers = Samplers(command.VertexSamplers),
            indexBuffer = command.IndexBuffer == null ? 0 : Handle(command.IndexBuffer.Resource),
            indexFormat = (int)(command.IndexBuffer?.Format ?? IndexFormat.Sixteen),
            command.IndexCount, command.IndexOffset, command.VertexCount, command.VertexOffset, command.InstanceCount,
            viewport = command.Viewport is {} vp ? Region(vp) : null, scissor = command.Scissor is {} sc ? Region(sc) : null,
            blend = new[] { (int)blend.ColorSource, (int)blend.ColorDestination, (int)blend.ColorOperation, (int)blend.AlphaSource, (int)blend.AlphaDestination, (int)blend.AlphaOperation, (int)blend.Mask },
            blendColor = new[] { blend.Color.R / 255f, blend.Color.G / 255f, blend.Color.B / 255f, blend.Color.A / 255f },
            cull = (int)command.CullMode, command.DepthTestEnabled, command.DepthWriteEnabled, depthCompare = (int)command.DepthCompare,
            command.StencilTestEnabled, command.StencilCompareMask, command.StencilWriteMask, command.StencilReferenceValue,
            frontStencil = new[] { (int)command.FrontStencilState.FailOp, (int)command.FrontStencilState.DepthFailOp, (int)command.FrontStencilState.PassOp, (int)command.FrontStencilState.CompareOp },
            backStencil = new[] { (int)command.BackStencilState.FailOp, (int)command.BackStencilState.DepthFailOp, (int)command.BackStencilState.PassOp, (int)command.BackStencilState.CompareOp }
        }));
        for (int i = 0; i < command.VertexUniformBuffers.Count; i++)
            if (command.VertexUniformBuffers[i] is {} u) WebInterop.Uniform(0, i, u.Get().ToArray());
        for (int i = 0; i < command.FragmentUniformBuffers.Count; i++)
            if (command.FragmentUniformBuffers[i] is {} u) WebInterop.Uniform(1, i, u.Get().ToArray());
        WebInterop.Draw();
    }
}
