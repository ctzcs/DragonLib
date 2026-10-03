namespace DragonLib.Box2D;

/// <summary>
/// Task scheduling for Box2D's multithreaded solver. Box2D does not depend on a particular job system;
/// wrap yours in this interface. For Engine's JobScheduler:
/// <code>
/// sealed class JobSchedulerTasks(JobScheduler scheduler) : IBox2DTaskScheduler
/// {
///     public int WorkerCount => scheduler.WorkerCount;
///     public object Schedule(Action run) => scheduler.Schedule(_ => run());
///     public void Complete(object handle) => scheduler.Complete((JobHandle)handle);
/// }
/// </code>
/// </summary>
public interface IBox2DTaskScheduler
{
    /// <summary>Threads (including the caller) that may run tasks; caps Box2D's worker count.</summary>
    int WorkerCount { get; }

    /// <summary>Runs <paramref name="run"/> once on any thread; returns the handle passed to <see cref="Complete"/>.</summary>
    object Schedule(Action run);

    /// <summary>Blocks until the scheduled task has finished.</summary>
    void Complete(object handle);
}
