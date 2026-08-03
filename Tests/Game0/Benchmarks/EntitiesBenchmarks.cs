using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace Game0.EntitiesBenchmarks;

[MemoryDiagnoser]
[RankColumn]
[ShortRunJob]
public class EntitiesIterationBenchmarks
{
    private static readonly EntityRefAction<BenchmarkEntity> UpdateEntity =
        static (EntityHandle _, ref BenchmarkEntity entity) =>
        {
            entity.Position.X += entity.Velocity.X;
            entity.Position.Y += entity.Velocity.Y;
        };

    private static readonly EntityRefAction<BenchmarkPosition> UpdatePosition =
        static (EntityHandle _, ref BenchmarkPosition position) =>
        {
            position.X += 1f;
            position.Y -= 1f;
        };

    private Entities _dense = null!;
    private Entities _sparse = null!;
    private EntityHandle[] _handles = null!;
    private int[] _randomOrder = null!;

    [Params(1_000, 100_000, 1_000_000)]
    public int EntityCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _dense = CreateRegisteredEntities(EntityCount);
        _sparse = CreateRegisteredEntities(EntityCount);
        _handles = new EntityHandle[EntityCount];
        _randomOrder = Enumerable.Range(0, EntityCount).ToArray();

        var random = new Random(42);
        random.Shuffle(_randomOrder);

        for (var i = 0; i < EntityCount; i++)
        {
            var entity = CreateEntity(i);
            _handles[i] = _dense.Create(entity);
            var sparseHandle = _sparse.Create(entity);

            if ((i & 1) != 0)
                _sparse.Destroy(sparseHandle);
        }
    }

    [Benchmark(Baseline = true)]
    public float DenseForEach()
    {
        _dense.ForEach(UpdateEntity);
        return _dense.GetRef<BenchmarkEntity>(_handles[^1]).Position.X;
    }

    [Benchmark]
    public int Sparse50PercentForEach()
    {
        _sparse.ForEach(UpdateEntity);
        return _sparse.Count;
    }

    [Benchmark]
    public float ComponentForEach()
    {
        _dense.ForEachComponent(UpdatePosition);
        return _dense.GetRef<BenchmarkEntity>(_handles[^1]).Position.X;
    }

    [Benchmark]
    public float SequentialGetRef()
    {
        var sum = 0f;
        for (var i = 0; i < _handles.Length; i++)
            sum += _dense.GetRef<BenchmarkEntity>(_handles[i]).Position.X;

        return sum;
    }

    [Benchmark]
    public float RandomGetRef()
    {
        var sum = 0f;
        for (var i = 0; i < _randomOrder.Length; i++)
            sum += _dense.GetRef<BenchmarkEntity>(_handles[_randomOrder[i]]).Position.X;

        return sum;
    }

    [Benchmark]
    public float ComponentGetRef()
    {
        var sum = 0f;
        for (var i = 0; i < _handles.Length; i++)
            sum += _dense.GetComponentRef<BenchmarkPosition>(_handles[i]).X;

        return sum;
    }

    [Benchmark]
    public float RawDenseBuffer()
    {
        var buffer = _dense.DangerousGetBuffer<BenchmarkEntity>(out var highWaterMark);
        for (var i = 1; i <= highWaterMark; i++)
            buffer[i].Position.X += buffer[i].Velocity.X;

        return buffer[highWaterMark].Position.X;
    }

    private static Entities CreateRegisteredEntities(int capacity)
    {
        var entities = new Entities(capacity);
        entities.Register<BenchmarkEntity>(capacity);
        return entities;
    }

    private static BenchmarkEntity CreateEntity(int index)
    {
        return new BenchmarkEntity
        {
            Position = new BenchmarkPosition { X = index, Y = -index },
            Velocity = new BenchmarkVelocity { X = 0.25f, Y = -0.5f }
        };
    }
}

[MemoryDiagnoser]
[RankColumn]
[ShortRunJob]
public class EntitiesLifecycleBenchmarks
{
    private Entities _entities = null!;
    private EntityHandle[] _handles = null!;

    [Params(1_000, 100_000, 1_000_000)]
    public int EntityCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _entities = new Entities(EntityCount);
        _entities.Register<BenchmarkEntity>(EntityCount);
        _handles = new EntityHandle[EntityCount];

        for (var i = 0; i < EntityCount; i++)
            _handles[i] = _entities.Create(CreateEntity(i));
    }

    [Benchmark]
    public int DestroyAndReuseBatch()
    {
        for (var i = 0; i < _handles.Length; i++)
            _entities.Destroy(_handles[i]);

        for (var i = 0; i < _handles.Length; i++)
            _handles[i] = _entities.Create(CreateEntity(i));

        return _entities.Count;
    }

    private static BenchmarkEntity CreateEntity(int index)
    {
        return new BenchmarkEntity
        {
            Position = new BenchmarkPosition { X = index, Y = -index },
            Velocity = new BenchmarkVelocity { X = 0.25f, Y = -0.5f }
        };
    }
}

public struct BenchmarkEntity
{
    public BenchmarkPosition Position;
    public BenchmarkVelocity Velocity;
}

[EntitiesComponent]
public struct BenchmarkPosition
{
    public float X;
    public float Y;
}

[EntitiesComponent]
public struct BenchmarkVelocity
{
    public float X;
    public float Y;
}
