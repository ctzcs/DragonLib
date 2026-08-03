using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using DCFApixels.DragonECS;
using Engine.ECS;
using ImGuiNET;

namespace Game0.Content.Demos;

public sealed class EntitiesDemo : IEcsInit, IEcsDestroy, IUpdateSystem, IRenderSystem
{
    private const int MinEntityCount = 1_000;
    private const int MaxEntityCount = 2_000_000;
    private const int MinIterations = 1;
    private const int MaxIterations = 200;

    private static readonly EntityRefAction<PerfEntity> UpdateEntity =
        static (EntityHandle _, ref PerfEntity entity) =>
        {
            entity.Position.X += entity.Velocity.X;
            entity.Position.Y += entity.Velocity.Y;
        };

    private static readonly EntityRefAction<PositionComponent> UpdatePosition =
        static (EntityHandle _, ref PositionComponent position) =>
        {
            position.X += 1f;
            position.Y -= 1f;
        };

    private Entities _denseEntities = null!;
    private Entities _sparseEntities = null!;
    private EntityHandle[] _denseHandles = Array.Empty<EntityHandle>();
    private EntityHandle[] _lifecycleHandles = Array.Empty<EntityHandle>();
    private BenchmarkResult[] _results = Array.Empty<BenchmarkResult>();
    private int _entityCount = 100_000;
    private int _builtEntityCount;
    private int _iterations = 25;
    private int _sparseAliveCount;
    private float _checksum;
    private string _status = "Ready";

    public void Init()
    {
        RebuildDatasets();
    }

    public void Destroy()
    {
        _results = Array.Empty<BenchmarkResult>();
        _denseHandles = Array.Empty<EntityHandle>();
        _lifecycleHandles = Array.Empty<EntityHandle>();
    }

    public void Update()
    {
        ImGui.SetNextWindowSize(new Vector2(820f, 0f), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Entities Performance"))
        {
            ImGui.End();
            return;
        }

        ImGui.SetNextItemWidth(180f);
        ImGui.InputInt("Entities", ref _entityCount, 10_000, 100_000);
        _entityCount = Math.Clamp(_entityCount, MinEntityCount, MaxEntityCount);

        ImGui.SetNextItemWidth(180f);
        ImGui.InputInt("Iterations", ref _iterations, 1, 10);
        _iterations = Math.Clamp(_iterations, MinIterations, MaxIterations);

        if (ImGui.Button("Rebuild dataset"))
            RebuildDatasets();

        ImGui.SameLine();
        if (ImGui.Button("Run benchmark"))
            RunBenchmarkSuite();

        ImGui.SameLine();
        ImGui.TextDisabled(_status);
        ImGui.TextDisabled("Run a Release build without a debugger for meaningful numbers.");
        ImGui.Separator();

        if (_results.Length == 0)
        {
            ImGui.TextDisabled("No results yet.");
            ImGui.End();
            return;
        }

        if (ImGui.BeginTable("entities_benchmark_results", 6,
                ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg |
                ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("Benchmark");
            ImGui.TableSetupColumn("Ops/run");
            ImGui.TableSetupColumn("Mean ms");
            ImGui.TableSetupColumn("P95 ms");
            ImGui.TableSetupColumn("ns/op");
            ImGui.TableSetupColumn("Alloc/run");
            ImGui.TableHeadersRow();

            foreach (var result in _results)
            {
                ImGui.TableNextRow();
                WriteCell(result.Name);
                WriteCell(result.OperationsPerRun.ToString("N0"));
                WriteCell(result.MeanMilliseconds.ToString("F3"));
                WriteCell(result.P95Milliseconds.ToString("F3"));
                WriteCell(result.NanosecondsPerOperation.ToString("F2"));
                WriteCell(FormatBytes(result.AllocatedBytesPerRun));
            }

            ImGui.EndTable();
        }

        ImGui.TextDisabled(
            $"Dense: {_entityCount:N0} alive | Sparse: {_sparseAliveCount:N0} alive / {_entityCount:N0} slots");
        ImGui.End();
    }

    public void Render()
    {
    }

    private void RebuildDatasets()
    {
        _status = "Building...";
        _results = Array.Empty<BenchmarkResult>();
        _denseHandles = new EntityHandle[_entityCount];

        _denseEntities = CreateRegisteredEntities(_entityCount);
        _sparseEntities = CreateRegisteredEntities(_entityCount);

        for (var i = 0; i < _entityCount; i++)
        {
            var entity = CreatePerfEntity(i);
            _denseHandles[i] = _denseEntities.Create(entity);
            var sparseHandle = _sparseEntities.Create(entity);

            // Keep the high-water mark unchanged while leaving 50% holes.
            if ((i & 1) != 0)
                _sparseEntities.Destroy(sparseHandle);
        }

        _sparseAliveCount = _sparseEntities.Count;
        _lifecycleHandles = new EntityHandle[_entityCount];
        _builtEntityCount = _entityCount;
        _status = $"Built {_entityCount:N0} entities";
    }

    private void RunBenchmarkSuite()
    {
        if (_builtEntityCount != _entityCount)
            RebuildDatasets();

        _status = "Benchmarking...";

        // Force collection before measuring so old datasets do not add GC noise.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var results = new List<BenchmarkResult>(8)
        {
            Measure("Dense ForEach", _entityCount, DenseForEach),
            Measure("Sparse 50% ForEach", _sparseAliveCount, SparseForEach),
            Measure("Component ForEach", _entityCount, ComponentForEach),
            Measure("Sequential GetRef", _entityCount, SequentialGetRef),
            Measure("Component GetRef", _entityCount, ComponentGetRef),
            Measure("Raw dense buffer", _entityCount, RawDenseBuffer)
        };

        MeasureLifecycle(out var create, out var destroy);
        results.Add(create);
        results.Add(destroy);

        _results = results.ToArray();
        _status = $"Completed {_iterations} iterations";
    }

    private BenchmarkResult Measure(string name, int operationsPerRun, Action action)
    {
        const int warmupIterations = 4;
        for (var i = 0; i < warmupIterations; i++)
            action();

        var elapsedTicks = new long[_iterations];
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

        for (var i = 0; i < _iterations; i++)
        {
            var startedAt = Stopwatch.GetTimestamp();
            action();
            elapsedTicks[i] = Stopwatch.GetTimestamp() - startedAt;
        }

        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        return BenchmarkResult.FromTicks(
            name,
            operationsPerRun,
            elapsedTicks,
            allocatedBytes / _iterations);
    }

    private void MeasureLifecycle(out BenchmarkResult create, out BenchmarkResult destroy)
    {
        var entities = CreateRegisteredEntities(_entityCount);

        // Warm up both the sequential allocation and free-list reuse paths.
        for (var warmup = 0; warmup < 2; warmup++)
        {
            CreateAll(entities);
            DestroyAll(entities);
        }

        var createTicks = new long[_iterations];
        var destroyTicks = new long[_iterations];
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

        for (var iteration = 0; iteration < _iterations; iteration++)
        {
            var startedAt = Stopwatch.GetTimestamp();
            CreateAll(entities);
            createTicks[iteration] = Stopwatch.GetTimestamp() - startedAt;

            startedAt = Stopwatch.GetTimestamp();
            DestroyAll(entities);
            destroyTicks[iteration] = Stopwatch.GetTimestamp() - startedAt;
        }

        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        var allocatedPerRun = allocatedBytes / (_iterations * 2);
        create = BenchmarkResult.FromTicks(
            "Create (reuse)", _entityCount, createTicks, allocatedPerRun);
        destroy = BenchmarkResult.FromTicks(
            "Destroy", _entityCount, destroyTicks, allocatedPerRun);
    }

    private void CreateAll(Entities entities)
    {
        for (var i = 0; i < _entityCount; i++)
            _lifecycleHandles[i] = entities.Create(CreatePerfEntity(i));
    }

    private void DestroyAll(Entities entities)
    {
        for (var i = 0; i < _entityCount; i++)
            entities.Destroy(_lifecycleHandles[i]);
    }

    private void DenseForEach()
    {
        _denseEntities.ForEach(UpdateEntity);
    }

    private void SparseForEach()
    {
        _sparseEntities.ForEach(UpdateEntity);
    }

    private void ComponentForEach()
    {
        _denseEntities.ForEachComponent(UpdatePosition);
    }

    private void SequentialGetRef()
    {
        var sum = 0f;
        for (var i = 0; i < _denseHandles.Length; i++)
            sum += _denseEntities.GetRef<PerfEntity>(_denseHandles[i]).Position.X;

        _checksum = sum;
    }

    private void ComponentGetRef()
    {
        var sum = 0f;
        for (var i = 0; i < _denseHandles.Length; i++)
            sum += _denseEntities.GetComponentRef<PositionComponent>(_denseHandles[i]).X;

        _checksum = sum;
    }

    private void RawDenseBuffer()
    {
        var buffer = _denseEntities.DangerousGetBuffer<PerfEntity>(out var highWaterMark);
        for (var i = 1; i <= highWaterMark; i++)
            buffer[i].Position.X += buffer[i].Velocity.X;

        _checksum = buffer[highWaterMark].Position.X;
    }

    private static Entities CreateRegisteredEntities(int capacity)
    {
        var entities = new Entities(capacity);
        entities.Register<PerfEntity>(capacity);
        return entities;
    }

    private static PerfEntity CreatePerfEntity(int index)
    {
        return new PerfEntity
        {
            Position = new PositionComponent { X = index, Y = -index },
            Velocity = new VelocityComponent { X = 0.25f, Y = -0.5f }
        };
    }

    private static void WriteCell(string text)
    {
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(text);
    }

    private static string FormatBytes(long bytes)
    {
        return bytes >= 1024
            ? $"{bytes / 1024d:F2} KiB"
            : $"{bytes} B";
    }

    private readonly record struct BenchmarkResult(
        string Name,
        int OperationsPerRun,
        double MeanMilliseconds,
        double P95Milliseconds,
        double NanosecondsPerOperation,
        long AllocatedBytesPerRun)
    {
        public static BenchmarkResult FromTicks(
            string name,
            int operationsPerRun,
            long[] ticks,
            long allocatedBytesPerRun)
        {
            Array.Sort(ticks);

            long totalTicks = 0;
            foreach (var value in ticks)
                totalTicks += value;

            var meanTicks = totalTicks / (double)ticks.Length;
            var p95Index = Math.Clamp(
                (int)Math.Ceiling(ticks.Length * 0.95) - 1,
                0,
                ticks.Length - 1);
            var meanMilliseconds = meanTicks * 1000d / Stopwatch.Frequency;
            var p95Milliseconds = ticks[p95Index] * 1000d / Stopwatch.Frequency;
            var nanosecondsPerOperation =
                meanTicks * 1_000_000_000d / Stopwatch.Frequency / operationsPerRun;

            return new BenchmarkResult(
                name,
                operationsPerRun,
                meanMilliseconds,
                p95Milliseconds,
                nanosecondsPerOperation,
                allocatedBytesPerRun);
        }
    }
}

public struct PerfEntity
{
    public PositionComponent Position;
    public VelocityComponent Velocity;
}

[EntitiesComponent]
public struct PositionComponent
{
    public float X;
    public float Y;
}

[EntitiesComponent]
public struct VelocityComponent
{
    public float X;
    public float Y;
}
