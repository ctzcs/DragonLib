using System.Buffers;
using DCFApixels.DragonECS;
using Engine.Threading;

namespace Engine.ECS;

public delegate void EcsParallelHandler(ReadOnlySpan<int> entities, in JobContext context);

public static class EcsParallelExtensions
{
    public static void IterateParallel(
        this EcsGroup group,
        JobScheduler scheduler,
        EcsParallelHandler handler,
        int batchSize = 512)
        => IterateParallel(group.ToSpan(), scheduler, handler, batchSize);

    public static void IterateParallel(
        this EcsReadonlyGroup group,
        JobScheduler scheduler,
        EcsParallelHandler handler,
        int batchSize = 512)
        => IterateParallel(group.ToSpan(), scheduler, handler, batchSize);

    public static void IterateParallel(
        this EcsSpan span,
        JobScheduler scheduler,
        EcsParallelHandler handler,
        int batchSize = 512)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(handler);
        if (batchSize < 1)
            throw new ArgumentOutOfRangeException(nameof(batchSize));

        int count = span.Count;
        if (count == 0)
            return;

        int[] entities = ArrayPool<int>.Shared.Rent(count);
        span.AsSystemSpan().CopyTo(entities);
        try
        {
            var handle = scheduler.ScheduleParallel(
                count,
                batchSize,
                (start, end, context) =>
                    handler(new ReadOnlySpan<int>(entities, start, end - start), in context));
            scheduler.Complete(handle);
        }
        finally
        {
            ArrayPool<int>.Shared.Return(entities, clearArray: false);
        }
    }
}
