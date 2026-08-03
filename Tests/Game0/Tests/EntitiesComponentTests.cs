using Xunit;

namespace Game0.EntitiesTests;

#pragma warning disable CS0649 // Fields intentionally exist only for reflection tests.

public sealed class EntitiesComponentTests
{
    [Fact]
    public void RegisterAutomaticallyExposesMarkedComponentFields()
    {
        var entities = new Entities();
        entities.Register<TestEntity>(4);

        var handle = entities.Create(new TestEntity
        {
            Position = new TestPosition { X = 10 },
            InternalValue = 20
        });

        Assert.True(entities.HasComponent<TestPosition>(handle));
        Assert.False(entities.HasComponent<UnmarkedComponent>(handle));

        ref var position = ref entities.GetComponentRef<TestPosition>(handle);
        position.X = 42;

        Assert.Equal(42, entities.Get<TestEntity>(handle).Position.X);
    }

    [Fact]
    public void FirstCreateUsesAutomaticRegistrationAndDefaultCapacity()
    {
        var entities = new Entities(defaultCapacity: 7);

        var handle = entities.Create(new TestEntity
        {
            Position = new TestPosition { X = 3 }
        });

        Assert.Equal(7, entities.CapacityOf<TestEntity>());
        Assert.True(entities.HasComponent<TestPosition>(handle));
    }

    [Fact]
    public void SharedComponentIterationVisitsEveryRegisteredEntityType()
    {
        var entities = new Entities();
        entities.Register<TestEntity>(2);
        entities.Register<SecondTestEntity>(2);

        var first = entities.Create(new TestEntity
        {
            Position = new TestPosition { X = 1 }
        });
        var second = entities.Create(new SecondTestEntity
        {
            Position = new TestPosition { X = 2 }
        });

        entities.ForEachComponent<TestPosition>(
            static (EntityHandle _, ref TestPosition position) => position.X++);

        Assert.Equal(2, entities.GetComponentRef<TestPosition>(first).X);
        Assert.Equal(3, entities.GetComponentRef<TestPosition>(second).X);
    }

    [Fact]
    public void PrivateComponentFieldsCanBeRegistered()
    {
        var entities = new Entities();
        entities.Register<PrivateFieldEntity>(1);
        var handle = entities.Create(new PrivateFieldEntity(5));

        ref var component = ref entities.GetComponentRef<TestPosition>(handle);
        component.X = 9;

        Assert.Equal(9, entities.Get<PrivateFieldEntity>(handle).PositionX);
    }

    [Fact]
    public void UnmarkedComponentsCanStillBeRegisteredManually()
    {
        var entities = new Entities();
        entities.Register<ManualEntity>(1);
        entities.RegisterComponent<ManualEntity, UnmarkedComponent>(
            static (ref ManualEntity entity) => ref entity.Component);
        var handle = entities.Create(new ManualEntity());

        Assert.True(entities.HasComponent<UnmarkedComponent>(handle));
    }

    [Fact]
    public void ReadonlyComponentFieldFailsBeforeEntityTypeIsRegistered()
    {
        var entities = new Entities();

        var error = Assert.Throws<InvalidOperationException>(
            () => entities.Register<ReadonlyComponentEntity>(1));

        Assert.Contains("readonly", error.Message);
        Assert.Equal(0, entities.RegisteredTypeCount);
    }

    [Fact]
    public void DuplicateComponentFieldsFailBeforeEntityTypeIsRegistered()
    {
        var entities = new Entities();

        var error = Assert.Throws<InvalidOperationException>(
            () => entities.Register<DuplicateComponentEntity>(1));

        Assert.Contains("multiple", error.Message);
        Assert.Equal(0, entities.RegisteredTypeCount);
    }

    [EntitiesComponent]
    private struct TestPosition
    {
        public int X;
    }

    private struct UnmarkedComponent
    {
        public int Value;
    }

    private struct TestEntity
    {
        public TestPosition Position;
        public int InternalValue;
        public UnmarkedComponent Unmarked;
    }

    private struct SecondTestEntity
    {
        public TestPosition Position;
    }

    private struct PrivateFieldEntity
    {
        private TestPosition _position;

        public PrivateFieldEntity(int x)
        {
            _position = new TestPosition { X = x };
        }

        public int PositionX => _position.X;
    }

    private struct ManualEntity
    {
        public UnmarkedComponent Component;
    }

    private readonly struct ReadonlyComponentEntity
    {
        public readonly TestPosition Position;
    }

    private struct DuplicateComponentEntity
    {
        public TestPosition First;
        public TestPosition Second;
    }
}

#pragma warning restore CS0649
