using System.Runtime.CompilerServices;
using DCFApixels.DragonECS;
using DCFApixels.DragonECS.Core;

namespace Engine.ECS;

public interface IRenderSystem : IEcsProcess
{
    void Render();
}

public interface IUpdateSystem : IEcsProcess
{
    void Update();
}


public class RenderSystem : EcsRunner<IRenderSystem>, IRenderSystem
{
    public void Render()
    {
        foreach (var item in Process)
        {
            item.Render();
        }
    }
}

public class UpdateSystem : EcsRunner<IUpdateSystem>, IUpdateSystem
{
    public void Update()
    {
        foreach (var item in Process)
        {
            item.Update();
        }
    }
}


public static class PipelineRunnerExtension
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void Update(this EcsPipeline pipeline)
    {
        pipeline.GetRunnerInstance<UpdateSystem>().Update();
    }
    
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void Render(this EcsPipeline pipeline)
    {
        pipeline.GetRunnerInstance<RenderSystem>().Render();
    }
}

