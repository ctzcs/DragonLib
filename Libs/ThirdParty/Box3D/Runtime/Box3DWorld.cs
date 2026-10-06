using System.Numerics;
using global::Box3D;

namespace DragonLib.Box3D;

/// <summary>拥有原生世界；以固定时间步推进，可在每步结束后读取事件。</summary>
public sealed class Box3DWorld : IDisposable
{
    private readonly PhysicsWorld _simulation;
    private double _accumulator;

    public bool IsDisposed { get; private set; }
    public float FixedDeltaSeconds { get; }
    public int SubStepCount { get; }
    public int MaxCatchUpSteps { get; }
    public ulong StepCount { get; private set; }
    public double DroppedSeconds { get; private set; }
    public float InterpolationAlpha => (float)(_accumulator / FixedDeltaSeconds);

    public PhysicsWorld Simulation
    {
        get
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            return _simulation;
        }
    }

    public Box3DWorld(Vector3 gravity, float fixedDeltaSeconds = 1f / 60f,
        int subStepCount = 4, int maxCatchUpSteps = 8)
        : this(WorldSettings.Default with { Gravity = gravity }, fixedDeltaSeconds, subStepCount, maxCatchUpSteps)
    {
    }

    public Box3DWorld(in WorldSettings settings, float fixedDeltaSeconds = 1f / 60f,
        int subStepCount = 4, int maxCatchUpSteps = 8)
    {
        if (!float.IsFinite(fixedDeltaSeconds) || fixedDeltaSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(fixedDeltaSeconds));
        if (subStepCount <= 0) throw new ArgumentOutOfRangeException(nameof(subStepCount));
        if (maxCatchUpSteps <= 0) throw new ArgumentOutOfRangeException(nameof(maxCatchUpSteps));
        FixedDeltaSeconds = fixedDeltaSeconds;
        SubStepCount = subStepCount;
        MaxCatchUpSteps = maxCatchUpSteps;
        _simulation = new PhysicsWorld(settings);
    }

    /// <summary>推进恰好一个固定步。暂停时的单步调试也走同一求解参数。</summary>
    public void Step()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        _simulation.Step(FixedDeltaSeconds, SubStepCount);
        StepCount++;
    }

    /// <summary>
    /// 累积帧时间；长帧最多接受 MaxCatchUpSteps 步的时间，保留此前的小数余量。
    /// afterStep 必须当场读取事件，因为下一步会覆盖原生事件缓冲区。
    /// </summary>
    public int Advance(double deltaSeconds, Action<PhysicsWorld>? afterStep = null)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (!double.IsFinite(deltaSeconds) || deltaSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));

        // 先限制积压，避免超长帧或极大输入让累计时间溢出。
        var budget = (double)FixedDeltaSeconds * MaxCatchUpSteps;
        var accepted = Math.Min(deltaSeconds, budget);
        DroppedSeconds += deltaSeconds - accepted;
        _accumulator += accepted;
        var steps = 0;
        while (_accumulator >= FixedDeltaSeconds && steps < MaxCatchUpSteps)
        {
            Step();
            _accumulator -= FixedDeltaSeconds;
            steps++;
            afterStep?.Invoke(_simulation);
        }
        return steps;
    }

    /// <summary>切场景/取消暂停时丢弃未走完的一步，避免把等待时间带进新场景。</summary>
    public void ResetAccumulator()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        _accumulator = 0;
    }

    public void Dispose()
    {
        if (IsDisposed) return;
        _simulation.Dispose();
        IsDisposed = true;
        _accumulator = 0;
    }
}
