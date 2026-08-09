using System.Numerics;
using Engine.Threading;
using global::Box2D.NET;
using static global::Box2D.NET.B2Types;
using static global::Box2D.NET.B2Worlds;

namespace DragonLib.Box2D;

/// <summary>Owns the lifetime of a Box2D simulation world.</summary>
public sealed class Box2DWorld : IDisposable
{
    private B2WorldId _worldId;

    public bool IsDisposed { get; private set; }

    public B2WorldId WorldId
    {
        get
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            return _worldId;
        }
    }

    public Box2DWorld(Vector2 gravity)
        : this(CreateDefaultDefinition(gravity))
    {
    }

    public Box2DWorld(Vector2 gravity, JobScheduler scheduler, int workerCount)
        : this(CreateThreadedDefinition(gravity, scheduler, workerCount))
    {
    }

    public Box2DWorld(in B2WorldDef definition)
    {
        _worldId = b2CreateWorld(definition);
        if (!b2World_IsValid(_worldId))
            throw new InvalidOperationException("Box2D could not allocate a simulation world.");
    }

    public void Step(float deltaSeconds, int subStepCount = 4)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (!float.IsFinite(deltaSeconds) || deltaSeconds < 0f)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        if (subStepCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(subStepCount));

        b2World_Step(_worldId, deltaSeconds, subStepCount);
    }

    public void Dispose()
    {
        if (IsDisposed)
            return;

        if (b2World_IsValid(_worldId))
            b2DestroyWorld(_worldId);

        _worldId = default;
        IsDisposed = true;
    }

    public static B2WorldDef CreateDefaultDefinition(Vector2 gravity)
    {
        var definition = b2DefaultWorldDef();
        definition.gravity = gravity.ToBox2D();
        return definition;
    }

    private static B2WorldDef CreateThreadedDefinition(Vector2 gravity, JobScheduler scheduler, int workerCount)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        if (workerCount < 1)
            throw new ArgumentOutOfRangeException(nameof(workerCount));

        var definition = CreateDefaultDefinition(gravity);
        definition.workerCount = Math.Min(workerCount, scheduler.WorkerCount);
        if (definition.workerCount > 1)
        {
            definition.enqueueTask = Box2DTaskSchedulerAdapter.Enqueue;
            definition.finishTask = Box2DTaskSchedulerAdapter.Finish;
            definition.userTaskContext = scheduler;
        }

        return definition;
    }
}
