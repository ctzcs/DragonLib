using System;
using System.Collections.Generic;

namespace Engine.ECS;

/// <summary>
/// 轻量场景/屏幕路由：只负责记录“当前是哪个屏幕”、切换、以及切换过程的计时进度，
/// 不携带任何具体屏幕的业务逻辑，也不知道过渡该画成什么样（淡入淡出/滑动等由调用方决定）。
/// 具体有哪些屏幕由各游戏自己定义一个 enum 作为 TScreen 传入。
/// </summary>
public class SceneRouter<TScreen> where TScreen : struct, Enum
{
    public TScreen Current { get; private set; }
    public TScreen Previous { get; private set; }

    /// <summary>是否处于过渡动画中。</summary>
    public bool IsTransitioning { get; private set; }

    /// <summary>过渡进度 0..1；非过渡状态下恒为 1。</summary>
    public float Progress { get; private set; } = 1f;

    private float _duration;
    private float _elapsed;

    public SceneRouter(TScreen initial)
    {
        Current = initial;
        Previous = initial;
    }

    /// <summary>
    /// 切换到目标屏幕。duration &gt; 0 时进入过渡期：Current 立即更新，
    /// 但 Progress 从 0 开始随 Update(dt) 推进到 1，期间 Previous 保留切换前的屏幕，
    /// 供渲染代码同时绘制新旧两屏做过渡效果。
    /// </summary>
    public void SwitchTo(TScreen screen, float duration = 0f)
    {
        if (EqualityComparer<TScreen>.Default.Equals(Current, screen)) return;
        Previous = Current;
        Current = screen;

        if (duration > 0f)
        {
            _duration = duration;
            _elapsed = 0f;
            Progress = 0f;
            IsTransitioning = true;
        }
        else
        {
            IsTransitioning = false;
            Progress = 1f;
        }
    }

    /// <summary>推进过渡计时，非过渡状态下调用无副作用。</summary>
    public void Update(float dt)
    {
        if (!IsTransitioning) return;
        _elapsed += dt;
        Progress = _duration <= 0f ? 1f : Math.Clamp(_elapsed / _duration, 0f, 1f);
        if (Progress >= 1f) IsTransitioning = false;
    }
}
