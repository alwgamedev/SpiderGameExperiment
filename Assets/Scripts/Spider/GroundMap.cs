using UnityEngine;
using System;

[Serializable]
public struct GroundMap
{
    public int numFwdIntervals;
    public float intervalWidth;
    public LayerMask raycastLayerMask;

    [SerializeField] GroundMapPt[] map;

    //in future, can cache some of these and make numFwdIntervals and intervalWidth private
    public int NumPts => (numFwdIntervals << 1) | 1;
    public float MapHalfWidth => intervalWidth * numFwdIntervals;
    public GroundMapPt Center => map[numFwdIntervals];
    public GroundMapPt LeftEndPt => map[0];
    public GroundMapPt RightEndPt => map[^1];
    public GroundMapPt this[int i]
    {
        get
        {
            int n = NumPts;
            if (i >= n)
            {
                return RightEndPt;
            }
            if (i < 0)
            {
                return LeftEndPt;
            }
            return map[i];
        }
    }

    public bool IsCentralIndex(int i)
    {
        return i == numFwdIntervals;
    }

    public int IndexOfFirstForwardGroundHit()
    {
        for (int i = numFwdIntervals; i < NumPts; i++)
        {
            if (map[i].hitGround)
            {
                return i;
            }
        }
        return numFwdIntervals;
    }

    public int IndexOfFirstBackwardGroundHit()
    {
        for (int i = numFwdIntervals; i > -1; i--)
        {
            if (map[i].hitGround)
            {
                return i;
            }
        }
        return numFwdIntervals;
    }

    public GroundMapPt PointFromCenterByIndex(int i)
    {
        if (i >= numFwdIntervals)
        {
            return RightEndPt;
        }
        if (i <= -numFwdIntervals)
        {
            return LeftEndPt;
        }
        return map[numFwdIntervals + i];
    }
    public Vector2 ProjectOntoGround(Vector2 p)
    {
        var x = Vector2.Dot(p - Center.point, Center.normal.CWPerp());
        return PointFromCenterByPosition(x);
    }

    public Vector2 PointFromCenterByPosition(float x)
    {
        if (x > 0)
        {
            for (int i = numFwdIntervals; i < numFwdIntervals << 1; i++)
            {
                var p = map[i + 1];
                if (p.horizontalPosition > x)
                {
                    var q = map[i];
                    var s = p.horizontalPosition - q.horizontalPosition;
                    return Vector2.Lerp(q.point, p.point, (x - q.horizontalPosition) / s);
                }
            }

            var t = x - RightEndPt.horizontalPosition;
            return RightEndPt.point + t * RightEndPt.normal.CWPerp();
        }

        for (int i = numFwdIntervals; i > 0; i--)
        {
            var p = map[i - 1];
            if (p.horizontalPosition < x)
            {
                var q = map[i];
                var s = p.horizontalPosition - q.horizontalPosition;
                return Vector2.Lerp(q.point, p.point, (x - q.horizontalPosition) / s);
            }
        }

        var u = x - LeftEndPt.horizontalPosition;
        return LeftEndPt.point + u * LeftEndPt.normal.CWPerp();
    }

    public int IndexOfLastMarkedPointBeforePosition(float x)
    {
        if (x > 0)
        {
            int n = NumPts;
            for (int i = numFwdIntervals + 1; i < NumPts; i++)
            {
                if (map[i].horizontalPosition > x)
                {
                    return i - 1;
                }
            }

            return n - 1;
        }

        for (int i = numFwdIntervals - 1; i > -1; i--)
        {
            if (map[i].horizontalPosition < x)
            {
                return i + 1;
            }
        }

        return 0;
    }

    public void UpdateMap(Vector2 origin, Vector2 originDown, float raycastLength)
    {
        //n = 2 * numFwdPts + 1
        int n = NumPts;
        if (map == null || map.Length != n)
        {
            map = new GroundMapPt[n];
        }

        var r = MathTools.DebugRaycast(origin, originDown, raycastLength, raycastLayerMask, Color.red);
        map[numFwdIntervals] = r ? new GroundMapPt(r.point, r.normal, 0, r.distance, true)
            : new GroundMapPt(origin + raycastLength * originDown, -originDown, 0, raycastLength, false);
        var center = map[numFwdIntervals].point;
        var centerCastRight = originDown.CCWPerp();

        for (int i = numFwdIntervals; i < n - 1; i++)
        {
            //set map[i + 1]
            var lastMapPt = map[i];
            var lastNormal = lastMapPt.normal;
            var lastTangent = lastNormal.CWPerp();
            var lastRaycastLength = lastMapPt.raycastDistance;
            var o = lastMapPt.point + lastRaycastLength * lastNormal;

            //first raycast horizontally to check if ground kicks UP suddenly (running into a wall e.g.)
            r = MathTools.DebugRaycast(o, lastTangent, intervalWidth, raycastLayerMask, Color.blue);
            if (r)
            {
                var h = lastMapPt.horizontalPosition + Vector2.Distance(lastMapPt.point, r.point);
                map[i + 1] = new GroundMapPt(r.point, r.normal, h, r.distance, true);
            }
            else
            {
                //now shift forward and raycast down
                o += intervalWidth * lastTangent;
                r = MathTools.DebugRaycast(o, -lastNormal, raycastLength, raycastLayerMask, Color.cyan);
                if (r)
                {
                    var h = lastMapPt.horizontalPosition + Vector2.Distance(lastMapPt.point, r.point);
                    map[i + 1] = new GroundMapPt(r.point, r.normal, h, r.distance * Vector2.Dot(lastNormal, r.normal), true);
                }
                else
                {
                    var l = Mathf.Min(lastRaycastLength + 0.5f * intervalWidth, raycastLength);
                    r = Physics2D.Raycast(o - l * lastNormal, -lastTangent, 2 * intervalWidth, raycastLayerMask);
                    if (r)
                    {
                        var h = lastMapPt.horizontalPosition + Vector2.Distance(lastMapPt.point, r.point);
                        map[i + 1] = new GroundMapPt(r.point, r.normal, h, r.distance, true);
                    }
                    else
                    {
                        var p = o - raycastLength * lastNormal;
                        var h = lastMapPt.horizontalPosition + Vector2.Distance(lastMapPt.point, p);
                        map[i + 1] = new GroundMapPt(p, lastNormal, h, raycastLength, false);
                    }
                }
            }
        }

        for (int i = numFwdIntervals; i > 0; i--)
        {
            //set map[i - 1]
            var lastMapPt = map[i];
            var lastNormal = lastMapPt.normal;
            var lastTangent = lastNormal.CCWPerp();
            var lastRaycastLength = lastMapPt.raycastDistance;
            var o = lastMapPt.point + lastRaycastLength * lastNormal;

            r = MathTools.DebugRaycast(o, lastTangent, intervalWidth, raycastLayerMask, Color.blue);
            if (r)
            {
                var h = lastMapPt.horizontalPosition + Vector2.Distance(lastMapPt.point, r.point);
                map[i + 1] = new GroundMapPt(r.point, r.normal, h, r.distance, true);
            }
            else
            {
                //now shift forward and raycast down
                o += intervalWidth * lastTangent;
                r = MathTools.DebugRaycast(o, -lastNormal, raycastLength, raycastLayerMask, Color.cyan);
                if (r)
                {
                    var h = lastMapPt.horizontalPosition - Vector2.Distance(lastMapPt.point, r.point);
                    map[i - 1] = new GroundMapPt(r.point, r.normal, h, r.distance * Vector2.Dot(lastNormal, r.normal), true);
                }
                else
                {
                    var l = Mathf.Min(lastRaycastLength + 0.5f * intervalWidth, raycastLength);
                    r = Physics2D.Raycast(o - l * lastNormal, -lastTangent, 2 * intervalWidth, raycastLayerMask);
                    if (r)
                    {
                        var h = lastMapPt.horizontalPosition - Vector2.Distance(lastMapPt.point, r.point);
                        map[i - 1] = new GroundMapPt(r.point, r.normal, h, r.distance, true);
                    }
                    else
                    {
                        var p = o - raycastLength * lastNormal;
                        var h = lastMapPt.horizontalPosition - Vector2.Distance(lastMapPt.point, p);
                        map[i - 1] = new GroundMapPt(p, lastNormal, h, raycastLength, false);
                    }
                }
            }
        }
    }

    public void DrawGizmos()
    {
        if (map == null || map.Length == 0) return;

        Gizmos.color = Color.green;
        for (int i = 0; i < numFwdIntervals; i++)
        {
            Gizmos.DrawSphere(map[i].point, 0.1f);
        }
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(map[numFwdIntervals].point, 0.1f);
        Gizmos.color = Color.yellow;
        for (int i = numFwdIntervals + 1; i < NumPts; i++)
        {
            Gizmos.DrawSphere(map[i].point, 0.1f);
        }
    }
}

[Serializable]//just so i can watch them in the inspector
public readonly struct GroundMapPt
{
    public readonly Vector2 point;
    public readonly Vector2 normal;
    public readonly float horizontalPosition;
    public readonly float raycastDistance;
    public readonly bool hitGround;

    public GroundMapPt(Vector2 point, Vector2 normal, float horizontalPosition, float raycastDistance, bool hitGround)
    {
        this.point = point;
        this.normal = normal;
        this.horizontalPosition = horizontalPosition;
        this.raycastDistance = raycastDistance;
        this.hitGround = hitGround;
    }
}