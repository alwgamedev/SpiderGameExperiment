using System;
using System.Runtime.InteropServices;
using UnityEngine;

[Serializable]
[StructLayout(LayoutKind.Sequential)]
public struct AnimationTimer<T, TUtility>
    where T : struct
    where TUtility : struct, IAnimationUtility<T>
{
    public T startVal;
    public T goalVal;
    public float speed;
    public float timer;

    public readonly T AnimatedValue => default(TUtility).AnimatedValue(in startVal, in goalVal, timer);

    public void SnapTo(T val)
    {
        startVal = val;
        goalVal = val;
        speed = 0;
        timer = 1;
    }

    public void BeginAnimation(T startVal, T goalVal, float speed)
    {
        this.startVal = startVal;
        this.goalVal = goalVal;
        this.speed = speed;
        timer = default(TUtility).InitialTimer(in startVal, in goalVal, speed);
    }

    public bool Update(float dt)
    {
        if (timer < 1)
        {
            timer += speed * dt;
            return true;
        }

        return false;
    }
}

public interface IAnimationUtility<T> where T : struct
{
    public T AnimatedValue(in T startVal, in T goalVal, float t);
    public float InitialTimer(in T startVal, in T goalVal, float speed);
}

public struct FloatAnimationUtility : IAnimationUtility<float>
{
    public readonly float AnimatedValue(in float startVal, in float goalVal, float t)
    {
        return Mathf.Lerp(startVal, goalVal, t);
    }

    public readonly float InitialTimer(in float startVal, in float goalVal, float speed)
    {
        return 1 - Mathf.Clamp(Mathf.Abs(goalVal - startVal) / speed, 0, 1);
    }
}

public struct Vector4AnimationUtility : IAnimationUtility<Vector4>
{
    public readonly Vector4 AnimatedValue(in Vector4 startVal, in Vector4 goalVal, float t)
    {
        return Vector4.Lerp(startVal, goalVal, t);
    }

    public readonly float InitialTimer(in Vector4 startVal, in Vector4 goalVal, float speed)
    {
        return 1 - Mathf.Clamp(MathTools.MaxDifference(startVal, goalVal) / speed, 0, 1);
    }
}