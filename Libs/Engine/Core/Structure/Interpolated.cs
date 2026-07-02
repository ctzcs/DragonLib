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
public struct Interpolated<T>(T start, T end, float currentTime,Transition transition, Func<T, T, float, T> lerp)
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

    public void SetValue(T newValue, float currentTime)
    {
        start = GetValue(currentTime);
        end = newValue;
        startTime = currentTime;
    }

    public void SetValue(T nowValue,T newValue, float currentTime,Transition trans, Func<T, T, float, T> l)
    {
        start = nowValue;
        end = newValue;
        startTime = currentTime;
        this.transition = trans;
        this.lerp = l;
    }

    public T GetValue(float currentTime)
    {
        float t = GetElapsedSeconds(currentTime) * speed;
        if (t <= 0f)
            return start;
        if (t >= 1.0f)
            return end;

        var ratio = GetRatioInternal(t, transition);
        return lerp(start, end, ratio); 
    }

    public void SetDuration(float duration)
    {
        speed = duration <= 0f ? float.PositiveInfinity : 1.0f / duration;
    }

    float GetElapsedSeconds(float currentTime) => currentTime - startTime;

    static float GetRatioInternal(float t, Transition transition)
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

            default:
                return t;
        }
    }
}

public enum Transition{
    None,
    Linear,
    EaseInOut,
    EaseOut,
    EaseInBack,
    EaseOutElastic,
}
