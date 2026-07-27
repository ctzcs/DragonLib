# Entities benchmarks

Run the complete benchmark suite from `Tests/Game0`:

```powershell
dotnet run -c Release --project Benchmarks\Entities.Benchmarks.csproj
```

Run only a subset while iterating:

```powershell
dotnet run -c Release --project Benchmarks\Entities.Benchmarks.csproj -- --filter "*DenseForEach*"
```

The iteration benchmarks process the entire `EntityCount` in one invocation.
Divide `Mean` by `EntityCount` to get the approximate cost per entity. Compare
`DenseForEach` with `RawDenseBuffer` to estimate iteration bookkeeping cost, and
compare dense with sparse traversal to expose the cost of scanning dead slots.

Use a Release build, close CPU-heavy applications, and do not attach a debugger.
BenchmarkDotNet reports are written under `BenchmarkDotNet.Artifacts`.
