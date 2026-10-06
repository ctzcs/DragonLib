using Engine.Assets.Dasset;

namespace Engine.Animation;

public readonly record struct AnimationEvent3D(float Time, string Name);
public readonly record struct AnimationEventOccurrence3D(AnimationEvent3D Marker, double PlaybackTime);

/// <summary>独立剪辑时钟；绝对时间用于跨循环事件和根运动，Time 用于已有 SkeletonAnimator 采样。</summary>
public sealed class AnimationPlayback3D
{
    private readonly AnimationEvent3D[] _markers;
    private readonly List<AnimationEventOccurrence3D> _events = [];
    public DassetAnimationClip Clip { get; }
    public bool Loop { get; set; }
    public float Speed { get; set; } = 1;
    public double Position { get; private set; }
    public float Time => SampleTime(Position, Clip.Duration, Loop);
    public IReadOnlyList<AnimationEventOccurrence3D> Events => _events;
    /// <summary>单次最多发出 1024 个事件，超限数量显式报告，避免异常 dt 导致无界分配。</summary>
    public long DroppedEvents { get; private set; }

    public AnimationPlayback3D(DassetAnimationClip clip, bool loop = true, IEnumerable<AnimationEvent3D>? markers = null)
    {
        ArgumentNullException.ThrowIfNull(clip);
        if (!float.IsFinite(clip.Duration) || clip.Duration < 0) throw new ArgumentException("Invalid clip duration.", nameof(clip));
        Clip = clip; Loop = loop;
        var sorted = new List<AnimationEvent3D>();
        if (markers != null)
            foreach (var marker in markers)
            {
                if (!float.IsFinite(marker.Time) || marker.Time < 0 || marker.Time > clip.Duration || marker.Name == null)
                    throw new ArgumentException("Event time must lie in the clip and its name must be non-null.", nameof(markers));
                sorted.Add(marker);
            }
        sorted.Sort((a, b) => a.Time.CompareTo(b.Time));
        _markers = sorted.ToArray();
    }

    /// <summary>跳转不发事件。非循环剪辑 clamp，循环位置保留圈数；播放开始处事件不自动触发。</summary>
    public void Seek(double position)
    {
        if (!double.IsFinite(position)) throw new ArgumentOutOfRangeException(nameof(position));
        Position = Loop && Clip.Duration > 0 ? position : Math.Clamp(position, 0, Clip.Duration);
        _events.Clear(); DroppedEvents = 0;
    }

    public void Advance(float deltaSeconds)
    {
        if (!float.IsFinite(deltaSeconds) || deltaSeconds < 0 || !float.IsFinite(Speed)) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        var previous = Position;
        var next = previous + (double)deltaSeconds * Speed;
        if (!double.IsFinite(next)) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        if (!Loop || Clip.Duration == 0) next = Math.Clamp(next, 0, Clip.Duration);
        _events.Clear(); DroppedEvents = 0;
        if (next != previous)
        {
            var candidates = new List<AnimationEventOccurrence3D>();
            foreach (var marker in _markers)
            {
                if (!Loop || Clip.Duration == 0)
                {
                    if (Crossed(previous, next, marker.Time)) candidates.Add(new(marker, marker.Time));
                    continue;
                }
                var duration = (double)Clip.Duration;
                var forward = next > previous;
                var first = forward ? Math.Floor((previous - marker.Time) / duration) + 1 : Math.Ceiling((next - marker.Time) / duration);
                var last = forward ? Math.Floor((next - marker.Time) / duration) : Math.Ceiling((previous - marker.Time) / duration) - 1;
                var total = Math.Max(0, last - first + 1);
                var emitted = (int)Math.Min(1024, total);
                var dropped = total - emitted;
                DroppedEvents = SaturatingAdd(DroppedEvents, dropped);
                for (var i = 0; i < emitted; i++)
                {
                    var cycle = forward ? first + i : last - i;
                    candidates.Add(new(marker, marker.Time + cycle * duration));
                }
            }
            candidates.Sort((a, b) => next > previous ? a.PlaybackTime.CompareTo(b.PlaybackTime) : b.PlaybackTime.CompareTo(a.PlaybackTime));
            for (var i = 0; i < Math.Min(1024, candidates.Count); i++) _events.Add(candidates[i]);
            DroppedEvents = SaturatingAdd(DroppedEvents, Math.Max(0, candidates.Count - 1024));
        }
        Position = next;
    }

    internal static float SampleTime(double position, float duration, bool loop)
        => duration <= 0 ? 0 : (float)(loop ? position - Math.Floor(position / duration) * duration : Math.Clamp(position, 0, duration));

    private static bool Crossed(double from, double to, double marker)
        => to > from ? marker > from && marker <= to : marker >= to && marker < from;
    private static long SaturatingAdd(long value, double amount)
        => amount >= long.MaxValue - value ? long.MaxValue : value + (long)amount;
}
