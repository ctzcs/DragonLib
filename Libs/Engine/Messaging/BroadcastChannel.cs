using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Engine.Messaging;

/// <summary>
/// 多消费者模型，延迟一帧处理，确定性高，所有消费者读到的事件一样，最终统一销毁
/// A strict double-buffered broadcast channel. Messages published during one
/// update become visible to every consumer at the start of the next update.
/// </summary>
/// <remarks>This type is intended for a single-threaded update loop.</remarks>
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
    /// Call once at the start of each update, before producers and consumers run.
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
