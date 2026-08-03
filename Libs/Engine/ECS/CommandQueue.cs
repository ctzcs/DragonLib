using DCFApixels.DragonECS;

namespace Engine.ECS;

/// <summary>
/// 单消费者模型，本帧处理
/// A FIFO queue owned by one consumer. Producers may enqueue commands, but only
/// the system that owns <typeparamref name="TCommand"/> should dequeue them.
/// </summary>
/// <remarks>This type is intended for the single-threaded ECS update loop.</remarks>
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

public static class CommandQueuePipelineExtensions
{
    /// <summary>Registers an existing command queue for pipeline injection.</summary>
    public static EcsPipeline.Builder AddCommandQueue<TCommand>(
        this EcsPipeline.Builder builder,
        CommandQueue<TCommand> queue)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(queue);
        return builder.Inject(queue);
    }

    /// <summary>Creates and registers a command queue for pipeline injection.</summary>
    public static EcsPipeline.Builder AddCommandQueue<TCommand>(
        this EcsPipeline.Builder builder,
        out CommandQueue<TCommand> queue)
    {
        queue = new CommandQueue<TCommand>();
        return builder.AddCommandQueue(queue);
    }
}
