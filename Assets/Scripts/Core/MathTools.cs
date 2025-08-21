using UnityEngine;

public static class MathTools
{
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
}
