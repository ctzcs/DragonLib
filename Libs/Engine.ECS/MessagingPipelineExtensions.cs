using DCFApixels.DragonECS;
using Engine.Messaging;

namespace Engine.ECS;

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

internal sealed class AdvanceBroadcastChannelSystem<TMessage> :
    IUpdateSystem,
    IEcsInject<BroadcastChannel<TMessage>>
{
    private BroadcastChannel<TMessage> _channel = null!;

    public void Inject(BroadcastChannel<TMessage> channel) => _channel = channel;

    public void Update() => _channel.AdvanceFrame();
}

public static class BroadcastChannelPipelineExtensions
{
    /// <summary>
    /// Registers a broadcast channel for injection and advances it at the start
    /// of every update.
    /// </summary>
    public static EcsPipeline.Builder AddBroadcastChannel<TMessage>(
        this EcsPipeline.Builder builder,
        BroadcastChannel<TMessage> channel,
        string layerName = EcsConsts.PRE_BEGIN_LAYER)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentException.ThrowIfNullOrEmpty(layerName);

        return builder
            .Inject(channel)
            .AddUnique(new AdvanceBroadcastChannelSystem<TMessage>(), layerName);
    }

    /// <summary>Creates and registers a broadcast channel for injection.</summary>
    public static EcsPipeline.Builder AddBroadcastChannel<TMessage>(
        this EcsPipeline.Builder builder,
        out BroadcastChannel<TMessage> channel,
        string layerName = EcsConsts.PRE_BEGIN_LAYER)
    {
        channel = new BroadcastChannel<TMessage>();
        return builder.AddBroadcastChannel(channel, layerName);
    }
}
