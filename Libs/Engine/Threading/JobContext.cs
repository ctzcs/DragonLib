namespace Engine.Threading;

/// <summary>Execution information supplied to a running job.</summary>
public readonly struct JobContext
{
    internal JobContext(int workerIndex)
    {
        WorkerIndex = workerIndex;
    }

    /// <summary>Zero is the calling thread; background workers use positive indexes.</summary>
    public int WorkerIndex { get; }
}
