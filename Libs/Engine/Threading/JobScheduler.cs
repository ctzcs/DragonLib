using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Engine.Threading;

/// <summary>
/// Persistent worker pool for frame-sized jobs and parallel range processing.
/// The calling thread participates while completing a handle.
/// </summary>
public sealed class JobScheduler : IDisposable
{
    private const int DefaultQueueCapacity = 4096;
    private readonly object _queueSync = new();
    private readonly WorkItem[] _queue;
    private readonly Thread[] _workers;
    private readonly SemaphoreSlim _wake;
    private int _queueHead;
    private int _queueTail;
    private int _queued;
    private int _activeHandles;
    private int _shutdown;
    private int _disposed;

    [ThreadStatic]
    private static int _currentWorkerIndex;

    public JobScheduler(int workerCount = 0, int queueCapacity = DefaultQueueCapacity)
    {
        if (queueCapacity < 1)
            throw new ArgumentOutOfRangeException(nameof(queueCapacity));

        if (workerCount < 0)
            throw new ArgumentOutOfRangeException(nameof(workerCount));

        WorkerCount = workerCount == 0 ? Math.Max(1, Environment.ProcessorCount) : workerCount;
        if (WorkerCount < 1)
            throw new ArgumentOutOfRangeException(nameof(workerCount));

        _queue = new WorkItem[queueCapacity];
        _wake = new SemaphoreSlim(0);
        _workers = new Thread[WorkerCount - 1];

        for (int i = 0; i < _workers.Length; i++)
        {
            int workerIndex = i + 1;
            var thread = new Thread(() => WorkerLoop(workerIndex))
            {
                IsBackground = true,
                Name = $"engine_job_worker_{workerIndex:00}"
            };
            _workers[i] = thread;
            thread.Start();
        }
    }

    public int WorkerCount { get; }

    public JobHandle Schedule(Action<JobContext> job)
    {
        ArgumentNullException.ThrowIfNull(job);
        ThrowIfDisposed();

        var handle = new JobHandle(1);
        Interlocked.Increment(ref _activeHandles);
        try
        {
            Enqueue(new WorkItem(handle, job));
            return handle;
        }
        catch
        {
            Interlocked.Decrement(ref _activeHandles);
            throw;
        }
    }

    public JobHandle ScheduleParallel(
        int itemCount,
        int batchSize,
        Action<int, int, JobContext> execute)
    {
        ArgumentNullException.ThrowIfNull(execute);
        if (itemCount < 0)
            throw new ArgumentOutOfRangeException(nameof(itemCount));
        if (batchSize < 1)
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        ThrowIfDisposed();

        int batchCount = itemCount / batchSize + (itemCount % batchSize == 0 ? 0 : 1);
        int runnerCount = Math.Min(WorkerCount, batchCount);
        var handle = new JobHandle(runnerCount);
        Interlocked.Increment(ref _activeHandles);

        if (runnerCount == 0)
        {
            Interlocked.Decrement(ref _activeHandles);
            return handle;
        }

        var batch = new ParallelBatch(handle, runnerCount, itemCount, batchSize, execute);
        try
        {
            EnqueueBatch(new WorkItem(batch), runnerCount);
            return handle;
        }
        catch
        {
            Interlocked.Decrement(ref _activeHandles);
            throw;
        }
    }

    public void Complete(JobHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        handle.Wait(this);
    }

    internal bool TryExecuteOneForCurrentThread()
    {
        if (!TryDequeue(out WorkItem item))
            return false;

        Execute(item, _currentWorkerIndex);
        return true;
    }

    private void WorkerLoop(int workerIndex)
    {
        _currentWorkerIndex = workerIndex;
        while (true)
        {
            _wake.Wait(10);
            while (TryDequeue(out WorkItem item))
                Execute(item, workerIndex);

            if (Volatile.Read(ref _shutdown) != 0)
                break;
        }

        while (TryDequeue(out WorkItem remaining))
            Execute(remaining, workerIndex);
    }

    private void Execute(WorkItem item, int workerIndex)
    {
        var context = new JobContext(workerIndex);
        try
        {
            if (item.Batch != null)
            {
                if (item.Batch.Run(context))
                    Interlocked.Decrement(ref _activeHandles);
            }
            else
            {
                item.Job!(context);
                item.Handle!.CompleteOne();
            }
        }
        catch (Exception exception)
        {
            (item.Batch?.Handle ?? item.Handle)!.AddException(exception);
            if (item.Batch == null)
                item.Handle!.CompleteOne();
        }
        finally
        {
            if (item.Batch == null)
                Interlocked.Decrement(ref _activeHandles);
        }
    }

    private void Enqueue(WorkItem item)
    {
        lock (_queueSync)
        {
            if (_queued == _queue.Length)
                throw new InvalidOperationException("The JobScheduler queue is full.");

            _queue[_queueTail] = item;
            _queueTail = (_queueTail + 1) % _queue.Length;
            _queued++;
        }

        _wake.Release();
    }

    private void EnqueueBatch(WorkItem item, int count)
    {
        lock (_queueSync)
        {
            if (_queue.Length - _queued < count)
                throw new InvalidOperationException("The JobScheduler queue is full.");

            for (int i = 0; i < count; i++)
            {
                _queue[_queueTail] = item;
                _queueTail = (_queueTail + 1) % _queue.Length;
                _queued++;
            }
        }

        for (int i = 0; i < count; i++)
            _wake.Release();
    }

    private bool TryDequeue(out WorkItem item)
    {
        lock (_queueSync)
        {
            if (_queued == 0)
            {
                item = default;
                return false;
            }

            item = _queue[_queueHead];
            _queue[_queueHead] = default;
            _queueHead = (_queueHead + 1) % _queue.Length;
            _queued--;
            return true;
        }
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(JobScheduler));
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        var disposeTimer = Stopwatch.StartNew();
        while (Volatile.Read(ref _activeHandles) != 0)
        {
            if (!TryExecuteOneForCurrentThread())
                Thread.Yield();
            if (disposeTimer.Elapsed > TimeSpan.FromSeconds(10))
                throw new TimeoutException("JobScheduler could not drain active jobs during disposal.");
        }

        Volatile.Write(ref _shutdown, 1);
        for (int i = 0; i < _workers.Length; i++)
            _wake.Release();
        foreach (var worker in _workers)
        {
            if (!worker.Join(TimeSpan.FromSeconds(10)))
                throw new TimeoutException($"JobScheduler worker '{worker.Name}' did not exit.");
        }
        _wake.Dispose();
    }

    private readonly struct WorkItem
    {
        public readonly JobHandle? Handle;
        public readonly Action<JobContext>? Job;
        public readonly ParallelBatch? Batch;

        public WorkItem(JobHandle handle, Action<JobContext> job)
        {
            Handle = handle;
            Job = job;
            Batch = null;
        }

        public WorkItem(ParallelBatch batch)
        {
            Handle = null;
            Job = null;
            Batch = batch;
        }
    }

    private sealed class ParallelBatch
    {
        private readonly int _itemCount;
        private readonly int _batchSize;
        private readonly Action<int, int, JobContext> _execute;
        private int _next;
        private int _remainingRunners;

        public ParallelBatch(JobHandle handle, int runnerCount, int itemCount, int batchSize, Action<int, int, JobContext> execute)
        {
            Handle = handle;
            _itemCount = itemCount;
            _batchSize = batchSize;
            _execute = execute;
            _remainingRunners = runnerCount;
        }

        public JobHandle Handle { get; }
        public bool Run(JobContext context)
        {
            bool isLastRunner = false;
            try
            {
                while (true)
                {
                    int start = Interlocked.Add(ref _next, _batchSize) - _batchSize;
                    if (start >= _itemCount)
                        break;

                    int end = Math.Min(start + _batchSize, _itemCount);
                    try
                    {
                        _execute(start, end, context);
                    }
                    catch (Exception exception)
                    {
                        Handle.AddException(exception);
                    }
                }
            }
            finally
            {
                Handle.CompleteOne();
                isLastRunner = Interlocked.Decrement(ref _remainingRunners) == 0;
            }

            return isLastRunner;
        }
    }
}
