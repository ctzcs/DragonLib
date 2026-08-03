using DCFApixels.DragonECS;
using Engine.ECS;
using Xunit;

namespace Game0.EntitiesTests;

public sealed class EcsMessagingTests
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

    [Fact]
    public void PipelineAdvancesBroadcastBeforeRegularUpdateSystems()
    {
        var producer = new BroadcastProducer();
        var consumer = new BroadcastConsumer();
        EcsPipeline pipeline = EcsPipeline.New()
            .AddBroadcastChannel<int>(out _)
            .Add(producer)
            .Add(consumer)
            .BuildAndInit();

        pipeline.Update();
        Assert.Empty(consumer.LastBatch);

        pipeline.Update();
        Assert.Equal(new[] { 1 }, consumer.LastBatch);

        pipeline.Destroy();
    }

    private sealed class BroadcastProducer : IUpdateSystem, IEcsInject<BroadcastChannel<int>>
    {
        private BroadcastChannel<int> _channel = null!;
        private int _next = 1;

        public void Inject(BroadcastChannel<int> channel) => _channel = channel;

        public void Update() => _channel.Publish(_next++);
    }

    private sealed class BroadcastConsumer : IUpdateSystem, IEcsInject<BroadcastChannel<int>>
    {
        private BroadcastChannel<int> _channel = null!;
        public int[] LastBatch { get; private set; } = [];

        public void Inject(BroadcastChannel<int> channel) => _channel = channel;

        public void Update() => LastBatch = _channel.Messages.ToArray();
    }
}
