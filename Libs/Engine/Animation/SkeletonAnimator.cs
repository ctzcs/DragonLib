using System.Numerics;
using Engine.Assets.Dasset;

namespace Engine.Animation;

/// <summary>一个关节某一帧的本地姿态（TRS）。</summary>
public struct JointPose
{
    public Vector3 Translation;
    public Quaternion Rotation;
    public Vector3 Scale;
}

/// <summary>
/// 蒙皮动画采样与 joint palette 计算（纯静态方法，无 GPU/ECS 依赖，供单测）。
/// 矩阵为行向量约定（v * M，与 Transform3DSystem/Renderer3D 一致）：
/// 本地姿态 → 沿拓扑序传播全局矩阵（global = local * parentGlobal）→
/// palette[i] = InverseBindMatrix[i] * global[i]。实体世界矩阵提供骨架挂点以上的变换，
/// bind pose 下渲染结果与 glTF 场景摆放一致。
///
/// 多剪辑状态机、morph target 和根运动由游戏层另行实现。
/// </summary>
public static class SkeletonAnimator
{
    /// <summary>shader 侧 joint palette cbuffer 的容量上限（Standard3DSkinned 的 MAX_JOINTS）。</summary>
    public const int MaxJoints = 128;

    /// <summary>
    /// 采样剪辑得到逐关节本地姿态：先填 bind pose，再按 channel 覆盖（越界 clamp 到端点）。
    /// 循环回绕由调用侧处理（time 先对 Duration 取模）。clip 为 null 时输出纯 bind pose。
    /// </summary>
    public static void SamplePose(DassetSkeleton skeleton, DassetAnimationClip? clip, float time, Span<JointPose> pose)
    {
        if (pose.Length < skeleton.Joints.Count)
            throw new ArgumentOutOfRangeException(nameof(pose));

        for (var i = 0; i < skeleton.Joints.Count; i++)
        {
            var joint = skeleton.Joints[i];
            pose[i] = new JointPose
            {
                Translation = joint.BindTranslation,
                Rotation = joint.BindRotation,
                Scale = joint.BindScale,
            };
        }

        if (clip == null)
            return;

        foreach (var channel in clip.Channels)
        {
            if (channel.JointIndex < 0 || channel.JointIndex >= skeleton.Joints.Count)
                continue;
            var value = SampleChannel(channel, time);
            var jointPose = pose[channel.JointIndex];
            switch (channel.Path)
            {
                case DassetAnimPath.Translation:
                    jointPose.Translation = new Vector3(value.X, value.Y, value.Z);
                    break;
                case DassetAnimPath.Rotation:
                    jointPose.Rotation = new Quaternion(value.X, value.Y, value.Z, value.W);
                    break;
                case DassetAnimPath.Scale:
                    jointPose.Scale = new Vector3(value.X, value.Y, value.Z);
                    break;
            }
            pose[channel.JointIndex] = jointPose;
        }
    }

    /// <summary>
    /// 由本地姿态算 joint palette：joints 已按拓扑序（父先于子），一次线性扫描传播。
    /// palette 长度不足关节数时只写前 palette.Length 个（调用侧按 shader 上限截断）。
    /// </summary>
    public static void ComputePalette(DassetSkeleton skeleton, ReadOnlySpan<JointPose> pose, Span<Matrix4x4> palette)
    {
        var count = Math.Min(skeleton.Joints.Count, palette.Length);
        Span<Matrix4x4> globals = skeleton.Joints.Count <= 128
            ? stackalloc Matrix4x4[skeleton.Joints.Count]
            : new Matrix4x4[skeleton.Joints.Count];

        for (var i = 0; i < count; i++)
        {
            var joint = skeleton.Joints[i];
            var local = Matrix4x4.CreateScale(pose[i].Scale)
                * Matrix4x4.CreateFromQuaternion(pose[i].Rotation)
                * Matrix4x4.CreateTranslation(pose[i].Translation);
            globals[i] = joint.ParentIndex >= 0 && joint.ParentIndex < i
                ? local * globals[joint.ParentIndex]
                : local;
            palette[i] = joint.InverseBindMatrix * globals[i];
        }
    }

    /// <summary>一步到位：采样 + 传播 + palette。返回实际写入的 palette 矩阵数。</summary>
    public static int ComputePalette(DassetSkeleton skeleton, DassetAnimationClip? clip, float time, Span<Matrix4x4> palette)
    {
        Span<JointPose> pose = skeleton.Joints.Count <= 128
            ? stackalloc JointPose[skeleton.Joints.Count]
            : new JointPose[skeleton.Joints.Count];
        SamplePose(skeleton, clip, time, pose);
        ComputePalette(skeleton, pose, palette);
        return Math.Min(skeleton.Joints.Count, palette.Length);
    }

    /// <summary>推进播放时间：循环回绕（负速度也落回 [0, duration)），非循环钳到 [0, duration] 停住。</summary>
    public static float AdvanceTime(float time, float delta, float speed, bool loop, float duration)
    {
        time += delta * speed;
        if (duration <= 1e-6f)
            return time;
        return loop
            ? time - MathF.Floor(time / duration) * duration
            : Math.Clamp(time, 0f, duration);
    }

    /// <summary>保守包围当前蒙皮姿态：非负归一化混权是各关节变换点的凸组合，落在联合 AABB 内。</summary>
    public static DassetBounds ComputeSkinnedBounds(in DassetBounds bindBounds, ReadOnlySpan<Matrix4x4> palette)
    {
        if (palette.IsEmpty) return bindBounds;
        var bounds = DassetBounds.Empty;
        foreach (var matrix in palette) bounds.Encapsulate(bindBounds.Transformed(matrix));
        return bounds;
    }

    public static void BlendPoses(ReadOnlySpan<JointPose> a, ReadOnlySpan<JointPose> b, float t, Span<JointPose> result)
    {
        if (a.Length != b.Length || result.Length < a.Length) throw new ArgumentException("Pose sizes must match.");
        t = Math.Clamp(t, 0, 1);
        for (var i = 0; i < a.Length; i++)
            result[i] = new JointPose
            {
                Translation = Vector3.Lerp(a[i].Translation, b[i].Translation, t),
                Scale = Vector3.Lerp(a[i].Scale, b[i].Scale, t),
                Rotation = ShortestSlerp(a[i].Rotation, b[i].Rotation, t),
            };
    }

    /// <summary>STEP 保持前键；cubic tangent 按区间秒数缩放，四元数按 glTF 要求插值后归一化。</summary>
    public static Vector4 SampleChannel(DassetAnimationChannel channel, float time)
    {
        var times = channel.Times; var values = channel.Values;
        if (times.Length == 0) return channel.Path == DassetAnimPath.Rotation ? Vector4.UnitW : Vector4.Zero;
        Vector4 Finish(Vector4 value)
        {
            if (channel.Path != DassetAnimPath.Rotation) return value;
            var q = Normalize(ToQuaternion(value)); return new(q.X, q.Y, q.Z, q.W);
        }
        if (times.Length == 1 || time <= times[0]) return Finish(values[0]);
        if (time >= times[^1]) return Finish(values[^1]);
        var segment = 0;
        while (segment + 1 < times.Length && times[segment + 1] <= time) segment++;
        var dt = times[segment + 1] - times[segment];
        var t = dt > 0 ? (time - times[segment]) / dt : 0;
        if (channel.Interpolation == DassetInterpolation.Step) return Finish(values[segment]);
        if (channel.Interpolation == DassetInterpolation.CubicSpline)
        {
            var t2 = t * t; var t3 = t2 * t;
            return Finish((2 * t3 - 3 * t2 + 1) * values[segment]
                + (t3 - 2 * t2 + t) * dt * channel.OutTangents[segment]
                + (-2 * t3 + 3 * t2) * values[segment + 1]
                + (t3 - t2) * dt * channel.InTangents[segment + 1]);
        }
        if (channel.Path == DassetAnimPath.Rotation)
        {
            var q = ShortestSlerp(ToQuaternion(values[segment]), ToQuaternion(values[segment + 1]), t);
            return new(q.X, q.Y, q.Z, q.W);
        }
        return Vector4.Lerp(values[segment], values[segment + 1], t);
    }

    private static Quaternion Normalize(Quaternion value) => value.LengthSquared() > 1e-12f ? Quaternion.Normalize(value) : Quaternion.Identity;
    private static Quaternion ShortestSlerp(Quaternion a, Quaternion b, float t)
    {
        a = Normalize(a); b = Normalize(b);
        if (Quaternion.Dot(a, b) < 0) b = new(-b.X, -b.Y, -b.Z, -b.W);
        return Normalize(Quaternion.Slerp(a, b, t));
    }
    private static Quaternion ToQuaternion(Vector4 value) => new(value.X, value.Y, value.Z, value.W);
}
