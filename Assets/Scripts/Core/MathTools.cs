using UnityEngine;

public static class MathTools
{
    static System.Random rng;

    public const float cos30 = 0.86602540378f;// sqrt(3)/2
    public const float sin30 = 0.5f;
    public const float tan30 = 0.57735026919f;// 1/sqrt(3)
    public const float cos45 = 0.70710678118f;// 1/sqrt(2)
    public const float sin45 = cos45;
    public const float tan45 = 1;
    public const float cos60 = sin30;
    public const float sin60 = cos30;
    public const float tan60 = 1.73205080757f;// sqrt(3)

    public static System.Random RNG
    {
        get
        {
            if (rng == null)
            {
                rng = new();
            }

            return rng;
        }
    }

    public static float RandomFloat(float min, float max)
    {
        return min + (max - min) * (float)RNG.NextDouble();
    }

    /// <param name="p">point to be reflect</param>
    /// <param name="planeNormal">unit vector</param>
    public static Vector3 ReflectAcrossHyperplane(this Vector3 p, Vector3 planeNormal)
    {
        return p - 2 * Vector3.Dot(p, planeNormal) * planeNormal;
    }

    public static Vector2 ReflectAcrossHyperplane(this Vector2 p, Vector2 planeNormal)
    {
        return p - 2 * Vector2.Dot(p, planeNormal) * planeNormal;
    }

    public static Vector2 CCWPerp(this Vector2 v)
    {
        return new Vector2(-v.y, v.x);
    }

    public static Vector2 CWPerp(this Vector2 v)
    {
        return new Vector2(v.y, -v.x);
    }

    /// <summary>
    /// u1, u2 unit vectors
    /// </summary>
    public static Vector2 CheapRotationalLerp(Vector2 u1, Vector2 u2, float lerpAmount)
    {
        //better than using explicit angles etc.
        return Vector2.Lerp(u1, u2, lerpAmount).normalized;
    }

    public static RaycastHit2D DebugRaycast(Vector2 origin, Vector2 direction, float length, int layerMask, Color drawColor)
    {
#if UNITY_EDITOR
        Debug.DrawLine(origin, origin + length * direction, drawColor);
#endif
        return Physics2D.Raycast(origin, direction, length, layerMask);
    }
}
