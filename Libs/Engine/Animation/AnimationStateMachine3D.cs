using System.Numerics;
using Engine.Assets.Dasset;

namespace Engine.Animation;

public sealed record AnimationState3D(DassetAnimationClip Clip, bool Loop = true, float Speed = 1,
    IReadOnlyList<AnimationEvent3D>? Markers = null);

/// <summary>独立骨架状态机；只生成 pose/palette/事件/根运动，世界变换和绘制由调用者处理。</summary>
public sealed class AnimationStateMachine3D
{
    private sealed record Transition(string From, string To, Func<bool> Condition, float FadeSeconds);
    private readonly Dictionary<string, AnimationState3D> _states = new(StringComparer.Ordinal);
    private readonly List<Transition> _transitions = [];
    private readonly JointPose[] _pose;
    private readonly JointPose[] _sourcePose;
    private readonly JointPose[] _targetPose;
    private readonly Matrix4x4[] _palette;
    private AnimationPlayback3D? _current;
    private AnimationPlayback3D? _source;
    private float _fadeSeconds;
    private float _fadeTime;
    private bool _fading;

    public DassetSkeleton Skeleton { get; }
    public string? CurrentState { get; private set; }
    public float Time => _current?.Time ?? 0;
    public long DroppedEvents => _current?.DroppedEvents ?? 0;
    public float PlaybackSpeed { get => _current?.Speed ?? 1; set { if (_current != null) _current.Speed = value; } }
    public Span<JointPose> Pose => _pose;
    public ReadOnlySpan<Matrix4x4> Palette => _palette;
    public IReadOnlyList<AnimationEventOccurrence3D> Events => _current?.Events ?? Array.Empty<AnimationEventOccurrence3D>();
    public int RootMotionJoint { get; set; } = -1;
    /// <summary>仅来自目标状态；过渡期间不混合两条轨迹，避免两次应用运动。</summary>
    public Matrix4x4 DeltaRootMotion { get; private set; } = Matrix4x4.Identity;

    public AnimationStateMachine3D(DassetSkeleton skeleton)
    {
        ArgumentNullException.ThrowIfNull(skeleton);
        Skeleton = skeleton;
        _pose = new JointPose[skeleton.Joints.Count];
        _sourcePose = new JointPose[_pose.Length]; _targetPose = new JointPose[_pose.Length];
        _palette = new Matrix4x4[Math.Min(_pose.Length, SkeletonAnimator.MaxJoints)];
        SkeletonAnimator.SamplePose(skeleton, null, 0, _pose);
        RebuildPalette();
    }

    public void AddState(string name, AnimationState3D state)
    {
        ArgumentException.ThrowIfNullOrEmpty(name); ArgumentNullException.ThrowIfNull(state);
        if (!float.IsFinite(state.Speed)) throw new ArgumentOutOfRangeException(nameof(state));
        // Validate duration/events when registering, before any transition can alter playback.
        _ = new AnimationPlayback3D(state.Clip, state.Loop, state.Markers);
        _states.Add(name, state);
    }

    /// <summary>按注册顺序检查，仅当前状态的第一个满足条件的过渡生效；每次 Update 至多一次。</summary>
    public void AddTransition(string from, string to, Func<bool> condition, float fadeSeconds = .2f)
    {
        if (!_states.ContainsKey(from) || !_states.ContainsKey(to)) throw new ArgumentException("Register both states before a transition.");
        ArgumentNullException.ThrowIfNull(condition); ValidateFade(fadeSeconds);
        _transitions.Add(new(from, to, condition, fadeSeconds));
    }

    /// <summary>显式切换/重播。普通过渡两条剪辑同时推进；打断过渡时从当前混合姿态开始，防止跳变。</summary>
    public void Play(string name, float fadeSeconds = 0)
    {
        ValidateFade(fadeSeconds);
        if (!_states.TryGetValue(name, out var state)) throw new ArgumentException("Unknown animation state.", nameof(name));
        var playback = new AnimationPlayback3D(state.Clip, state.Loop, state.Markers) { Speed = state.Speed };
        if (_current != null && fadeSeconds > 0)
        {
            _source = _fading ? null : _current;
            _pose.CopyTo(_sourcePose, 0);
            _fadeSeconds = fadeSeconds; _fadeTime = 0; _fading = true;
        }
        else { _source = null; _fading = false; }
        _current = playback; CurrentState = name; DeltaRootMotion = Matrix4x4.Identity;
        Sample();
    }

    public void Update(float deltaSeconds)
    {
        if (!float.IsFinite(deltaSeconds) || deltaSeconds < 0) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        foreach (var transition in _transitions)
            if (transition.From == CurrentState && transition.Condition()) { Play(transition.To, transition.FadeSeconds); break; }
        DeltaRootMotion = Matrix4x4.Identity;
        if (_current == null) return;
        var previous = _current.Position;
        _current.Advance(deltaSeconds);
        if (RootMotionJoint >= 0)
            DeltaRootMotion = RootMotion3D.Extract(Skeleton, _current.Clip, RootMotionJoint, previous, _current.Position, _current.Loop);
        if (_fading)
        {
            _source?.Advance(deltaSeconds);
            _fadeTime = Math.Min(_fadeSeconds, _fadeTime + deltaSeconds);
        }
        Sample();
    }

    /// <summary>定位当前剪辑，清除过渡及本次事件/根运动，不把跳转当作角色移动。</summary>
    public void Seek(double position)
    {
        _current?.Seek(position); _source = null; _fading = false;
        DeltaRootMotion = Matrix4x4.Identity; Sample();
    }

    /// <summary>调用 IK 或直接修改 Pose 后更新 GPU palette。</summary>
    public void RebuildPalette() => SkeletonAnimator.ComputePalette(Skeleton, _pose, _palette);

    private void Sample()
    {
        if (_current == null) return;
        SkeletonAnimator.SamplePose(Skeleton, _current.Clip, _current.Time, _targetPose);
        if (RootMotionJoint >= 0) RootMotion3D.RemoveFromPose(Skeleton, RootMotionJoint, _targetPose);
        if (_fading)
        {
            if (_source != null)
            {
                SkeletonAnimator.SamplePose(Skeleton, _source.Clip, _source.Time, _sourcePose);
                if (RootMotionJoint >= 0) RootMotion3D.RemoveFromPose(Skeleton, RootMotionJoint, _sourcePose);
            }
            SkeletonAnimator.BlendPoses(_sourcePose, _targetPose, _fadeTime / _fadeSeconds, _pose);
            if (_fadeTime >= _fadeSeconds) { _source = null; _fading = false; }
        }
        else _targetPose.CopyTo(_pose, 0);
        RebuildPalette();
    }

    private static void ValidateFade(float seconds)
    {
        if (!float.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
    }
}
