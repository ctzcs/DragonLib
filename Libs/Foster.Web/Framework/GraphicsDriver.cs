namespace Foster.Framework;

public enum GraphicsDriver { None, Private, Vulkan, D3D12, Metal, WebGL }
public static class GraphicsDriverExt
{
    public static string GetShaderExtension(this GraphicsDriver driver) => driver switch
    {
        GraphicsDriver.WebGL => "glsl",
        GraphicsDriver.None => "",
        GraphicsDriver.Private or GraphicsDriver.Vulkan => "spv",
        GraphicsDriver.D3D12 => "dxil",
        GraphicsDriver.Metal => "msl",
        _ => throw new NotSupportedException()
    };
}
