using System;

namespace Engine.Assets;

/// <summary>
/// 简化版 Snowflake ID 生成器。
/// 布局（共 63 bit，最高位符号位保持为 0，所以生成的 long 恒为正）：
///   [1 bit sign=0][41 bit timestamp ms][10 bit workerId][12 bit sequence]
/// - 41 bit 时间戳：相对 <see cref="Epoch"/> 的毫秒偏移，可用约 69 年
/// - 10 bit workerId：用于区分不同进程/机器/工具，默认 0（编辑器可设 1、导表工具可设 2 等）
/// - 12 bit sequence：同一毫秒内递增，最多 4096 个 id/ms/worker
/// 相比 Random.NextInt64：
///   * 无冲突（时间戳 + 序列号加锁）
///   * 天然按创建顺序有序
///   * 可反解出创建时间，方便调试
/// </summary>
public static class AssetIdGenerator
{
    // 2025-01-01 00:00:00 UTC，作为时间戳基准，节省高位空间
    public static readonly DateTime Epoch = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly long EpochMs =
        (long)(Epoch - DateTime.UnixEpoch).TotalMilliseconds;

    private const int WorkerIdBits = 10;
    private const int SequenceBits = 12;
    private const int TimestampShift = WorkerIdBits + SequenceBits; // 22
    private const int WorkerIdShift = SequenceBits;                 // 12

    public const long MaxWorkerId = (1L << WorkerIdBits) - 1;   // 1023
    private const long SequenceMask = (1L << SequenceBits) - 1; // 4095

    private static readonly object _lock = new();
    private static long _lastTimestamp = -1L;
    private static long _sequence = 0L;

    /// <summary>
    /// 当前进程的 workerId。默认 0；编辑器/工具进程建议设置成不同值以避免与游戏运行时冲突。
    /// </summary>
    public static long WorkerId { get; private set; } = 0;

    /// <summary>
    /// 设置 workerId。应在生成任何 id 之前调用一次。
    /// </summary>
    public static void Configure(long workerId)
    {
        if (workerId < 0 || workerId > MaxWorkerId)
            throw new ArgumentOutOfRangeException(nameof(workerId),
                $"workerId must be in [0, {MaxWorkerId}].");
        WorkerId = workerId;
    }

    public static long Next()
    {
        lock (_lock)
        {
            long timestamp = CurrentTimestampMs();

            if (timestamp < _lastTimestamp)
            {
                // 时钟回拨：短距离直接等待，长距离直接抛异常，避免生成重复 id
                long offset = _lastTimestamp - timestamp;
                if (offset <= 5)
                {
                    System.Threading.Thread.Sleep((int)offset + 1);
                    timestamp = CurrentTimestampMs();
                    if (timestamp < _lastTimestamp)
                        throw new InvalidOperationException(
                            $"Clock moved backwards. Refusing to generate id for {offset}ms.");
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Clock moved backwards by {offset}ms. Refusing to generate id.");
                }
            }

            if (timestamp == _lastTimestamp)
            {
                _sequence = (_sequence + 1) & SequenceMask;
                if (_sequence == 0)
                {
                    // 当前毫秒序列号用尽，忙等到下一毫秒
                    do { timestamp = CurrentTimestampMs(); }
                    while (timestamp <= _lastTimestamp);
                }
            }
            else
            {
                _sequence = 0;
            }

            _lastTimestamp = timestamp;

            long id = ((timestamp - EpochMs) << TimestampShift)
                    | (WorkerId << WorkerIdShift)
                    | _sequence;

            // 极端兜底：id 不能是 0（0 保留给 AssetId.None）
            return id == 0 ? Next() : id;
        }
    }

    /// <summary>
    /// 从一个 id 反解出它的创建时间，方便调试与日志。
    /// </summary>
    public static DateTime GetCreatedTime(long id)
    {
        long ms = (id >> TimestampShift) + EpochMs;
        return DateTime.UnixEpoch.AddMilliseconds(ms);
    }

    /// <summary>
    /// 反解出 id 的 workerId。
    /// </summary>
    public static long GetWorkerId(long id) => (id >> WorkerIdShift) & MaxWorkerId;

    /// <summary>
    /// 反解出 id 的同毫秒序列号。
    /// </summary>
    public static long GetSequence(long id) => id & SequenceMask;

    private static long CurrentTimestampMs()
        => (long)(DateTime.UtcNow - DateTime.UnixEpoch).TotalMilliseconds;
}