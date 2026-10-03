using System.Numerics;
using DCFApixels.DragonECS;
using Engine.Animation;
using Engine.Assets;
using Engine.Assets.Dasset;
using Foster.Framework;

// 动画播放的两层组件：AnimatorComp 是【配置层】（随 prefab/level 序列化；Time 是运行时状态，
// MVP 不拆播放状态组件），SkinPaletteComp 是【派生层】（IEcsDerivedComponent，序列化跳过）。
// 采样/插值/矩阵传播的纯逻辑在 Engine.Animation.SkeletonAnimator，本系统只是 ECS 薄壳。

namespace Engine.ECS;

/// <summary>
/// 动画播放状态（配置层）：引用 <see cref="MeshRendererComp.Model"/> 模型的剪辑表。
/// ClipIndex 越界或模型无剪辑时输出 bind pose。
/// </summary>
public struct AnimatorComp : IEcsComponent
{
    public int ClipIndex;
    public float Time;
    public float Speed = 1f;
    public bool Loop = true;
    public int NextClipIndex = -1;
    public float BlendDuration;
    public float BlendTime;
    public float NextTime;

    public void CrossFadeTo(int clipIndex, float duration)
    {
        NextClipIndex = clipIndex; BlendDuration = MathF.Max(0, duration);
        BlendTime = 0; NextTime = 0;
    }

    public AnimatorComp()
    {
    }

    public AnimatorComp(int clipIndex, float speed = 1f, bool loop = true)
    {
        ClipIndex = clipIndex;
        Speed = speed;
        Loop = loop;
    }
}

/// <summary>逐关节 palette 矩阵（派生层）：AnimationSystem 每帧重算，渲染时随 draw 传给蒙皮 shader。</summary>
public struct SkinPaletteComp : IEcsComponent, IEcsDerivedComponent
{
    public Matrix4x4[] Matrices = [];

    public SkinPaletteComp()
    {
    }
}

/// <summary>
/// 每帧推进 <see cref="AnimatorComp"/> 时间、采样剪辑、算出 joint palette 写入 <see cref="SkinPaletteComp"/>。
/// 需要 pipeline 注入 Foster <see cref="App"/>（取 Time.Delta；如 Program.cs 的 .Inject((App)this)）。
/// </summary>
public sealed class AnimationSystem : IUpdateSystem
{
    [DI] private EcsDefaultWorld _world = null!;
    [DI] private AssetDatabase _assets = null!;
    [DI] private App _app = null!;

    public void Update()
        => Run(_world, _assets, _app.Time.Delta);

    /// <summary>核心驱动逻辑独立成静态方法，供单元测试在无 pipeline 的情况下直接调用。</summary>
    public static void Run(EcsWorld world, AssetDatabase assets, float deltaTime)
    {
        var animatorPool = world.GetPool<AnimatorComp>();
        var rendererPool = world.GetPool<MeshRendererComp>();
        var palettePool = world.GetPool<SkinPaletteComp>();

        foreach (int e in world.Entities)
        {
            if (!animatorPool.Has(e) || !rendererPool.Has(e))
                continue;

            var model = assets.Get<DassetModelAsset>(rendererPool.Get(e).Model);
            if (model == null || model.Skeletons.Count == 0)
                continue;

            ref var animator = ref animatorPool.Get(e);
            var clip = animator.ClipIndex >= 0 && animator.ClipIndex < model.Clips.Count
                ? model.Clips[animator.ClipIndex]
                : null;

            var skeleton = clip != null && clip.SkinIndex >= 0 && clip.SkinIndex < model.Skeletons.Count
                ? model.Skeletons[clip.SkinIndex]
                : model.Skeletons[0];

            var jointCount = Math.Min(skeleton.Joints.Count, SkeletonAnimator.MaxJoints);
            var matrices = palettePool.Has(e) ? palettePool.Get(e).Matrices : [];
            if (matrices.Length != jointCount)
                matrices = new Matrix4x4[jointCount];
            var next = animator.NextClipIndex >= 0 && animator.NextClipIndex < model.Clips.Count
                ? model.Clips[animator.NextClipIndex] : null;
            // 两条剪辑必须属于同一骨架；配置错误时取消过渡，不能把另一套关节索引套过来。
            if (next != null && next.SkinIndex != (clip?.SkinIndex ?? 0)) next = null;
            if (next == null) animator.NextClipIndex = -1;
            Evaluate(skeleton, clip, next, ref animator, deltaTime, matrices);

            var palette = new SkinPaletteComp { Matrices = matrices };
            if (palettePool.Has(e))
                palettePool.Get(e) = palette;
            else
                palettePool.Add(e) = palette;
        }
    }

    /// <summary>推进双剪辑播放状态并输出 palette；纯数据入口便于测试过渡完成后的时间接续。</summary>
    public static void Evaluate(DassetSkeleton skeleton, DassetAnimationClip? clip, DassetAnimationClip? next,
        ref AnimatorComp animator, float deltaTime, Span<Matrix4x4> matrices)
    {
        animator.Time = SkeletonAnimator.AdvanceTime(animator.Time, deltaTime, animator.Speed, animator.Loop, clip?.Duration ?? 0);
        if (next == null)
        {
            SkeletonAnimator.ComputePalette(skeleton, clip, animator.Time, matrices);
            return;
        }
        animator.NextTime = SkeletonAnimator.AdvanceTime(animator.NextTime, deltaTime, animator.Speed, animator.Loop, next.Duration);
        animator.BlendTime += MathF.Max(0, deltaTime);
        var weight = animator.BlendDuration > 0 ? Math.Clamp(animator.BlendTime / animator.BlendDuration, 0, 1) : 1;
        var count = skeleton.Joints.Count;
        Span<JointPose> a = count <= SkeletonAnimator.MaxJoints ? stackalloc JointPose[count] : new JointPose[count];
        Span<JointPose> b = count <= SkeletonAnimator.MaxJoints ? stackalloc JointPose[count] : new JointPose[count];
        SkeletonAnimator.SamplePose(skeleton, clip, animator.Time, a);
        SkeletonAnimator.SamplePose(skeleton, next, animator.NextTime, b);
        SkeletonAnimator.BlendPoses(a, b, weight, a);
        SkeletonAnimator.ComputePalette(skeleton, a, matrices);
        if (weight >= 1)
        {
            animator.ClipIndex = animator.NextClipIndex; animator.Time = animator.NextTime;
            animator.NextClipIndex = -1; animator.BlendTime = 0; animator.NextTime = 0;
        }
    }
}
