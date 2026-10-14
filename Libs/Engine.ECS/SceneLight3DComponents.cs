using System.Numerics;
using DCFApixels.DragonECS;
using Engine.Rendering;

namespace Engine.ECS;

/// <summary>方向光配置；Direction 为世界空间的光传播方向。场景使用遇到的第一盏方向光。</summary>
public struct DirectionalLight3DComp : IEcsComponent
{
    public Vector3 Direction = -Vector3.UnitY;
    public Vector3 Color = Vector3.One;
    public float Intensity = 1f;
    public DirectionalLight3DComp() { }
}

/// <summary>点光配置；位置从 LocalToWorldComp 取得，随父子层级移动。</summary>
public struct PointLight3DComp : IEcsComponent
{
    public float Range = 5f;
    public Vector3 Color = Vector3.One;
    public float Intensity = 1f;
    public PointLight3DComp() { }
}

/// <summary>把光源组件收集成与画风无关的数据；写进哪套光照（如标准画风的 SceneLighting3D）由调用方决定。</summary>
public static class SceneLight3DCollector
{
    /// <summary>
    /// 清空并重填 <paramref name="pointLights"/>（最多 <see cref="PointLight3D.MaxCount"/> 盏）；
    /// 方向光取遇到的第一盏，没有时颜色为 0、方向为 -Y。
    /// </summary>
    public static void Collect(EcsWorld world, List<PointLight3D> pointLights, out Vector3 direction, out Vector3 directionalColor)
    {
        var directionalPool = world.GetPool<DirectionalLight3DComp>();
        var pointPool = world.GetPool<PointLight3DComp>();
        var transformPool = world.GetPool<LocalToWorldComp>();
        var foundDirectional = false;
        direction = -Vector3.UnitY;
        directionalColor = Vector3.Zero;
        pointLights.Clear();
        foreach (int e in world.Entities)
        {
            if (!foundDirectional && directionalPool.Has(e))
            {
                var light = directionalPool.Get(e);
                direction = light.Direction;
                directionalColor = light.Color * light.Intensity;
                foundDirectional = true;
            }
            if (pointPool.Has(e) && transformPool.Has(e) && pointLights.Count < PointLight3D.MaxCount)
            {
                var light = pointPool.Get(e);
                pointLights.Add(new PointLight3D(transformPool.Get(e).Value.Translation,
                    light.Range, light.Color, light.Intensity));
            }
        }
    }
}
