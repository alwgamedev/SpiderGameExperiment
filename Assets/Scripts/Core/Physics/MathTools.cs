using UnityEngine;

public static class MathTools
{
    static System.Random rng;

    public const float o41 = 10E-05f;
    //public const float o31 = 10E-4f;

    public const float cos15 = 0.9659258f;
    public const float sin15 = 0.2588190f;
    public const float cos22pt5 = 0.9238795f;
    public const float sin22pt5 = 0.3826834f;
    public const float cos30 = 0.8660254f;//sqrt(3)/2
    public const float sin30 = 0.5f;
    public const float tan30 = 0.5773502f;// 1/sqrt(3)
    public const float cos45 = 0.7071067f;// 1/sqrt(2)
    public const float sin45 = cos45;
    public const float tan45 = 1;
    public const float cos60 = sin30;
    public const float sin60 = cos30;
    public const float tan60 = 1.732051f;//sqrt(3)

    public enum OrientationXZ
    {
        front, back, right, left
    }

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

    public static Vector2 CubicInterpolation(Vector2 p, Vector2 v, Vector2 q, Vector2 w, float t)
    {
        var s = 1 - t;
        var s2 = s * s;
        var t2 = t * t;
        return s2 * s * p + s2 * t * (v + 3 * p) + s * t2 * (-w + 3 * q) + t2 * t * q;//fewer operations when written in Bezier form
    }

    public static Vector2 CubicTangent(Vector2 p, Vector2 v, Vector2 q, Vector2 w, float t)
    {
        var s = 1 - t;
        return s * s * v + 2 * s * t * (3 * (q - p) - v - w) + t * t * w;
    }

    public static float LerpAtConstantSpeed(float from, float to, float speed, float dt)
    {
        return from == to ? from : from < to ? Mathf.Min(from + speed * dt, to) : Mathf.Max(from - speed * dt, to);
    }

    public static Vector2 LerpAtConstantSpeed(Vector2 from, Vector2 to, float speed, float dt)
    {
        var dist = Vector2.Distance(from, to);
        var maxDist = speed * dt;
        var t = maxDist / dist;
        return float.IsNormal(t) ? Vector2.Lerp(from, to, t) : from;
    }

    /// <summary>
    /// lerp x and y by difft amounts
    /// </summary>
    public static Vector2 Lerp(Vector2 v, Vector2 w, float t1, float t2)
    {
        return new(t1 > 0 ? t1 < 1 ? v.x + t1 * (w.x - v.x) : w.x : v.x, t2 > 0 ? t2 < 1 ? v.y + t2 * (w.y - v.y) : w.y : v.y);
    }

    public static Vector2 LerpUnclamped(Vector2 v, Vector2 w, float t1, float t2)
    {
        return new Vector2(v.x + t1 * (w.x - v.x), v.y + t2 * (w.y - v.y));
    }

    public static bool OppositeSigns(int x, int y)
    {
        return (x > 0 && y < 0) || (x < 0 && y > 0);
    }

    public static bool OppositeSigns(float x, float y)
    {
        return (x > 0 && y < 0) || (x < 0 && y > 0);
    }

    public static float RandomFloat(float min, float max)
    {
        return min + (max - min) * (float)RNG.NextDouble();
    }

    /// <summary>
    /// Apply matrix (c1, c2) to v
    /// </summary>
    public static Vector2 ApplyTransformation(this Vector2 v, Vector2 c1, Vector2 c2)
    {
        return new(c1.x * v.x + c2.x * v.y, c1.y * v.x + c2.y * v.y);
    }

    public static Vector3 ApplyTransformation(this Vector3 v, Vector3 c1, Vector3 c2, Vector3 c3)
    {
        return new(c1.x * v.x + c2.x * v.y + c3.x * v.z, c1.y * v.x + c2.y * v.y + c3.y * v.z, c1.z * v.x + c2.z + v.y + c3.z * v.z);
    }

    public static Vector2 InFrame(this Vector2 v, Vector2 b1, Vector2 b2)
    {
        return new(Vector2.Dot(v, b1), Vector2.Dot(v, b2));
    }

    //to avoid casting
    public static Vector2 InFrame(this Vector2 v, Vector3 b1, Vector3 b2)
    {
        return new(v.x * b1.x + v.y * b1.y, v.x * b2.x + v.y * b2.y);
    }

    public static Vector3 InFrame(this Vector3 v, Vector3 b1, Vector3 b2, Vector3 b3)
    {
        return new(Vector3.Dot(v, b1), Vector3.Dot(v, b2), Vector3.Dot(v, b3));
    }    
    
    //to avoid casting... OK getting a little stupid at this point
    public static Vector2 InFrameV2(this Vector3 v, Vector3 b1, Vector3 b2)
    {
        return new(v.x * b1.x + v.y * b1.y, v.x * b2.x + v.y * b2.y);
    }

    public static Vector2 InFrameV2(this Vector3 v, Vector2 b1, Vector2 b2)
    {
        return new(v.x * b1.x + v.y * b1.y, v.x * b2.x + v.y * b2.y);
    }

    /// <param name="p">point to be reflected</param>
    /// <param name="planeNormal">a unit normal to plane being reflected over</param>
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
    
    //when v1, v2 are unit vectors, this equals the sine of the CCW angle from v1 to v2 (being dot(v1, v2.CWPerp()) = cos(theta-90))
    public static float Cross2D(Vector2 v1, Vector2 v2)
    {
        return v1.x * v2.y - v1.y * v2.x;
    }

    /// <summary>
    /// Try to intersect lines p1 + t*v1 and p2 + t*v2.
    /// </summary>
    public static bool TryIntersectLine(Vector2 p1, Vector2 v1, Vector2 p2, Vector2 v2, out Vector2 intersection)
    {
        var det = Cross2D(v1, v2);
        if (det == 0)// parallel lines
        {
            intersection = p1;
            return Cross2D(p2 - p1, v1) == 0;
        }

        float b1 = Cross2D(v1, p1);
        float b2 = Cross2D(v2, p2);
        det = 1 / det;
        intersection = new Vector2(det * (v2.x * b1 - v1.x * b2), det * (v2.y * b1 - v1.y * b2));
        return true;
    }

    public static Vector2 Circumcenter(Vector2 p0, Vector2 p1, Vector2 p2)
    {
        if (!TryIntersectLine(0.5f * (p0 + p1), (p1 - p0).CCWPerp(), 0.5f * (p0 + p2), (p2 - p0).CCWPerp(), out var c))
        {
            Debug.LogWarning($"Invalid triangle (vertices are colinear).");
        }
        return c;
    }

    //returns pt on line that is at desired distance from ptOffLine
    //public static bool PointOnLineAtDistance(Vector2 ptOffLine, Vector2 lineOrigin, Vector2 lineDirection, float dist, out Vector2 ptOnLine)
    //{
    //    var proj = Vector2.Dot(ptOffLine - lineOrigin, lineDirection);
    //    var q = lineOrigin + proj * lineDirection;
    //    var a2 = Vector2.SqrMagnitude(ptOffLine - q);
    //    var l2 = dist * dist;
    //    if (l2 < a2)//desired distance is less than distance to line, so just go dist units towards line
    //    {
    //        var n = lineDirection.CCWPerp();
    //        if (Vector2.Dot(q - ptOffLine, n) < 0)
    //        {
    //            n = -n;
    //        }
    //        ptOnLine =  ptOffLine + dist * n;
    //        return false;
    //    }
    //    ptOnLine = q + Mathf.Sqrt(l2 - a2) * lineDirection;
    //    return true;
    //}

    public static Quaternion QuaternionFrom2DUnitVector(Vector2 u)
    {
        if (!(u.x < 1))
        {
            return Quaternion.identity;
        }
        if (!(u.x > -1))
        {
            return new(0, 0, u.y < 0 ? -1 : 1, 0);
        }
        return new Quaternion(0, 0, u.y < 0 ? -Mathf.Sqrt(0.5f * (1 - u.x)) : Mathf.Sqrt(0.5f * (1 - u.x)), Mathf.Sqrt(0.5f * (1 + u.x)));
    }

    public static Quaternion InverseOfUnitQuaternion(Quaternion q)
    {
        return new(-q.x, -q.y, -q.z, q.w);
    }

    /// <summary>
    /// Rotates w around the axis perpendicular to plane spanned by a & b, by the angle that takes a to b.
    /// If a, b, or a x b is zero, returns w.
    /// </summary>
    public static Vector3 FromToRotation(Vector3 a, Vector3 b, Vector3 w, bool alreadyNormalized = false)
    {
        if (!alreadyNormalized)
        {
            a = a.normalized;
            b = b.normalized;
        }

        if (a == Vector3.zero || b == Vector3.zero)
        {
            return w;
        }

        var c = Vector3.Cross(a, b);
        var u = c.normalized;

        if (u == Vector3.zero)
        {
            return w;
        }

        //follows from the 2d version below (just add the u-component of w, which is unchanged by the rotation)
        return Vector3.Dot(a, b) * w + Vector3.Cross(c, w) + Vector3.Dot(u, w) * u;
    }

    public static Vector2 FromToRotation(Vector2 a, Vector2 b, Vector2 w, bool alreadyNormalized = false)
    {
        if (!alreadyNormalized)
        {
            a = a.normalized;
            b = b.normalized;
        }

        if (a == Vector2.zero || b == Vector2.zero)
        {
            return w;
        }

        //this is (b dot a)w + (b dot aPerp)wPerp
        return Vector2.Dot(a, b) * w + Cross2D(a,b) * w.CCWPerp();
    }

    /// <summary>
    /// fast alternative to arctan; as angle from u1 to u2 varies over (-pi,pi], output varies smoothly from -1 to 1 (specifically output is sin(theta/2), where theta is the correct angle)
    /// </summary>
    public static float PseudoAngle(Vector2 u1, Vector2 u2)
    {
        var cos = Vector2.Dot(u1, u2);
        if (!(cos < 1))//cos > 1 can occur from rounding errors
        {
            return 0;
        }
        var sin = Cross2D(u1, u2);
        return sin < 0 ? -Mathf.Sqrt(0.5f * (1 - cos)) : Mathf.Sqrt(0.5f * (1 - cos));
        //also get an acceptable function without square roots, but the angle is much too small around small angles and it gives a pretty bad feel to rotations
        //(we end up with +/- sin^2(theta/2) ~ theta^2/4 for small theta)
    }

    public static float AbsolutePseudoAngle(Vector2 u1, Vector2 u2)
    {
        var cos = Vector2.Dot(u1, u2);
        if (!(cos < 1))
        {
            return 0;
        }
        return Mathf.Sqrt(0.5f * (1 - cos));
    }

    /// <summary>
    /// u1, u2 unit vectors. Don't try to do more than 180 deg of rotation.
    /// </summary>
    public static Vector2 CheapRotationalLerp(Vector2 u1, Vector2 u2, float lerpAmount, out bool changed)
    {
        //better than using explicit angles etc.
        if (lerpAmount == 0 || u1 == u2)
        {
            changed = false;
            return u1;
        }
        if (u1 == -u2)
        {
            changed = true;
            //return Vector2.LerpUnclamped(u1, u1.CCWPerp(), 2 * lerpAmount);
            return lerpAmount > 0 ?
                Vector2.LerpUnclamped(u1, u1.CCWPerp(), 2 * lerpAmount).normalized
                : Vector2.LerpUnclamped(u1, u1.CWPerp(), -2 * lerpAmount).normalized;
        }
        changed = true;
        return Vector2.LerpUnclamped(u1, u2, lerpAmount).normalized;
    }

    public static Vector2 CheapRotationalLerpClamped(Vector2 u1, Vector2 u2, float lerpAmount, out bool changed)
    {
        //better than using explicit angles etc.
        if (lerpAmount == 0 || u1 == u2)
        {
            changed = false;
            return u1;
        }
        if (u1 == -u2)
        {
            changed = true;
            //return Vector2.Lerp(u1, u1.CCWPerp(), 2 * lerpAmount).normalized;
            return lerpAmount > 0 ?
                Vector2.Lerp(u1, u1.CCWPerp(), 2 * lerpAmount).normalized
                : Vector2.Lerp(u1, u1.CWPerp(), -2 * lerpAmount).normalized;
        }
        changed = true;
        return Vector2.Lerp(u1, u2, lerpAmount).normalized;
    }

    public static void ApplyCheapRotationalLerp(this Transform t, Vector2 goalRight, float lerpAmount, out bool changed)
    {
        var v = CheapRotationalLerp(t.right, goalRight, lerpAmount, out changed);
        if (changed)
        {
            t.rotation = QuaternionFrom2DUnitVector(v);
        }
    }

    public static void ApplyCheapRotationalLerpClamped(this Transform t, Vector2 goalRight, float lerpAmount, out bool changed)
    {
        var v = CheapRotationalLerpClamped(t.right, goalRight, lerpAmount, out changed);
        if (changed)
        {
            t.rotation = QuaternionFrom2DUnitVector(v);
        }
    }

    public static void ApplyCheapRotationalLerp(this Transform t, Vector2 u, Vector2 v, float lerpAmount, out bool changed)
    {
        v = new(Vector2.Dot(v, u), Vector2.Dot(v, u.CCWPerp()));
        t.ApplyCheapRotationalLerp(v, lerpAmount, out changed);
    }

    public static void ApplyCheapRotationalLerpClamped(this Transform t, Vector2 u, Vector2 v, float lerpAmount, out bool changed)
    {
        v = Vector2.Dot(v, u) * t.right + Vector2.Dot(v, u.CCWPerp()) * t.up;
        t.ApplyCheapRotationalLerpClamped(v, lerpAmount, out changed);
    }

    public static Vector2 CheapRotation(Vector2 u, float angleInRadians, out bool changed)
    {
        //return CheapFromToRotation(u, u.CCWPerp(), angleInRadians, out changed);
        return angleInRadians > 0 ? CheapFromToRotation(u, u.CCWPerp(), angleInRadians, out changed)
            : CheapFromToRotation(u, u.CWPerp(), -angleInRadians, out changed);
    }

    public static Vector2 CheapFromToRotation(Vector2 u1, Vector2 u2, float angleInRadians, out bool changed)
    {
        var t = AbsolutePseudoAngle(u1, u2);
        if (t == 0)
        {
            changed = false;
            return u1;
        }
        return CheapRotationalLerp(u1, u2, angleInRadians / (Mathf.PI * t), out changed);
    }

    public static Vector2 CheapFromToRotationClamped(Vector2 u1, Vector2 u2, float angleInRadians, out bool changed)
    {
        var t = AbsolutePseudoAngle(u1, u2);
        if (t == 0)
        {
            changed = false;
            return u1;
        }
        return CheapRotationalLerpClamped(u1, u2, angleInRadians / (Mathf.PI * t), out changed);
    }

    /// <summary>
    /// u1, u2 unit vectors
    /// </summary>
    public static Vector2 CheapRotationBySpeed(Vector2 u1, Vector2 u2, float rotationalSpeed, float dt, out bool changed)
    {
        var c = AbsolutePseudoAngle(u1, u2);
        if (c == 0)
        {
            changed = false;
            return u1;
        }
        return CheapRotationalLerp(u1, u2, rotationalSpeed * dt / (Mathf.PI * c), out changed);
    }

    public static Vector2 CheapRotationBySpeedClamped(Vector2 u1, Vector2 u2, float rotationalSpeed, float dt, out bool changed)
    {
        var c = AbsolutePseudoAngle(u1, u2);
        if (c == 0)//bc need to divide by 
        {
            changed = false;
            return u1;
        }
        return CheapRotationalLerpClamped(u1, u2, rotationalSpeed * dt / (Mathf.PI * c), out changed);
    }

    public static void ApplyCheapRotationBySpeed(this Transform t, float rotationalSpeed, float dt, out bool changed)
    {
        var v = CheapRotationBySpeed(t.right, t.up, rotationalSpeed, dt, out changed);
        if (changed)
        {
            t.rotation = QuaternionFrom2DUnitVector(v);
        }
    }

    public static void ApplyCheapRotationBySpeedClamped(this Transform t, Vector2 goalRight, float rotationalSpeed, float dt, out bool changed)
    {
        var v = CheapRotationBySpeedClamped(t.right, goalRight, rotationalSpeed, dt, out changed);
        if (changed)
        {
            t.rotation = QuaternionFrom2DUnitVector(v);
        }
    }

    public static RaycastHit2D DebugRaycast(Vector2 origin, Vector2 direction, float length, int layerMask, Color drawColor)
    {
#if UNITY_EDITOR
        Debug.DrawLine(origin, origin + length * direction, drawColor);
#endif
        return Physics2D.Raycast(origin, direction, length, layerMask);
    }

    public static void SetKinematic(this Rigidbody2D rb)
    {
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.totalForce = Vector2.zero;
        rb.linearVelocity = Vector2.zero;
    }
}
