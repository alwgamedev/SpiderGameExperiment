using UnityEngine;
using System;

[Serializable]
public struct GroundMap
{
    public int numFwdIntervals;
    public float intervalWidth;
    //public LayerMask raycastLayerMask;

    Color RaycastColor0 => Color.clear;//Color.red;
    Color RaycastColor1 => Color.clear;//Color.blue;
    Color RaycastColor2 => Color.clear;//Color.cyan;

    Color GizmoColorCenter => Color.red;
    Color GizmoColorRight => Color.green;
    Color GizmoColorLeft => Color.yellow;

    [SerializeField] GroundMapPt[] map;

    public int CentralIndex => numFwdIntervals;
    public int NumPts => (numFwdIntervals << 1) | 1;
    public float MapHalfWidth => intervalWidth * numFwdIntervals;
    public bool AllPointsHitGround { get; private set; }
    public bool AnyPointsHitGround { get; private set; }
    public GroundMapPt Center => map[numFwdIntervals];
    public GroundMapPt LeftEndPt => map[0];
    public GroundMapPt RightEndPt => map[^1];
    public GroundMapPt this[int i]
    {
        get
        {
            if (i >= NumPts)
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

    public bool AllHitGround()
    {
        for (int i = 0; i < map.Length; i++)
        {
            if (!map[i].hitGround)
            {
                return false;
            }
        }

        return true;
    }

    public bool AnyHitGround()
    {
        for (int i = 0; i < map.Length; i++)
        {
            if (map[i].hitGround)
            {
                return true;
            }
        }

        return false;
    }

    public bool AllHitGroundFromCenter(float maxPosition)
    {
        if (!Center.hitGround)
        {
            return false;
        }

        maxPosition += Mathf.Epsilon;
        var minPosition = -maxPosition - Mathf.Epsilon;
        for (int i = 1; i < numFwdIntervals; i++)
        {
            var r = map[numFwdIntervals + i];
            if (r.horizontalPosition < maxPosition && !r.hitGround)
            {
                return false;
            }

            var l = map[numFwdIntervals - i];
            if (l.horizontalPosition > minPosition && l.hitGround)
            {
                return false;
            }

            if (r.horizontalPosition > maxPosition && l.horizontalPosition < minPosition)
            {
                return true;
            }
        }

        return true;
    }

    public bool AnyHitGroundFromCenter(float maxPosition)
    {
        if (Center.hitGround)
        {
            return true;
        }

        maxPosition += Mathf.Epsilon;
        var minPosition = -maxPosition - Mathf.Epsilon;
        for (int i = 1; i < numFwdIntervals; i++)
        {
            var r = map[numFwdIntervals + i];
            if (r.horizontalPosition < maxPosition && r.hitGround)
            {
                return true;
            }

            var l = map[numFwdIntervals - i];
            if (l.horizontalPosition > minPosition && l.hitGround)
            {
                return true;
            }

            if (r.horizontalPosition > maxPosition && l.horizontalPosition < minPosition)
            {
                return false;
            }
        }

        return false;
    }

    public int IndexOfFirstGroundHitFromCenter()
    {
        int i = CentralIndex;
        if (map[i].hitGround)
        {
            return i;
        }

        i++;
        int j = CentralIndex - 1;
        int n = NumPts;
        while (i < n && j > 0)
        {
            if (map[i].hitGround)
            {
                return i;
            }
            if (map[j].hitGround)
            {
                return j;
            }

            i++;
            j--;
        }

        return CentralIndex;
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

    public Vector2 ProjectOntoGround(Vector2 p, out Vector2 normal)
    {
        var x = Vector2.Dot(p - Center.point, Center.normal.CWPerp());
        return PointFromCenterByPosition(x, out normal);
    }

    public bool CastToGround(Vector2 origin, Vector2 direction, float distance, out Vector2 hitPt)
    {
        //idea: casting ray is the diagonal of a box. since ground map points are closely spaced and box is big, we can assume that a cast hit occurs between two points inside the box.
        //iterate through groundMap and find the first pair of points that are inside the rectangle and on opposite sides of the diagonal.
        //A) yes, this is much faster than intersecting the groundMap line segments with the casting line; we don't compute any intersections until we the very end
        //B) if the cast hits the ground in multiple places, this detects the one that is furthest left along the ground map, not the first hit along the casting line
        //^we could continue iterating and find the real first hit, but this is a waste considering multiple hits will rarely happen for us
        var endPt = origin + distance * direction;
        var p0 = new Vector2(Mathf.Min(origin.x, endPt.x), Mathf.Min(origin.y, endPt.y));//lower left corner of box
        var p1 = new Vector2(Mathf.Max(origin.x, endPt.x), Mathf.Min(origin.y, endPt.y));//upper right corner of box

        bool IsInBox(Vector2 p)
        {
            return !(p.x < p0.x) && !(p.y < p0.y) && !(p.x > p1.x) && !(p.y > p1.y);
        }

        bool OnRightSideOfBox(Vector2 p)
        {
            return Vector2.Dot(p - origin, direction) > 0;
        }

        bool lastInBox = IsInBox(map[0].point);
        bool lastSide = OnRightSideOfBox(map[0].point);
        bool tempLastInBox = lastInBox;
        bool tempLastSide = lastSide;

        for (int i = 1; i < map.Length; i++)
        {
            tempLastInBox = IsInBox(map[i].point);
            tempLastSide = OnRightSideOfBox(map[i].point);
            if (lastInBox && tempLastInBox && lastSide != tempLastSide)
            {
                if (MathTools.TryIntersectLine(map[i].point, map[i + 1].point - map[i].point, origin, direction, out hitPt))
                {
                    return true;
                }
            }

            lastInBox = tempLastInBox;
            lastSide = tempLastSide;
        }

        hitPt = endPt;
        return false;
    }

    public Vector2 PointFromCenterByPosition(float x, out Vector2 normal)
    {
        if (x > 0)
        {
            for (int i = numFwdIntervals; i < map.Length - 1; i++)
            {
                var p = map[i + 1];
                if (p.horizontalPosition > x)
                {
                    var q = map[i];
                    var s = p.horizontalPosition - q.horizontalPosition;
                    normal = q.normal;
                    return Vector2.Lerp(q.point, p.point, (x - q.horizontalPosition) / s);
                }
            }

            var t = x - RightEndPt.horizontalPosition;
            normal = RightEndPt.normal;
            return RightEndPt.point + t * RightEndPt.normal.CWPerp();
        }

        for (int i = numFwdIntervals; i > 0; i--)
        {
            var p = map[i - 1];
            if (p.horizontalPosition < x)
            {
                var q = map[i];
                var s = p.horizontalPosition - q.horizontalPosition;
                normal = q.normal;
                return Vector2.Lerp(q.point, p.point, (x - q.horizontalPosition) / s);
            }
        }

        var u = x - LeftEndPt.horizontalPosition;
        normal = LeftEndPt.normal;
        return LeftEndPt.point + u * LeftEndPt.normal.CWPerp();
    }

    public GroundMapPt PointFromCenterByPositionClamped(float x)
    {
        if (x > 0)
        {
            for (int i = numFwdIntervals; i < map.Length - 1; i++)
            {
                var p = map[i + 1];
                if (p.horizontalPosition > x)
                {
                    var q = map[i];
                    var s = p.horizontalPosition - q.horizontalPosition;
                    return new(Vector2.Lerp(q.point, p.point, (x - q.horizontalPosition) / s), q.normal, q.right, 0, 0, q.groundCollider);
                }
            }

            var t = x - RightEndPt.horizontalPosition;
            return RightEndPt;
        }

        for (int i = numFwdIntervals; i > 0; i--)
        {
            var p = map[i - 1];
            if (p.horizontalPosition < x)
            {
                var q = map[i];
                var s = p.horizontalPosition - q.horizontalPosition;
                return new(Vector2.Lerp(q.point, p.point, (x - q.horizontalPosition) / s), q.normal, q.right, 0, 0, q.groundCollider);
            }
        }

        var u = x - LeftEndPt.horizontalPosition;
        return LeftEndPt;
    }

    public int IndexOfLastMarkedPointBeforePosition(float x)
    {
        if (x > 0)
        {
            for (int i = numFwdIntervals + 1; i < map.Length; i++)
            {
                if (map[i].horizontalPosition > x)
                {
                    return i - 1;
                }
            }

            return map.Length - 1;
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

    public Vector2 AveragePointFromCenter(float minPos, float maxPos)
    {
        maxPos += Mathf.Epsilon;//so we can use < instead of <= (probably pointless)
        minPos -= Mathf.Epsilon;
        Vector2 sum = default;
        int ct = 0;

        for (int i = 0; i < map.Length; i++)
        {
            if (map[i].horizontalPosition < maxPos && map[i].horizontalPosition > minPos)
            {
                sum += map[i].point;
                ct++;
            }
        }

        return ct == 0 ? sum : sum / ct;
    }

    public Vector2 AverageNormalFromCenter(float minPos, float maxPos)
    {
        maxPos += Mathf.Epsilon;//so we can use < instead of <= (probably pointless)
        minPos -= Mathf.Epsilon;
        Vector2 sum = default;
        int ct = 0;

        for (int i = 0; i < map.Length; i++)
        {
            if (map[i].horizontalPosition < maxPos && map[i].horizontalPosition > minPos)
            {
                sum += map[i].normal;
                ct++;
            }
        }

        return ct == 0 ? sum : sum / ct;
    }

    public void UpdateMap(Vector2 origin, Vector2 originDown, Vector2 originRight, float raycastLength, int centralIndex, int raycastLayerMask)
    {
        int n = NumPts;
        if (map == null || map.Length != n)
        {
            map = new GroundMapPt[n];
        }

        AllPointsHitGround = true;
        AnyPointsHitGround = false;

        var r = MathTools.DebugRaycast(origin, originDown, raycastLength, raycastLayerMask, RaycastColor0);
        map[centralIndex] = r ? new GroundMapPt(r.point, r.normal, r.normal.CWPerp(), 0, r.distance, r.collider)
            : new GroundMapPt(origin + raycastLength * originDown, -originDown, originRight, 0, raycastLength, null);
        
        if (r)
        {
            AnyPointsHitGround = true;
        }
        else
        {
            AllPointsHitGround = false;
        }

        for (int i = centralIndex; i < n - 1; i++)
        {
            //set map[i + 1]
            var lastMapPt = map[i];
            var lastNormal = lastMapPt.normal;
            var lastRight = lastMapPt.right;
            var lastRaycastLength = lastMapPt.raycastDistance;
            var o = lastMapPt.point + lastRaycastLength * lastNormal;

            //first raycast horizontally to check if ground kicks UP suddenly (running into a wall e.g.)
            r = MathTools.DebugRaycast(o, lastRight, intervalWidth, raycastLayerMask, RaycastColor1);
            if (r)
            {
                var h = lastMapPt.horizontalPosition + Vector2.Distance(lastMapPt.point, r.point);
                map[i + 1] = new GroundMapPt(r.point, r.normal, r.normal.CWPerp(), h, r.distance, r.collider);
                if (!AnyPointsHitGround)
                {
                    AnyPointsHitGround = true;
                }
            }
            else
            {
                //now shift forward and raycast down
                o += intervalWidth * lastRight;
                r = MathTools.DebugRaycast(o, -lastNormal, raycastLength, raycastLayerMask, RaycastColor2);
                if (r)
                {
                    var h = lastMapPt.horizontalPosition + Vector2.Distance(lastMapPt.point, r.point);
                    map[i + 1] = new GroundMapPt(r.point, r.normal, r.normal.CWPerp(), h, r.distance * Vector2.Dot(lastNormal, r.normal), r.collider);
                    if (!AnyPointsHitGround)
                    {
                        AnyPointsHitGround = true;
                    }
                }
                else
                {
                    var l = Mathf.Min(lastRaycastLength + 0.5f * intervalWidth, raycastLength);
                    r = Physics2D.Raycast(o - l * lastNormal, -lastRight, 2 * intervalWidth, raycastLayerMask);
                    if (r)
                    {
                        var h = lastMapPt.horizontalPosition + Vector2.Distance(lastMapPt.point, r.point);
                        map[i + 1] = new GroundMapPt(r.point, r.normal, r.normal.CWPerp(), h, r.distance, r.collider);
                        if (!AnyPointsHitGround)
                        {
                            AnyPointsHitGround = true;
                        }
                    }
                    else
                    {
                        var p = o - raycastLength * lastNormal;
                        var h = lastMapPt.horizontalPosition + Vector2.Distance(lastMapPt.point, p);
                        map[i + 1] = new GroundMapPt(p, lastNormal, lastMapPt.right, h, raycastLength, null);
                        if (AllPointsHitGround)
                        {
                            AllPointsHitGround = false;
                        }
                    }
                }
            }
        }

        for (int i = centralIndex; i > 0; i--)
        {
            //set map[i - 1]
            var lastMapPt = map[i];
            var lastNormal = lastMapPt.normal;
            var lastTangent = -lastMapPt.right;
            var lastRaycastLength = lastMapPt.raycastDistance;
            var o = lastMapPt.point + lastRaycastLength * lastNormal;

            r = MathTools.DebugRaycast(o, lastTangent, intervalWidth, raycastLayerMask, RaycastColor1);
            if (r)
            {
                var h = lastMapPt.horizontalPosition + Vector2.Distance(lastMapPt.point, r.point);
                map[i - 1] = new GroundMapPt(r.point, r.normal, r.normal.CWPerp(), h, r.distance, r.collider);
                if (!AnyPointsHitGround)
                {
                    AnyPointsHitGround = true;
                }
            }
            else
            {
                //now shift forward and raycast down
                o += intervalWidth * lastTangent;
                r = MathTools.DebugRaycast(o, -lastNormal, raycastLength, raycastLayerMask, RaycastColor2);
                if (r)
                {
                    var h = lastMapPt.horizontalPosition - Vector2.Distance(lastMapPt.point, r.point);
                    map[i - 1] = new GroundMapPt(r.point, r.normal, r.normal.CWPerp(), h, r.distance * Vector2.Dot(lastNormal, r.normal), r.collider);
                    if (!AnyPointsHitGround)
                    {
                        AnyPointsHitGround = true;
                    }
                }
                else
                {
                    var l = Mathf.Min(lastRaycastLength + 0.5f * intervalWidth, raycastLength);
                    r = Physics2D.Raycast(o - l * lastNormal, -lastTangent, 2 * intervalWidth, raycastLayerMask);
                    if (r)
                    {
                        var h = lastMapPt.horizontalPosition - Vector2.Distance(lastMapPt.point, r.point);
                        map[i - 1] = new GroundMapPt(r.point, r.normal, r.normal.CWPerp(), h, r.distance, r.collider);
                        if (!AnyPointsHitGround)
                        {
                            AnyPointsHitGround = true;
                        }
                    }
                    else
                    {
                        var p = o - raycastLength * lastNormal;
                        var h = lastMapPt.horizontalPosition - Vector2.Distance(lastMapPt.point, p);
                        map[i - 1] = new GroundMapPt(p, lastNormal, lastMapPt.right, h, raycastLength, null);
                        if (AllPointsHitGround)
                        {
                            AllPointsHitGround = false;
                        }
                    }
                }
            }
        }
    }

    public void DrawGizmos()
    {
        if (map == null || map.Length == 0) return;

        Gizmos.color = GizmoColorLeft;
        for (int i = 0; i < numFwdIntervals; i++)
        {
            Gizmos.DrawSphere(map[i].point, 0.1f);
        }
        Gizmos.color = GizmoColorCenter;
        Gizmos.DrawSphere(map[numFwdIntervals].point, 0.1f);
        Gizmos.color = GizmoColorRight;
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
    public readonly Vector2 right;
    public readonly float horizontalPosition;
    public readonly float raycastDistance;
    //public readonly int groundColliderId;
    public readonly Collider2D groundCollider;

    public bool hitGround => groundCollider != null;

    public GroundMapPt(Vector2 point, Vector2 normal, Vector2 right, float horizontalPosition, float raycastDistance, Collider2D groundCollider)
    {
        this.point = point;
        this.normal = normal;
        this.right = right;
        this.horizontalPosition = horizontalPosition;
        this.raycastDistance = raycastDistance;
        this.groundCollider = groundCollider;
    }
}