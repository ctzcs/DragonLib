using Foster.Framework;

namespace Engine.Rendering;

/// <summary>
/// <see cref="Renderer3D"/> 阴影 pass 的默认深度材质：vertex b0 为 WorldLightViewProjection，
/// 蒙皮变体另读 vertex b2/b3 的 joint palette（与颜色 pass 同一份数据）。
/// 只依赖 Renderer3D 的提交约定，与画风无关；顶点位置须在 TEXCOORD0。
/// </summary>
public sealed class ShadowDepthShaders3D : IDisposable
{
    public static readonly ShaderStageSpec FragmentSpec = new(0, 0, "fragment_main");
    public static readonly ShaderStageSpec VertexSpec = new(0, 1, "vertex_main");
    public static readonly ShaderStageSpec SkinnedVertexSpec = new(0, 4, "vertex_main");
    private readonly EmbeddedShaderMaterial? _depth;
    private readonly EmbeddedShaderMaterial? _skinnedDepth;

    public Material Depth => _depth!.Material;
    public Material SkinnedDepth => _skinnedDepth!.Material;

    public ShadowDepthShaders3D(GraphicsDevice device)
    {
        try
        {
            _depth = Load(device, "DepthOnly", VertexSpec);
            _skinnedDepth = Load(device, "DepthOnlySkinned", SkinnedVertexSpec);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private static EmbeddedShaderMaterial Load(GraphicsDevice device, string name, ShaderStageSpec vertex)
        => EmbeddedShaderMaterial.Load(device, typeof(ShadowDepthShaders3D).Assembly,
            $"Engine/Shaders/{name}", FragmentSpec, vertex);

    public void Dispose()
    {
        _depth?.Dispose();
        _skinnedDepth?.Dispose();
    }
}
