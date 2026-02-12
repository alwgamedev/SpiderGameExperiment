using System;
using UnityEngine;

[Serializable]
public class GroundMap
{
    public int numFwdIntervals;
    public float intervalWidth;
    public GroundMapPt[] map;

    public int CentralIndex => numFwdIntervals;
    public int NumPts => (numFwdIntervals << 1) | 1;
    public float MapHalfWidth => intervalWidth * numFwdIntervals;
    public Vector2 LastOrigin { get; private set; }
    public Vector2 LastOriginRight { get; private set; }
    public ref GroundMapPt Center => ref map[CentralIndex];//NOTE: you will modify the array element through this property! it's not a copy!!
    public ref GroundMapPt LeftEndPt => ref map[0];
    public ref GroundMapPt RightEndPt => ref map[^1];

    Color RaycastColor0 => Color.clear;//Color.red;
    Color RaycastColor1 => Color.clear;//Color.blue;
    Color RaycastColor2 => Color.clear;//Color.cyan;

    Color GizmoColorCenter => Color.red;
    Color GizmoColorRight => Color.green;
    Color GizmoColorLeft => Color.yellow;

    public bool IsCentralIndex(int i)
    {
        return i == numFwdIntervals;
    }

    public bool AllHitGround()
    {
        for (int i = 0; i < map.Length; i++)
        {
            if (!map[i].HitGround)
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
            if (map[i].HitGround)
            {
                return true;
            }
        }

        return false;
    }

    public bool AllHitGroundFromCenter(float maxPosition)
    {
        if (!Center.HitGround)
        {
            return false;
        }

        maxPosition += Mathf.Epsilon;
        var minPosition = -maxPosition - Mathf.Epsilon;
        for (int i = 1; i < numFwdIntervals; i++)
        {
            ref var r = ref map[numFwdIntervals + i];
            if (r.position < maxPosition && !r.HitGround)
            {
                return false;
            }

            ref var l = ref map[numFwdIntervals - i];
            if (l.position > minPosition && l.HitGround)
            {
                return false;
            }

            if (r.position > maxPosition && l.position < minPosition)
            {
                return true;
            }
        }

        return true;
    }

    public bool AnyHitGroundFromCenter(float maxPosition)
    {
        if (Center.HitGround)
        {
            return true;
        }

        maxPosition += Mathf.Epsilon;
        var minPosition = -maxPosition - Mathf.Epsilon;
        for (int i = 1; i < numFwdIntervals; i++)
        {
            ref var r = ref map[numFwdIntervals + i];
            if (r.position < maxPosition && r.HitGround)
            {
                return true;
            }

            ref var l = ref map[numFwdIntervals - i];
            if (l.position > minPosition && l.HitGround)
            {
                return true;
            }

            if (r.position > maxPosition && l.position < minPosition)
            {
                return false;
            }
        }

        return false;
    }

    public int IndexOfFirstGroundHitFromCenter(bool facingRight, out bool isCentralIndex)
    {
        int i = CentralIndex;
        if (map[i].HitGround)
        {
            isCentralIndex = true;
            return i;
        }

        int di = facingRight ? 1 : -1;
        int n = numFwdIntervals << 1;
        int max = facingRight ? n : 0;

        //search forward first (not necessary - we could alternate front and behind until we get a hit - but i feel like this way is better for continuity)
        while (i != max)
        {
            i += di;
            if (map[i].HitGround)
            {
                isCentralIndex = false;
                return i;
            }
        }

        i = CentralIndex;
        di = -di;
        max = facingRight ? 0 : n;

        while (i != max)
        {
            i += di;
            if (map[i].HitGround)
            {
                isCentralIndex = false;
                return i;
            }
        }

        isCentralIndex = true;
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

    public Vector2 ProjectOntoGroundByArcLength(Vector2 p, out Vector2 normal, out bool hitGround, float htFraction = 1, bool onlyUseHtFractionIfNotHitGround = false)
    {
        p = PointFromCenterByPosition(Vector2.Dot(p - LastOrigin, LastOriginRight), out normal, out hitGround);
        if (htFraction < 1 && (!onlyUseHtFractionIfNotHitGround || !hitGround))
        {
            var y = Vector2.Dot(LastOrigin - p, normal);
            if (y > 0)
            {
                p += (1 - htFraction) * y * normal;
            }
        }

        return p;
    }

    public Vector2 PointFromCenterByPosition(float x, out Vector2 normal, out bool hitGround, float htFraction = 1, bool onlyUseHtFractionIfNotHitGround = false)
    {
        var p = PointFromCenterByPosition(x, out normal, out hitGround);
        if (htFraction < 1 && (!onlyUseHtFractionIfNotHitGround || !hitGround))
        {
            var y = Vector2.Dot(LastOrigin - p, normal);
            if (y > 0)
            {
                p += (1 - htFraction) * y * normal;
            }
        }

        return p;
    }

    public Vector2 PointFromCenterByPosition(float x, out Vector2 normal, out bool hitGround)
    {
        if (x > 0)
        {
            for (int i = numFwdIntervals; i < map.Length - 1; i++)
            {
                ref var p = ref map[i + 1];
                if (p.position > x)
                {
                    ref var q = ref map[i];
                    var s = p.position - q.position;
                    normal = q.normal;
                    hitGround = q.HitGround;
                    return Vector2.Lerp(q.point, p.point, (x - q.position) / s);
                }
            }

            var t = x - RightEndPt.position;
            normal = RightEndPt.normal;
            hitGround = RightEndPt.HitGround;
            return RightEndPt.point + t * RightEndPt.normal.CWPerp();
        }

        for (int i = numFwdIntervals; i > 0; i--)
        {
            ref var p = ref map[i - 1];
            if (p.position < x)
            {
                ref var q = ref map[i];
                var s = p.position - q.position;
                normal = q.normal;
                hitGround = q.HitGround;
                return Vector2.Lerp(q.point, p.point, (x - q.position) / s);
            }
        }

        var u = x - LeftEndPt.position;
        normal = LeftEndPt.normal;
        hitGround = LeftEndPt.HitGround;
        return LeftEndPt.point + u * LeftEndPt.normal.CWPerp();
    }

    public void SetToPointFromCenterByPositionClamped(float x, ref GroundMapPt pt)
    {
        if (x > 0)
        {
            for (int i = numFwdIntervals; i < map.Length - 1; i++)
            {
                if (map[i + 1].position > x)
                {
                    var s = map[i + 1].position - map[i].position;
                    pt.Set(Vector2.Lerp(map[i].point, map[i + 1].point, (x - map[i].position) / s), map[i].normal, map[i].right, 0, 0, map[i].groundCollider);
                }
            }
            pt = RightEndPt;
        }

        for (int i = numFwdIntervals; i > 0; i--)
        {
            if (map[i - 1].position < x)
            {
                var s = map[i - 1].position - map[i].position;
                pt.Set(Vector2.Lerp(map[i].point, map[i - 1].point, (x - map[i].position) / s), map[i].normal, map[i].right, 0, 0, map[i].groundCollider);
            }
        }
        pt = LeftEndPt;
    }

    public Vector2 ClosestPoint(Vector2 p, out Vector2 normal, out bool hitGround)
    {
        var a1 = MathTools.Cross2D(p - map[0].point, map[0].normal);
        if (a1 == 0)
        {
            hitGround = map[0].HitGround;
            normal = map[0].normal;
            return map[0].point;
        }
        for (int i = 1; i < NumPts; i++)
        {
            var a2 = MathTools.Cross2D(p - map[i].point, map[i].normal);
            if (a2 == 0)
            {
                hitGround = map[i].HitGround;
                normal = map[i].normal;
                return map[i].point;
            }
            if (MathTools.OppositeSigns(a1, a2))
            {
                var t = Mathf.Abs(a1 / (a2 - a1));
                hitGround = t < 0.5f ? map[i - 1].HitGround : map[i].HitGround;
                normal = MathTools.CheapRotationalLerp(map[i - 1].normal, map[i].normal, t, out _);
                return Vector2.Lerp(map[i - 1].point, map[i].point, t);
            }
            a1 = a2;
        }

        if (Vector2.Dot(p - RightEndPt.point, RightEndPt.right) > 0)
        {
            hitGround = RightEndPt.HitGround;
            normal = RightEndPt.normal;
            return RightEndPt.point;
        }

        hitGround = LeftEndPt.HitGround;
        normal = LeftEndPt.normal;
        return LeftEndPt.point;
    }

    public Vector2 AveragePointFromCenter(float minPos, float maxPos)
    {
        maxPos += Mathf.Epsilon;//so we can use < instead of <=
        minPos -= Mathf.Epsilon;
        Vector2 sum = default;
        int ct = 0;

        for (int i = 0; i < map.Length; i++)
        {
            if (map[i].position < maxPos && map[i].position > minPos)
            {
                sum += map[i].point;
                ct++;
            }
        }

        return ct == 0 ? sum : sum / ct;
    }

    public Vector2 AverageNormalFromCenter(float minPos, float maxPos)
    {
        maxPos += Mathf.Epsilon;//so we can use < instead of <=
        minPos -= Mathf.Epsilon;
        Vector2 sum = default;
        int ct = 0;

        for (int i = 0; i < map.Length; i++)
        {
            if (map[i].position < maxPos && map[i].position > minPos)
            {
                sum += map[i].normal;
                ct++;
            }
        }

        return ct == 0 ? sum : sum / ct;
    }

    public void Initialize()
    {
        map = new GroundMapPt[NumPts];
    }

    public void UpdateMap(Vector2 origin, Vector2 originDown, Vector2 originRight, float raycastLength, int centralIndex, int raycastLayerMask)
    {
        int n = numFwdIntervals << 1;//last index of map array
        LastOrigin = origin;
        LastOriginRight = originRight;

        //var r = MathTools.DebugRaycast(origin, originDown, raycastLength, raycastLayerMask, RaycastColor0);
        var r = Physics2D.Raycast(origin, originDown, raycastLength, raycastLayerMask);
        if (r)
        {
            map[centralIndex].Set(r.point, r.normal, r.normal.CWPerp(), 0, r.distance, r.collider);
        }
        else
        {
            map[centralIndex].Set(origin + raycastLength * originDown, -originDown, originRight, 0, raycastLength, null);
        }

        for (int i = centralIndex; i < n; i++)
        {
            //set map[i + 1]
            ref var lastMapPt = ref map[i];
            var lastNormal = lastMapPt.normal;
            var lastRight = lastMapPt.right;
            var lastRaycastLength = lastMapPt.raycastDistance;
            var o = lastMapPt.point + lastRaycastLength * lastNormal;

            //first raycast horizontally to check if ground kicks UP suddenly (running into a wall e.g.)
            //r = MathTools.DebugRaycast(o, lastRight, intervalWidth, raycastLayerMask, RaycastColor1);
            r = Physics2D.Raycast(o, lastRight, intervalWidth, raycastLayerMask);
            if (r)
            {
                map[i + 1].Set(r.point, r.normal, r.normal.CWPerp(), lastMapPt.position + Vector2.Distance(lastMapPt.point, r.point),
                    r.distance, r.collider);
            }
            else
            {
                //now shift forward and raycast down
                o += intervalWidth * lastRight;
                //r = MathTools.DebugRaycast(o, -lastNormal, raycastLength, raycastLayerMask, RaycastColor2);
                r = Physics2D.Raycast(o, -lastNormal, raycastLength, raycastLayerMask);
                if (r)
                {
                    map[i + 1].Set(r.point, r.normal, r.normal.CWPerp(), lastMapPt.position + Vector2.Distance(lastMapPt.point, r.point),
                        r.distance * Vector2.Dot(lastNormal, r.normal), r.collider);
                }
                else
                {
                    var l = Mathf.Min(lastRaycastLength + 0.5f * intervalWidth, raycastLength);
                    r = Physics2D.Raycast(o - l * lastNormal, -lastRight, 2 * intervalWidth, raycastLayerMask);
                    if (r)
                    {
                        map[i + 1].Set(r.point, r.normal, r.normal.CWPerp(), lastMapPt.position + Vector2.Distance(lastMapPt.point, r.point), r.distance, r.collider);
                    }
                    else
                    {
                        o -= raycastLength * lastNormal;
                        map[i + 1].Set(o, lastNormal, lastMapPt.right, lastMapPt.position + Vector2.Distance(lastMapPt.point, o), raycastLength, null);
                    }
                }
            }
        }

        for (int i = centralIndex; i > 0; i--)
        {
            //set map[i - 1]
            ref var lastMapPt = ref map[i];
            var lastNormal = lastMapPt.normal;
            var lastTangent = -lastMapPt.right;
            var lastRaycastLength = lastMapPt.raycastDistance;
            var o = lastMapPt.point + lastRaycastLength * lastNormal;

            //r = MathTools.DebugRaycast(o, lastTangent, intervalWidth, raycastLayerMask, RaycastColor1);
            r = Physics2D.Raycast(o, lastTangent, intervalWidth, raycastLayerMask);
            if (r)
            {
                map[i - 1].Set(r.point, r.normal, r.normal.CWPerp(), lastMapPt.position - Vector2.Distance(lastMapPt.point, r.point), r.distance, r.collider);
            }
            else
            {
                //now shift forward and raycast down
                o += intervalWidth * lastTangent;
                //r = MathTools.DebugRaycast(o, -lastNormal, raycastLength, raycastLayerMask, RaycastColor2);
                r = Physics2D.Raycast(o, -lastNormal, raycastLength, raycastLayerMask);
                if (r)
                {
                    map[i - 1].Set(r.point, r.normal, r.normal.CWPerp(), lastMapPt.position - Vector2.Distance(lastMapPt.point, r.point),
                        r.distance * Vector2.Dot(lastNormal, r.normal), r.collider);
                }
                else
                {
                    var l = Mathf.Min(lastRaycastLength + 0.5f * intervalWidth, raycastLength);
                    r = Physics2D.Raycast(o - l * lastNormal, -lastTangent, 2 * intervalWidth, raycastLayerMask);
                    if (r)
                    {
                        map[i - 1].Set(r.point, r.normal, r.normal.CWPerp(), lastMapPt.position - Vector2.Distance(lastMapPt.point, r.point),
                            r.distance, r.collider);
                    }
                    else
                    {
                        o -= raycastLength * lastNormal;
                        map[i - 1].Set(o, lastNormal, lastMapPt.right, lastMapPt.position - Vector2.Distance(lastMapPt.point, o), raycastLength, null);
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
public struct GroundMapPt
{
    public float position;
    public float raycastDistance;
    public Vector2 point;
    public Vector2 normal;
    public Vector2 right;
    public Collider2D groundCollider;

    public bool HitGround => groundCollider != null;

    public GroundMapPt(Vector2 point, Vector2 normal, Vector2 right, float position, float raycastDistance, Collider2D groundCollider)
    {
        this.point = point;
        this.normal = normal;
        this.right = right;
        this.position = position;
        this.raycastDistance = raycastDistance;
        this.groundCollider = groundCollider;
    }

    public void Set(Vector2 point, Vector2 normal, Vector2 right, float position, float raycastDistance, Collider2D groundCollider)
    {
        this.point = point;
        this.normal = normal;
        this.right = right;
        this.position = position;
        this.raycastDistance = raycastDistance;
        this.groundCollider = groundCollider;
    }
}