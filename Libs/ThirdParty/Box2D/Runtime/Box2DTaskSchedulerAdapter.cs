using System.Collections.Concurrent;
using Box2D.NET;
using Engine.Threading;

namespace DragonLib.Box2D;

/// <summary>Bridges Box2D task callbacks to the shared engine scheduler.</summary>
public sealed class Box2DTaskSchedulerAdapter
{
    private static readonly ConcurrentBag<Box2DTaskHandle> HandlePool = new();

    public static object Enqueue(
        b2TaskCallback task,
        object taskContext,
        object userContext)
    {
        ArgumentNullException.ThrowIfNull(task);
        var scheduler = (JobScheduler)userContext;
        if (!HandlePool.TryTake(out var taskHandle))
            taskHandle = new Box2DTaskHandle();

        taskHandle.Initialize(task, taskContext);
        try
        {
            taskHandle.Schedule(scheduler);
            return taskHandle;
        }
        catch
        {
            taskHandle.Reset();
            HandlePool.Add(taskHandle);
            throw;
        }
    }

    public static void Finish(object taskHandle, object userContext)
    {
        ArgumentNullException.ThrowIfNull(taskHandle);
        var scheduler = (JobScheduler)userContext;
        var handle = (Box2DTaskHandle)taskHandle;
        try
        {
            scheduler.Complete(handle.Handle);
        }
        finally
        {
            handle.Reset();
            HandlePool.Add(handle);
        }
    }

    private sealed class Box2DTaskHandle
    {
        private readonly Action<JobContext> _execute;
        private b2TaskCallback? _task;
        private object? _taskContext;

        public Box2DTaskHandle()
        {
            _execute = Execute;
        }

        public JobHandle Handle { get; private set; } = null!;

        public void Initialize(b2TaskCallback task, object taskContext)
        {
            _task = task;
            _taskContext = taskContext;
        }

        public void Schedule(JobScheduler scheduler)
        {
            Handle = scheduler.Schedule(_execute);
        }

        public void Reset()
        {
            Handle = null!;
            _task = null;
            _taskContext = null;
        }

        private void Execute(JobContext _)
        {
            _task!(_taskContext!);
        }
    }
}
