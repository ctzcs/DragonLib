using System.Numerics;
using Engine.Assets.Dasset;

namespace Engine.Animation;

/// <summary>根关节刚体运动提取；返回行向量 delta，应用方式 world = delta * world。</summary>
public static class RootMotion3D
{
    public static Matrix4x4 Extract(DassetSkeleton skeleton, DassetAnimationClip clip, int rootJoint,
        double previousTime, double currentTime, bool loop = true)
    {
        ValidateRoot(skeleton, rootJoint);
        if (!double.IsFinite(previousTime) || !double.IsFinite(currentTime)) throw new ArgumentOutOfRangeException(nameof(currentTime));
        var previous = Unwrapped(skeleton, clip, rootJoint, previousTime, loop);
        var current = Unwrapped(skeleton, clip, rootJoint, currentTime, loop);
        if (!Matrix4x4.Invert(previous, out var inverse)) throw new InvalidOperationException("Root transform is singular.");
        return current * inverse;
    }

    /// <summary>恢复根的 bind translation/rotation，保留 scale，防止运动既应用 world 又留在蒙皮里。</summary>
    public static void RemoveFromPose(DassetSkeleton skeleton, int rootJoint, Span<JointPose> pose)
    {
        ValidateRoot(skeleton, rootJoint);
        if (pose.Length < skeleton.Joints.Count) throw new ArgumentOutOfRangeException(nameof(pose));
        pose[rootJoint].Translation = skeleton.Joints[rootJoint].BindTranslation;
        pose[rootJoint].Rotation = skeleton.Joints[rootJoint].BindRotation;
    }

    private static Matrix4x4 Unwrapped(DassetSkeleton skeleton, DassetAnimationClip clip, int root, double time, bool loop)
    {
        if (!float.IsFinite(clip.Duration) || clip.Duration < 0) throw new ArgumentException("Invalid clip duration.", nameof(clip));
        var sample = Sample(skeleton, clip, root, AnimationPlayback3D.SampleTime(time, clip.Duration, loop));
        if (!loop || clip.Duration <= 0) return sample;
        var cycles = Math.Floor(time / clip.Duration);
        if (cycles < int.MinValue || cycles > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(time), "Root motion supports up to 2 billion loops.");
        var start = Sample(skeleton, clip, root, 0);
        var end = Sample(skeleton, clip, root, clip.Duration);
        Matrix4x4.Invert(start, out var inverseStart);
        return sample * inverseStart * Power(end * inverseStart, (long)cycles) * start;
    }

    private static Matrix4x4 Sample(DassetSkeleton skeleton, DassetAnimationClip clip, int root, float time)
    {
        var joint = skeleton.Joints[root];
        var translation = joint.BindTranslation; var rotation = joint.BindRotation;
        foreach (var channel in clip.Channels)
        {
            if (channel.JointIndex != root || channel.Path == DassetAnimPath.Scale) continue;
            var value = SkeletonAnimator.SampleChannel(channel, time);
            if (channel.Path == DassetAnimPath.Translation) translation = new(value.X, value.Y, value.Z);
            else rotation = new(value.X, value.Y, value.Z, value.W);
        }
        rotation = rotation.LengthSquared() > 1e-12f ? Quaternion.Normalize(rotation) : Quaternion.Identity;
        return Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(translation);
    }

    private static Matrix4x4 Power(Matrix4x4 value, long count)
    {
        if (count < 0) { Matrix4x4.Invert(value, out value); count = -count; }
        var result = Matrix4x4.Identity;
        while (count > 0)
        {
            if ((count & 1) != 0) result *= value;
            value *= value; count >>= 1;
        }
        return result;
    }

    private static void ValidateRoot(DassetSkeleton skeleton, int root)
    {
        ArgumentNullException.ThrowIfNull(skeleton);
        if (root < 0 || root >= skeleton.Joints.Count || skeleton.Joints[root].ParentIndex >= 0)
            throw new ArgumentOutOfRangeException(nameof(root), "Root motion requires a top-level joint.");
    }
}
