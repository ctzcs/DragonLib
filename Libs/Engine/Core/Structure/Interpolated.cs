using System;

namespace Engine;
/// <summary>
/// 插值方法
/// </summary>
/// <param name="start"></param>
/// <param name="end"></param>
/// <param name="currentTime"></param>
/// <param name="transition"></param>
/// <param name="lerp"> start + (end-start) * ratio 用来抵消这里的泛型不支持，填入Lerp函数 </param>
/// <typeparam name="T"></typeparam>
public struct Interpolated<T>(T start, T end, float currentTime,Transition transition, Func<T, T, float, T> lerp, Func<float, float> customEase = null)
{
    //开始值
    T start = start;
    //结束值
    T end = end;
    //开始时间
    float startTime = currentTime;
    //动画速度
    float speed = 1.0f;
    
    Transition transition = transition;

    Func<T, T, float, T> lerp = lerp;

    Func<float, float> customEase = customEase;

    public void SetValue(T newValue, float currentTime)
    {
        start = GetValue(currentTime);
        end = newValue;
        startTime = currentTime;
    }

    public void SetValue(T nowValue,T newValue, float currentTime,Transition trans, Func<T, T, float, T> l, Func<float, float> customEase = null)
    {
        start = nowValue;
        end = newValue;
        startTime = currentTime;
        this.transition = trans;
        this.lerp = l;
        this.customEase = customEase;
    }

    public T GetValue(float currentTime)
    {
        float t = GetElapsedSeconds(currentTime) * speed;
        if (t <= 0f)
            return start;
        if (t >= 1.0f)
            return end;

        var ratio = GetRatioInternal(t, transition, customEase);
        return lerp(start, end, ratio);
    }

    public void SetDuration(float duration)
    {
        speed = duration <= 0f ? float.PositiveInfinity : 1.0f / duration;
    }

    float GetElapsedSeconds(float currentTime) => currentTime - startTime;

    static float GetRatioInternal(float t, Transition transition, Func<float, float> customEase = null)
    {
        t = Math.Clamp(t, 0f, 1f);
        switch (transition)
        {
            case Transition.None:
                return 1f;

            case Transition.Linear:
                return t;

            case Transition.EaseInOut:
                if (t < 0.5f)
                    return 4f * t * t * t;
                return 1f - MathF.Pow(-2f * t + 2f, 3f) / 2f;

            case Transition.EaseOut:
                return 1f - MathF.Pow(1f - t, 3f);

            case Transition.EaseInBack:
                const float c1 = 1.70158f;
                const float c3 = c1 + 1f;
                return c3 * t * t * t - c1 * t * t;

            case Transition.EaseOutElastic:
                if (t <= 0f)
                    return 0f;
                if (t >= 1f)
                    return 1f;
                var c4 = (2f * MathF.PI) / 3f;
                return MathF.Pow(2f, -10f * t) * MathF.Sin((t * 10f - 0.75f) * c4) + 1f;

            case Transition.EaseIn:
                return t * t * t;

            case Transition.EaseOutBack:
                const float c5 = 1.70158f;
                const float c6 = c5 + 1f;
                return 1f + c6 * MathF.Pow(t - 1f, 3f) + c5 * MathF.Pow(t - 1f, 2f);

            case Transition.EaseInOutBack:
                const float c7 = 1.70158f * 1.525f;
                return t < 0.5f
                    ? MathF.Pow(2f * t, 2f) * ((c7 + 1f) * 2f * t - c7) / 2f
                    : (MathF.Pow(2f * t - 2f, 2f) * ((c7 + 1f) * (2f * t - 2f) + c7) + 2f) / 2f;

            case Transition.EaseInElastic:
                if (t <= 0f)
                    return 0f;
                if (t >= 1f)
                    return 1f;
                var c8 = (2f * MathF.PI) / 3f;
                return -MathF.Pow(2f, 10f * t - 10f) * MathF.Sin((t * 10f - 10.75f) * c8);

            case Transition.EaseInOutElastic:
                if (t <= 0f)
                    return 0f;
                if (t >= 1f)
                    return 1f;
                var c9 = (2f * MathF.PI) / 4.5f;
                return t < 0.5f
                    ? -(MathF.Pow(2f, 20f * t - 10f) * MathF.Sin((20f * t - 11.125f) * c9)) / 2f
                    : MathF.Pow(2f, -20f * t + 10f) * MathF.Sin((20f * t - 11.125f) * c9) / 2f + 1f;

            case Transition.EaseOutBounce:
            {
                const float n1 = 7.5625f;
                const float d1 = 2.75f;
                if (t < 1f / d1)
                    return n1 * t * t;
                if (t < 2f / d1)
                    return n1 * (t -= 1.5f / d1) * t + 0.75f;
                if (t < 2.5f / d1)
                    return n1 * (t -= 2.25f / d1) * t + 0.9375f;
                return n1 * (t -= 2.625f / d1) * t + 0.984375f;
            }

            case Transition.EaseInBounce:
            {
                const float n1 = 7.5625f;
                const float d1 = 2.75f;
                t = 1f - t;
                float bt;
                if (t < 1f / d1)
                    bt = n1 * t * t;
                else if (t < 2f / d1)
                    bt = n1 * (t -= 1.5f / d1) * t + 0.75f;
                else if (t < 2.5f / d1)
                    bt = n1 * (t -= 2.25f / d1) * t + 0.9375f;
                else
                    bt = n1 * (t -= 2.625f / d1) * t + 0.984375f;
                return 1f - bt;
            }

            case Transition.EaseInQuad:
                return t * t;

            case Transition.EaseOutQuad:
                return 1f - (1f - t) * (1f - t);

            case Transition.EaseInOutQuad:
                return t < 0.5f ? 2f * t * t : 1f - MathF.Pow(-2f * t + 2f, 2f) / 2f;

            case Transition.EaseInQuart:
                return t * t * t * t;

            case Transition.EaseOutQuart:
                return 1f - MathF.Pow(1f - t, 4f);

            case Transition.EaseInOutQuart:
                return t < 0.5f ? 8f * t * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 4f) / 2f;

            case Transition.EaseInCirc:
                return 1f - MathF.Sqrt(1f - t * t);

            case Transition.EaseOutCirc:
                return MathF.Sqrt(1f - MathF.Pow(t - 1f, 2f));

            case Transition.EaseInOutCirc:
                return t < 0.5f
                    ? (1f - MathF.Sqrt(1f - 4f * t * t)) / 2f
                    : (MathF.Sqrt(1f - MathF.Pow(-2f * t + 2f, 2f)) + 1f) / 2f;

            case Transition.Custom:
                return customEase != null ? Math.Clamp(customEase(t), 0f, 1f) : t;

            default:
                return t;
        }
    }
}

public enum Transition
{
    None,
    Linear,
    EaseIn,
    EaseOut,
    EaseInOut,
    EaseInQuad,
    EaseOutQuad,
    EaseInOutQuad,
    EaseInQuart,
    EaseOutQuart,
    EaseInOutQuart,
    EaseInBack,
    EaseOutBack,
    EaseInOutBack,
    EaseInElastic,
    EaseOutElastic,
    EaseInOutElastic,
    EaseInBounce,
    EaseOutBounce,
    EaseInCirc,
    EaseOutCirc,
    EaseInOutCirc,
    Custom,
}
