using System.Text.Json.Serialization;

namespace Foster.Framework;

// Descriptors sent to foster.js. Property names are the exact JSON keys foster.js reads (some camelCase, some
// PascalCase), so they are spelled here verbatim. Serialized through the source-generated WebJson context:
// no reflection per draw call, and safe under trimming/AOT.
#pragma warning disable IDE1006 // naming: JSON keys

internal sealed record TextureDesc(int width, int height, int format, int target, int mipLevels);
internal sealed record TargetDesc(int width, int height);
internal sealed record ShaderDesc(int stage, string code);
internal sealed record BufferDesc(int type);
internal sealed record AttributeDesc(int location, int type, bool normalized, int offset);
internal sealed record VertexBufferDesc(int handle, int stride, bool instance, AttributeDesc[] attributes);
internal sealed record SamplerDesc(int handle, int filter, int wrapX, int wrapY, bool mipmaps);

internal sealed record DrawDesc(
    int target, int vertexShader, int fragmentShader,
    VertexBufferDesc[] vertices, SamplerDesc[] fragmentSamplers, SamplerDesc[] vertexSamplers,
    int indexBuffer, int indexFormat,
    int IndexCount, int IndexOffset, int VertexCount, int VertexOffset, int InstanceCount,
    int[]? viewport, int[]? scissor,
    int[] blend, float[] blendColor,
    int topology, int cull, bool DepthTestEnabled, bool DepthWriteEnabled, int depthCompare,
    bool StencilTestEnabled, byte StencilCompareMask, byte StencilWriteMask, byte StencilReferenceValue,
    int[] frontStencil, int[] backStencil);

#pragma warning restore IDE1006

[JsonSerializable(typeof(TextureDesc))]
[JsonSerializable(typeof(TargetDesc))]
[JsonSerializable(typeof(ShaderDesc))]
[JsonSerializable(typeof(BufferDesc))]
[JsonSerializable(typeof(DrawDesc))]
[JsonSerializable(typeof(int[]))]
[JsonSerializable(typeof(float[][]))]
[JsonSerializable(typeof(string[]))]
internal sealed partial class WebJson : JsonSerializerContext;
