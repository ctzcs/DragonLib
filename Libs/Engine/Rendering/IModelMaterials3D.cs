using Engine.Assets.Dasset;
using Foster.Framework;

namespace Engine.Rendering;

/// <summary>
/// 为模型的每个 primitive 提供材质和渲染状态，顺序与 <see cref="DassetModelAsset.Primitives"/> 一致，
/// 结果直接交给 <see cref="Renderer3D.DrawModel"/>。实现负责缓存和失效；
/// 标准画风的实现是 Standard/ 下的 MaterialCache。
/// </summary>
public interface IModelMaterials3D
{
    IReadOnlyList<(Material Material, RenderState3D State)> Get(DassetModelAsset model);
}
