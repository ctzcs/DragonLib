using DCFApixels.DragonECS;
using Engine.ECS;
using Engine.Messaging;
using Xunit;

namespace Game0.EntitiesTests;

public sealed class EcsMessagingTests
{
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
