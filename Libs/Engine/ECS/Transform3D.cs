using System.Numerics;
using DCFApixels.DragonECS;
using Engine.Assets;

// 本文件只放【配置层】3D 组件：随 prefab/level 序列化的创作数据。
// 每帧重算的世界矩阵是【派生层】，见 Transform3DSystem.cs 里的 LocalToWorldComp。

namespace Engine.ECS;

/// <summary>
/// 3D 局部变换（TRS），配置层。世界矩阵由 <see cref="Transform3DSystem"/> 每帧写入
/// <see cref="LocalToWorldComp"/>；本组件只存"创作数据"，随 prefab/level 序列化。
/// </summary>
public struct Transform3DComp : IEcsComponent
{
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 Scale = Vector3.One;

    public Transform3DComp()
    {
    }

    public Transform3DComp(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        Position = position;
        Rotation = rotation;
        Scale = scale;
    }
}

/// <summary>
/// 父子层级（配置层）：指向同一关卡里另一个实例（按 <see cref="SpawnId"/>），被引用方须带 SpawnIdComp。
/// 运行时由 <see cref="Transform3DSystem"/> 解析成真实实体并组合矩阵。之所以存 SpawnId
/// 而不是实体 int id：int id 删除后会被回收复用，跨存盘不稳定。
/// </summary>
public struct Parent3DComp : IEcsComponent
{
    public EntityRef Parent;
}

/// <summary>
/// 3D 网格渲染（配置层）：引用一个模型资产（当前为 DragonLib.Gltf 的 GltfModelAsset，按名字解析，
/// 如 "Models/testscene"）。MeshIndex = -1 表示绘制模型的全部 primitive。
/// </summary>
public struct MeshRendererComp : IEcsComponent
{
    public AssetId Model;
    public int MeshIndex = -1;

    public MeshRendererComp()
    {
    }

    public MeshRendererComp(AssetId model, int meshIndex = -1)
    {
        Model = model;
        MeshIndex = meshIndex;
    }
}
