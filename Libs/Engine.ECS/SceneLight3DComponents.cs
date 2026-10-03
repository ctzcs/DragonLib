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

public static class SceneLight3DCollector
{
    public static void Collect(EcsWorld world, SceneLighting3D lighting)
    {
        var directionalPool = world.GetPool<DirectionalLight3DComp>();
        var pointPool = world.GetPool<PointLight3DComp>();
        var transformPool = world.GetPool<LocalToWorldComp>();
        var foundDirectional = false;
        lighting.DirectionalColor = Vector3.Zero;
        lighting.PointLights.Clear();
        foreach (int e in world.Entities)
        {
            if (!foundDirectional && directionalPool.Has(e))
            {
                var light = directionalPool.Get(e);
                lighting.Direction = light.Direction;
                lighting.DirectionalColor = light.Color * light.Intensity;
                foundDirectional = true;
            }
            if (pointPool.Has(e) && transformPool.Has(e) && lighting.PointLights.Count < PointLight3D.MaxCount)
            {
                var light = pointPool.Get(e);
                lighting.PointLights.Add(new PointLight3D(transformPool.Get(e).Value.Translation,
                    light.Range, light.Color, light.Intensity));
            }
        }
    }
}
