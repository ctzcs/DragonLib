using System.Collections.Concurrent;
using System.Threading;

namespace Engine.Threading;

/// <summary>Represents one scheduled job or parallel batch.</summary>
public sealed class JobHandle
{
    private readonly object _sync = new();
    private int _remaining;
    private bool _completed;
    private ConcurrentQueue<Exception>? _exceptions;

    internal JobHandle(int remaining)
    {
        _remaining = remaining;
        _completed = remaining == 0;
    }

    public bool IsCompleted => Volatile.Read(ref _remaining) == 0;

    internal void AddException(Exception exception)
    {
        var exceptions = Volatile.Read(ref _exceptions);
        if (exceptions == null)
        {
            var created = new ConcurrentQueue<Exception>();
            exceptions = Interlocked.CompareExchange(ref _exceptions, created, null) ?? created;
        }

        exceptions.Enqueue(exception);
    }

    internal void CompleteOne()
    {
        if (Interlocked.Decrement(ref _remaining) != 0)
            return;

        lock (_sync)
        {
            _completed = true;
            Monitor.PulseAll(_sync);
        }
    }

    internal void Wait(JobScheduler scheduler)
    {
        while (!IsCompleted)
        {
            if (scheduler.TryExecuteOneForCurrentThread())
                continue;

            lock (_sync)
            {
                if (!_completed)
                    Monitor.Wait(_sync, 1);
            }
        }

        if (_exceptions == null || _exceptions.IsEmpty)
            return;

        throw new AggregateException(_exceptions);
    }
}
