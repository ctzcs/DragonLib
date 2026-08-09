using System.Numerics;
using DragonLib.Box2D;
using DCFApixels.DragonECS;
using Engine.ECS;
using Engine.Threading;
using Xunit;
using static Box2D.NET.B2Worlds;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Game0.EngineTests;

public sealed class JobSchedulerTests
{
    [Fact]
    public void NegativeWorkerCountIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new JobScheduler(-1));
    }

    [Fact]
    public void ParallelForBatchCountDoesNotOverflow()
    {
        using var scheduler = new JobScheduler(1);
        var handle = scheduler.ScheduleParallel(int.MaxValue, int.MaxValue, (_, _, _) => { });

        scheduler.Complete(handle);

        Assert.True(handle.IsCompleted);
    }

    [Fact]
    public void ScheduleRunsExactlyOnce()
    {
        using var scheduler = new JobScheduler(2);
        int count = 0;
        var handle = scheduler.Schedule(_ => Interlocked.Increment(ref count));

        scheduler.Complete(handle);

        Assert.Equal(1, count);
        Assert.True(handle.IsCompleted);
    }

    [Fact]
    public void ParallelForCoversEveryItemOnce()
    {
        using var scheduler = new JobScheduler(4);
        var hits = new int[4097];
        var handle = scheduler.ScheduleParallel(
            hits.Length,
            31,
            (start, end, _) =>
            {
                for (int i = start; i < end; i++)
                    Interlocked.Increment(ref hits[i]);
            });

        scheduler.Complete(handle);

        Assert.All(hits, hit => Assert.Equal(1, hit));
    }

    [Fact]
    public void CompleteHelpsRunWorkOnCallingThread()
    {
        using var scheduler = new JobScheduler(1);
        int workerIndex = -1;
        var handle = scheduler.Schedule(context => workerIndex = context.WorkerIndex);

        scheduler.Complete(handle);

        Assert.Equal(0, workerIndex);
    }

    [Fact]
    public void ExceptionsAreReportedByComplete()
    {
        using var scheduler = new JobScheduler(2);
        var handle = scheduler.Schedule(_ => throw new InvalidOperationException("job failure"));

        var exception = Assert.Throws<AggregateException>(() => scheduler.Complete(handle));

        Assert.Contains(exception.InnerExceptions, item => item is InvalidOperationException);
    }

    [Fact]
    public void ParallelExceptionsAreAllReportedByComplete()
    {
        using var scheduler = new JobScheduler(4);
        const int itemCount = 64;
        var handle = scheduler.ScheduleParallel(
            itemCount,
            1,
            (start, _, _) => throw new InvalidOperationException(start.ToString()));

        var exception = Assert.Throws<AggregateException>(() => scheduler.Complete(handle));

        Assert.Equal(itemCount, exception.InnerExceptions.Count);
    }

    [Fact]
    public void WorkerCanCompleteAChildJobWithoutDeadlocking()
    {
        using var scheduler = new JobScheduler(2);
        int value = 0;
        var parent = scheduler.Schedule(_ =>
        {
            var child = scheduler.Schedule(__ => Interlocked.Increment(ref value));
            scheduler.Complete(child);
        });

        scheduler.Complete(parent);

        Assert.Equal(1, value);
    }

    [Fact]
    public void SchedulerCanBeDisposedAfterParallelWork()
    {
        var scheduler = new JobScheduler(3);
        var handle = scheduler.ScheduleParallel(10_000, 127, (start, end, _) =>
        {
            for (int i = start; i < end; i++)
                Math.Sqrt(i);
        });

        scheduler.Complete(handle);
        scheduler.Dispose();
        scheduler.Dispose();

        Assert.Throws<ObjectDisposedException>(() => scheduler.Schedule(_ => { }));
    }

    [Fact]
    public void Box2DUsesTheSharedScheduler()
    {
        using var scheduler = new JobScheduler(2);
        using var world = new Box2DWorld(new Vector2(0f, 9.81f), scheduler, 2);

        for (int i = 0; i < 64; i++)
            world.Step(1f / 60f, 4);

        Assert.True(b2World_IsValid(world.WorldId));
        Assert.Equal(2, b2World_GetWorkerCount(world.WorldId));
    }

    [Fact]
    public void EcsGroupCanUpdateExistingComponentsInParallel()
    {
        var world = new EcsWorld();
        try
        {
            var pool = world.GetPool<ParallelPosition>();
            for (int i = 0; i < 2048; i++)
            {
                int entity = world.NewEntity();
                pool.Add(entity).Value = i;
            }

            var group = world.WhereToGroup(out SingleAspect<ParallelPosition> aspect);
            using var scheduler = new JobScheduler(4);
            group.IterateParallel(scheduler, (ReadOnlySpan<int> entities, in JobContext _) =>
            {
                foreach (int entity in entities)
                    aspect.pool.Get(entity).Value++;
            }, batchSize: 37);

            for (int i = 1; i <= 2048; i++)
                Assert.Equal(i, pool.Get(i).Value);
        }
        finally
        {
            world.Destroy();
        }
    }

    private struct ParallelPosition : IEcsComponent
    {
        public int Value;
    }
}
