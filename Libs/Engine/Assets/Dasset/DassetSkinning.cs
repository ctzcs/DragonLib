using System.Numerics;

namespace Engine.Assets.Dasset;

/// <summary>动画 channel 的目标分量（与 glTF PropertyPath 对齐）。</summary>
public enum DassetAnimPath
{
    Translation = 0,
    Rotation = 1,
    Scale = 2,
}

/// <summary>骨架中的一个关节。joints 按拓扑序存储（父先于子），传播时一次线性扫描即可。</summary>
public struct DassetJoint
{
    public string Name = string.Empty;

    /// <summary>父关节在 joints 里的下标；-1 表示根（其本地 TRS 相对骨架挂点）。</summary>
    public int ParentIndex = -1;

    /// <summary>bind pose 的本地 TRS（动画 channel 未覆盖时用的静止值）。</summary>
    public Vector3 BindTranslation;
    public Quaternion BindRotation = Quaternion.Identity;
    public Vector3 BindScale = Vector3.One;

    /// <summary>inverse bind matrix：mesh bind 空间 → 关节 bind 局部空间。</summary>
    public Matrix4x4 InverseBindMatrix = Matrix4x4.Identity;

    public DassetJoint()
    {
    }
}

/// <summary>
/// 骨架（蒙皮 skin）：拓扑序的关节列表。运行时姿态 = 采样剪辑改本地 TRS →
/// 沿树传播全局矩阵 → palette[i] = InverseBindMatrix[i] × Global[i]（行向量约定）。
/// </summary>
public sealed class DassetSkeleton
{
    public List<DassetJoint> Joints = [];
}

public enum DassetInterpolation { Linear, Step, CubicSpline }

/// <summary>关键帧值与 cubic 入/出切线分别存储；Rotation 是 xyzw，其余用 xyz。</summary>
public sealed class DassetAnimationChannel
{
    public int JointIndex;
    public DassetAnimPath Path;
    public float[] Times = [];
    public Vector4[] Values = [];
    public DassetInterpolation Interpolation;
    public Vector4[] InTangents = [];
    public Vector4[] OutTangents = [];
}

/// <summary>一条动画剪辑。MVP 只收目标是关节的 channel（其余 cook 时警告跳过）。</summary>
public sealed class DassetAnimationClip
{
    public string Name = string.Empty;

    /// <summary>剪辑作用的骨架在模型 Skeletons 里的下标。</summary>
    public int SkinIndex;
    public float Duration;
    public List<DassetAnimationChannel> Channels = [];
}
