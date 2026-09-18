using Foster.Framework;

namespace Engine.Rendering;

/// <summary>
/// 一次 3D draw 的渲染状态：混合模式 / 面剔除 / 深度写，外加是否进透明队列。
/// 请基于 <see cref="Opaque"/> / <see cref="Transparent"/> 预设改字段使用
/// （struct 的 default 不是合法状态：Blend/Cull 全零）。
/// </summary>
public struct RenderState3D
{
    public BlendMode Blend;
    public CullMode Cull;
    public bool DepthWrite;

    /// <summary>true 进透明队列（按到相机距离 back-to-front 排序、在不透明队之后提交），false 进不透明队列。</summary>
    public bool IsTransparent;

    /// <summary>不透明：NonPremultiplied 混合（alpha=1 时等价无混合）、背面剔除、写深度。</summary>
    public static RenderState3D Opaque => new()
    {
        Blend = BlendMode.NonPremultiplied,
        Cull = CullMode.Back,
        DepthWrite = true,
        IsTransparent = false,
    };

    /// <summary>半透明：alpha 混合（SrcAlpha/OneMinusSrcAlpha）、不写深度（深度测试仍开，靠不透明队先写的深度遮挡身后物体）。</summary>
    public static RenderState3D Transparent => new()
    {
        Blend = BlendMode.NonPremultiplied,
        Cull = CullMode.Back,
        DepthWrite = false,
        IsTransparent = true,
    };
}
