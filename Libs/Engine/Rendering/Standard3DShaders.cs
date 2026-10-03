using Foster.Framework;

namespace Engine.Rendering;

/// <summary>标准 3D 管线的 shader 与默认贴图所有者；材质 clone 的生命周期不得超过本对象。</summary>
public sealed class Standard3DShaders : IDisposable
{
    public static readonly ShaderStageSpec FragmentSpec = new(3, 4, "fragment_main");
    public static readonly ShaderStageSpec VertexSpec = new(0, 2, "vertex_main");
    public static readonly ShaderStageSpec SkinnedVertexSpec = new(0, 3, "vertex_main");
    public static readonly ShaderStageSpec DepthFragmentSpec = new(0, 0, "fragment_main");
    public static readonly ShaderStageSpec DepthVertexSpec = new(0, 1, "vertex_main");
    private readonly List<EmbeddedShaderMaterial> _owned = [];
    public Material Standard { get; }
    public Material Skinned { get; }
    public Material Depth { get; }
    public Material SkinnedDepth { get; }
    public Texture WhiteTexture { get; }

    public Standard3DShaders(GraphicsDevice device)
    {
        try
        {
            Standard = Load(device, "Standard3D", FragmentSpec, VertexSpec);
            Skinned = Load(device, "Standard3DSkinned", FragmentSpec, SkinnedVertexSpec);
            Depth = Load(device, "DepthOnly", DepthFragmentSpec, DepthVertexSpec);
            SkinnedDepth = Load(device, "DepthOnlySkinned", DepthFragmentSpec, SkinnedVertexSpec);
            WhiteTexture = new Texture(device, 1, 1, [Color.White], name: "Engine White 1x1");
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private Material Load(GraphicsDevice device, string name, ShaderStageSpec fragment, ShaderStageSpec vertex)
    {
        var shader = EmbeddedShaderMaterial.Load(device, typeof(Standard3DShaders).Assembly,
            $"Engine/Shaders/{name}", fragment, vertex);
        _owned.Add(shader);
        return shader.Material;
    }

    public void Dispose()
    {
        WhiteTexture?.Dispose();
        foreach (var shader in _owned)
            shader.Dispose();
        _owned.Clear();
    }
}
