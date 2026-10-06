using Engine.Assets.Dasset;
using Engine.World;

namespace Engine.Rendering;

/// <summary>
/// 可选遮挡判断接入点；只能在确定整个世界 AABB 被遮挡时返回 true。
/// 未知、结果尚未就绪、相机/遮挡物改变后旧结果失效时必须返回 false，避免误删可见物体。
/// 不提供原生 query 或深度读回；实现由应用拥有，颜色 pass 调用，阴影 pass 保留。
/// </summary>
public interface IOcclusionCuller3D
{
    void BeginFrame(Camera3D camera);
    bool IsOccluded(in DassetBounds worldBounds);
}
