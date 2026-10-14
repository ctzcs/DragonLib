using Engine.World;
using Foster.Framework;

namespace Engine.Rendering;

/// <summary>
/// 每帧材质绑定入口：<see cref="Renderer3D"/> 在颜色 pass 提交每个 draw 前调用一次，
/// 由画风自己写入光照、阴影、环境贴图等槽位。共享材质因此不会残留上一相机的数据。
/// 标准画风的实现是 Standard/ 下的 SceneLighting3D；其他画风按自己的 shader 布局实现。
/// </summary>
public interface IMaterialBinder3D
{
    void Bind(Material material, Camera3D camera);
}
