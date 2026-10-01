using Engine.Messaging;
using Xunit;

namespace Engine.Tests;

public sealed class MessagingTests
{
    [Fact]
    public void CommandQueueDrainsOnlyTheInitialBatch()
    {
        var queue = new CommandQueue<int>();
        var handled = new List<int>();
        queue.Enqueue(1);
        queue.Enqueue(2);

        int count = queue.Drain(command =>
        {
            handled.Add(command);
            if (command == 1)
                queue.Enqueue(3);
        });

        Assert.Equal(2, count);
        Assert.Equal(new[] { 1, 2 }, handled);
        Assert.Equal(1, queue.Count);
        Assert.True(queue.TryDequeue(out int remaining));
        Assert.Equal(3, remaining);
    }

    [Fact]
    public void BroadcastChannelPublishesToTheNextBatch()
    {
        var channel = new BroadcastChannel<int>();

        channel.Publish(1);
        Assert.Empty(channel.Messages.ToArray());
        Assert.Equal(1, channel.PendingCount);

        channel.AdvanceFrame();
        Assert.Equal(new[] { 1 }, channel.Messages.ToArray());

        channel.Publish(2);
        Assert.Equal(new[] { 1 }, channel.Messages.ToArray());

        channel.AdvanceFrame();
        Assert.Equal(new[] { 2 }, channel.Messages.ToArray());
        Assert.Equal(0, channel.PendingCount);
    }
}
