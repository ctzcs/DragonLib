using System.Numerics;
using Engine.Assets.Dasset;

namespace Engine.Animation;

/// <summary>基础 CCD IK，target 位于骨架模型空间，修改本地 rotation；不改变 translation/scale。</summary>
public static class CcdIk3D
{
    public static float Solve(DassetSkeleton skeleton, Span<JointPose> pose, int endJoint, Vector3 target,
        int chainLength = 3, int iterations = 12, float tolerance = .001f, float weight = 1)
    {
        ArgumentNullException.ThrowIfNull(skeleton);
        if (pose.Length < skeleton.Joints.Count || endJoint < 0 || endJoint >= skeleton.Joints.Count)
            throw new ArgumentOutOfRangeException(nameof(endJoint));
        if (chainLength < 1 || iterations < 1 || !float.IsFinite(tolerance) || tolerance < 0 || !float.IsFinite(weight) ||
            !float.IsFinite(target.X) || !float.IsFinite(target.Y) || !float.IsFinite(target.Z)) throw new ArgumentOutOfRangeException(nameof(chainLength));
        weight = Math.Clamp(weight, 0, 1);
        var count = skeleton.Joints.Count;
        Span<Matrix4x4> globals = count <= 128 ? stackalloc Matrix4x4[count] : new Matrix4x4[count];
        Span<JointPose> original = count <= 128 ? stackalloc JointPose[count] : new JointPose[count];
        pose[..count].CopyTo(original);
        for (var i = 0; i < count; i++)
        {
            var scale = pose[i].Scale;
            if (!float.IsFinite(scale.X) || scale.X <= 0 || Math.Abs(scale.X - scale.Y) > 1e-5f || Math.Abs(scale.X - scale.Z) > 1e-5f)
                throw new ArgumentException("CCD requires finite positive uniform joint scales.", nameof(pose));
        }
        SkeletonAnimator.ComputeGlobals(skeleton, pose, globals);
        // 完全反向的直链若先转末端会折叠到根上，CCD 无法继续；先转所选链根打破这个退化情况。
        var chainRoot = skeleton.Joints[endJoint].ParentIndex;
        for (var link = 1; chainRoot >= 0 && link < chainLength && skeleton.Joints[chainRoot].ParentIndex >= 0; link++)
            chainRoot = skeleton.Joints[chainRoot].ParentIndex;
        if (chainRoot >= 0 && Matrix4x4.Invert(globals[chainRoot], out var rootInverse))
        {
            var from = Vector3.Transform(globals[endJoint].Translation, rootInverse);
            var to = Vector3.Transform(target, rootInverse);
            if (from.LengthSquared() > 1e-10f && to.LengthSquared() > 1e-10f &&
                Vector3.Dot(Vector3.Normalize(from), Vector3.Normalize(to)) < -.99999f)
            {
                var axis = Vector3.Cross(Vector3.Normalize(from), Math.Abs(Vector3.Normalize(from).X) < .9f ? Vector3.UnitX : Vector3.UnitY);
                var delta = Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), MathF.PI);
                pose[chainRoot].Rotation = Quaternion.Normalize(Quaternion.Concatenate(delta, pose[chainRoot].Rotation));
                SkeletonAnimator.ComputeGlobals(skeleton, pose, globals);
            }
        }
        for (var iteration = 0; iteration < iterations && Vector3.Distance(globals[endJoint].Translation, target) > tolerance; iteration++)
        {
            var joint = skeleton.Joints[endJoint].ParentIndex;
            for (var link = 0; joint >= 0 && link < chainLength; link++, joint = skeleton.Joints[joint].ParentIndex)
            {
                if (!Matrix4x4.Invert(globals[joint], out var inverse)) continue;
                var from = Vector3.Transform(globals[endJoint].Translation, inverse);
                var to = Vector3.Transform(target, inverse);
                if (from.LengthSquared() < 1e-10f || to.LengthSquared() < 1e-10f) continue;
                from = Vector3.Normalize(from); to = Vector3.Normalize(to);
                var dot = Math.Clamp(Vector3.Dot(from, to), -1, 1);
                if (dot > .999999f) continue;
                var axis = Vector3.Cross(from, to);
                if (axis.LengthSquared() < 1e-10f)
                    axis = Vector3.Cross(from, Math.Abs(from.X) < .9f ? Vector3.UnitX : Vector3.UnitY);
                var delta = Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), MathF.Acos(dot));
                pose[joint].Rotation = Quaternion.Normalize(Quaternion.Concatenate(delta, pose[joint].Rotation));
                SkeletonAnimator.ComputeGlobals(skeleton, pose, globals);
            }
        }
        if (weight < 1)
            for (var i = 0; i < count; i++) pose[i].Rotation = Quaternion.Normalize(Quaternion.Slerp(original[i].Rotation, pose[i].Rotation, weight));
        SkeletonAnimator.ComputeGlobals(skeleton, pose, globals);
        return Vector3.Distance(globals[endJoint].Translation, target);
    }
}
