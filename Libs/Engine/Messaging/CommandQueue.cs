namespace Engine.Messaging;

/// <summary>
/// 单消费者模型，本帧处理
/// A FIFO queue owned by one consumer. Producers may enqueue commands, but only
/// the system that owns <typeparamref name="TCommand"/> should dequeue them.
/// </summary>
/// <remarks>This type is intended for a single-threaded update loop.</remarks>
public sealed class CommandQueue<TCommand>
{
    private readonly Queue<TCommand> _commands = new();

    public int Count => _commands.Count;

    public void Enqueue(TCommand command) => _commands.Enqueue(command);

    public bool TryDequeue(out TCommand command) => _commands.TryDequeue(out command!);

    /// <summary>
    /// Processes the commands that were queued when this call began. Commands
    /// enqueued by <paramref name="handler"/> remain queued for the next drain.
    /// </summary>
    public int Drain(Action<TCommand> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        int count = _commands.Count;
        for (int i = 0; i < count; i++)
            handler(_commands.Dequeue());

        return count;
    }

    public void Clear() => _commands.Clear();
}
