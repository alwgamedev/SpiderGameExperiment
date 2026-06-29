using System;
using UnityEngine;
using Unity.U2D.Physics;
using Unity.Mathematics;

[Serializable]
public struct SlugPose
{
    public Vector2 p0;
    public Vector2 v0;
    public Vector2 p1;
    public Vector2 v1;
    public Vector2 p2;
    public Vector2 v2;

    public readonly Vector4 Ctrl0 => new(p0.x, p0.y, v0.x, v0.y);
    public readonly Vector4 Ctrl1 => new(p1.x, p1.y, v1.x, v1.y);
    public readonly Vector4 Ctrl2 => new(p2.x, p2.y, v2.x, v2.y);

    public void Transform(Matrix4x4 transform)
    {
        p0 = transform.MultiplyPoint3x4(p0);
        p1 = transform.MultiplyPoint3x4(p1);
        p2 = transform.MultiplyPoint3x4(p2);
        v0 = transform.MultiplyVector(v0);
        v1 = transform.MultiplyVector(v1);
        v2 = transform.MultiplyVector(v2);
    }

    public void Aim(PhysicsRotate aim)
    {
        var d0 = aim.RotateVector(p1 - p0);
        var d1 = aim.RotateVector(p2 - p1);
        p1 = 0.125f * p1 + 0.875f * (p0 + d0);
        p2 = p1 + d1;
        v1 = aim.RotateVector(v1);
        v2 = aim.RotateVector(v2);
    }

    public static SlugPose Lerp(in SlugPose x, in SlugPose y, float t)
    {
        return new()
        {
            p0 = Vector2.Lerp(x.p0, y.p0, t),
            p1 = Vector2.Lerp(x.p1, y.p1, t),
            p2 = Vector2.Lerp(x.p2, y.p2, t),
            v0 = Vector2.Lerp(x.v0, y.v0, t),
            v1 = Vector2.Lerp(x.v1, y.v1, t),
            v2 = Vector2.Lerp(x.v2, y.v2, t)
        };
    }

    public static float MaxDifference(in SlugPose pose1, in SlugPose pose2)
    {
        var d0 = MathTools.MaxDifference(pose1.Ctrl0, pose2.Ctrl0);
        var d1 = MathTools.MaxDifference(pose1.Ctrl1, pose2.Ctrl1);
        var d2 = MathTools.MaxDifference(pose1.Ctrl2, pose2.Ctrl2);
        return Mathf.Max(Mathf.Max(d0, d1), d2);
    }
}

public struct SlugAnimationUtility : IAnimationUtility<SlugPose>
{
    public readonly SlugPose AnimatedValue(in SlugPose startVal, in SlugPose goalVal, float t)
    {
        return SlugPose.Lerp(in startVal, in goalVal, t);
    }

    public readonly float InitialTimer(in SlugPose startVal, in SlugPose goalVal, float speed)
    {
        return 1 - Mathf.Clamp(SlugPose.MaxDifference(in startVal, in goalVal) / speed, 0, 1);
    }
}