using DragonLib.Box2D;
using Engine.Threading;

namespace Game0.Content;

/// <summary>Runs Box2D's multithreaded solver on Engine's shared JobScheduler (DragonLib.Box2D itself has no Engine dependency).</summary>
public sealed class JobSchedulerBox2DTasks(JobScheduler scheduler) : IBox2DTaskScheduler
{
    public int WorkerCount => scheduler.WorkerCount;
    public object Schedule(Action run) => scheduler.Schedule(_ => run());
    public void Complete(object handle) => scheduler.Complete((JobHandle)handle);
}
