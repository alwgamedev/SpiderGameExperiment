using UnityEngine;

public static class MathTools
{
    static System.Random rng;

    public const float o51 = 10E-05f;

    public const float cos15 = 0.9659258f;
    public const float sin15 = 0.2588190f;
    public const float cos30 = 0.8660254f;//sqrt(3)/2
    public const float sin30 = 0.5f;
    public const float tan30 = 0.5773502f;// 1/sqrt(3)
    public const float cos45 = 0.7071067f;// 1/sqrt(2)
    public const float sin45 = cos45;
    public const float tan45 = 1;
    public const float cos60 = sin30;
    public const float sin60 = cos30;
    public const float tan60 = 1.7320508f;//sqrt(3)
    public const float cos75 = 0.2588190f;
    public const float sin75 = 0.9659258f;

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

    public static float LerpAtConstantRate(float from, float to, float rate, float dt)
    {
        return from == to ? from : from < to ? Mathf.Min(from + rate * dt, to) : Mathf.Max(from - rate * dt, to);
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
    
    //when v1, v2 are unit vectors, this equals the sine of the angle from v1 to v2 (being dot(v1, v2.CWPerp()) = cos(theta-90))
    public static float Cross2D(Vector2 v1, Vector2 v2)
    {
        return v1.x * v2.y - v1.y * v2.x;
    }

    /// <summary>
    /// as angle from u1 to u2 varies over (-pi,pi], output varies smoothly from -1 to 1 (specifically output is sin(theta/2), where theta is the correct angle)
    /// (~5-10x faster than arctan)
    /// </summary>
    public static float PseudoAngle(Vector2 u1, Vector2 u2)
    {
        var cos = Vector2.Dot(u1, u2);
        var sin = Cross2D(u1, u2);
        if (cos > 1)//just in case of rounding errors
        {
            cos = 1;
        }
        return sin < 0 ? -Mathf.Sqrt(0.5f * (1 - cos)) : Mathf.Sqrt(0.5f * (1 - cos));
    }

    public static float AbsolutePseudoAngle(Vector2 u1, Vector2 u2)
    {
        var cos = Vector2.Dot(u1, u2);
        var sin = Cross2D(u1, u2);
        if (cos > 1)//just in case of rounding errors
        {
            cos = 1;
        }
        return Mathf.Sqrt(0.5f * (1 - cos));
    }

    /// <summary>
    /// u1, u2 unit vectors
    /// </summary>
    public static Vector2 CheapRotationalLerp(Vector2 u1, Vector2 u2, float lerpAmount)
    {
        //better than using explicit angles etc.
        if (u1 == u2)
        {
            return u1;
        }
        if (u1 == -u2)
        {
            return Vector2.LerpUnclamped(u1, u2.CCWPerp(), 2 * lerpAmount).normalized;
        }
        return Vector2.LerpUnclamped(u1, u2, lerpAmount).normalized;
    }

    //2do: this sometimes rotates wrong way and settles on -u2 instead of u2
    /// <summary>
    /// u1, u2 unit vectors
    /// </summary>
    public static Vector2 CheapRotationBySpeed(Vector2 u1, Vector2 u2, float rotationalSpeed, float dt)
    {
        var c = AbsolutePseudoAngle(u1, u2);
        if (c == 0)
        {
            return u1;
        }
        return CheapRotationalLerp(u1, u2, rotationalSpeed * dt / (Mathf.PI * c));
    }

    //i.e. rotate keeping center pt fixed
    public static void RotateAroundPoint(this Transform t, Vector3 center, Vector3 newRight)
    {
        var d = center - t.position;
        t.position += d;
        t.right = newRight;
        t.position -= d;
    }

    public static RaycastHit2D DebugRaycast(Vector2 origin, Vector2 direction, float length, int layerMask, Color drawColor)
    {
#if UNITY_EDITOR
        Debug.DrawLine(origin, origin + length * direction, drawColor);
#endif
        return Physics2D.Raycast(origin, direction, length, layerMask);
    }

    //public static Vector2 ClosestPointOnPerimeter(this Bounds bounds, Vector2 point)
    //{
    //    var x = point.x;
    //    var y = point.y;
    //    var x1 = x < bounds.center.x ? bounds.min.x : bounds.max.x;
    //    var y1 = y < bounds.center.y ? bounds.min.y : bounds.max.y;
    //    var dx = Mathf.Abs(x - x1);
    //    var dy = Mathf.Abs(y - y1);
    //    return dx < dy ? new Vector2(x1, y) : new Vector2(x, y1);
    //}

    ////point will only move horizontally or vertically, works well when point is very near edge of collider
    ////yeah but then our collision only pushes us back horizontally or vertically...
    //public static Vector2 CheapClosestPointOnPerimeter(this Collider2D collider, Vector2 point, RaycastHit2D[] buffer)
    //{
    //    var b = collider.bounds.ClosestPointOnPerimeter(point);
    //    var d = point - b;
    //    var dir = d.x == 0 ? (d.y > 0 ? Vector2.up : Vector2.down) : (d.x > 0 ? Vector2.right : Vector2.left);
    //    var dist = d.x == 0 ? Mathf.Abs(d.y) : Mathf.Abs(d.x);
    //    var layer = 1 << collider.gameObject.layer;
    //    buffer ??= new RaycastHit2D[64];
    //    Physics2D.RaycastNonAlloc(b, dir, buffer, dist, layer);
    //    Vector2 lastHit = b;
    //    for (int i = 0; i < buffer.Length; i++)
    //    {
    //        if (buffer[i].collider == collider)
    //        {
    //            lastHit = buffer[i].point;
    //        }
    //    }

    //    return lastHit;
    //}
}
