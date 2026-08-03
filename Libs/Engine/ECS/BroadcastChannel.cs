using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using DCFApixels.DragonECS;

namespace Engine.ECS;

/// <summary>
/// 多消费者模型，延迟一帧处理，确定性高，所有消费者读到的事件一样，最终统一销毁
/// A strict double-buffered broadcast channel. Messages published during one
/// update become visible to every consumer at the start of the next update.
/// </summary>
/// <remarks>This type is intended for the single-threaded ECS update loop.</remarks>
public sealed class BroadcastChannel<TMessage>
{
    private List<TMessage> _current = new();
    private List<TMessage> _pending = new();

    /// <summary>The immutable batch visible during the current update.</summary>
    public ReadOnlySpan<TMessage> Messages => CollectionsMarshal.AsSpan(_current);

    public int Count => _current.Count;
    public int PendingCount => _pending.Count;

    /// <summary>Publishes a message for the next update.</summary>
    public void Publish(TMessage message) => _pending.Add(message);

    /// <summary>
    /// Makes the pending batch current and discards the previously visible batch.
    /// Normally called automatically by <see cref="AddBroadcastChannel{TMessage}(EcsPipeline.Builder, BroadcastChannel{TMessage}, string)"/>.
    /// </summary>
    public void AdvanceFrame()
    {
        (_current, _pending) = (_pending, _current);
        _pending.Clear();
    }

    public void Clear()
    {
        _current.Clear();
        _pending.Clear();
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
