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
            if (r.arcLengthPosition < maxPosition && !r.HitGround)
            {
                return false;
            }

            ref var l = ref map[numFwdIntervals - i];
            if (l.arcLengthPosition > minPosition && l.HitGround)
            {
                return false;
            }

            if (r.arcLengthPosition > maxPosition && l.arcLengthPosition < minPosition)
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
            if (r.arcLengthPosition < maxPosition && r.HitGround)
            {
                return true;
            }

            ref var l = ref map[numFwdIntervals - i];
            if (l.arcLengthPosition > minPosition && l.HitGround)
            {
                return true;
            }

            if (r.arcLengthPosition > maxPosition && l.arcLengthPosition < minPosition)
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

    public Vector2 PointFromCenterByIntervalWidth(float x)
    {
        var i = (int)(x / intervalWidth);
        if (!(i < numFwdIntervals))
        {
            return RightEndPt.point;
        }
        if (!(i > -numFwdIntervals))
        {
            return LeftEndPt.point;
        }

        i += numFwdIntervals;
        var x0 = i * intervalWidth;
        var x1 = x - x0;
        return x1 > 0 ? Vector2.Lerp(map[i].point, map[i + 1].point, x1 / intervalWidth) : Vector2.Lerp(map[i].point, map[i - 1].point, - x1 / intervalWidth);
    }

    public Vector2 ProjectOntoGroundByArcLength(Vector2 p, out Vector2 normal, out bool hitGround, float htFraction = 1, bool onlyUseHtFractionIfNotHitGround = false)
    {
        p = PointFromCenterByArcLength(Vector2.Dot(p - LastOrigin, LastOriginRight), out normal, out hitGround);
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

    public Vector2 PointFromCenterByArcLength(float x, out Vector2 normal, out bool hitGround, float htFraction, bool onlyUseHtFractionIfNotHitGround)
    {
        var p = PointFromCenterByArcLength(x, out normal, out hitGround);
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

    public Vector2 PointFromCenterByArcLength(float x, out Vector2 normal, out bool hitGround)
    {
        if (x > 0)
        {
            for (int i = numFwdIntervals; i < map.Length - 1; i++)
            {
                ref var p = ref map[i + 1];
                if (p.arcLengthPosition > x)
                {
                    ref var q = ref map[i];
                    var s = p.arcLengthPosition - q.arcLengthPosition;
                    normal = q.normal;
                    hitGround = q.HitGround;
                    return Vector2.Lerp(q.point, p.point, (x - q.arcLengthPosition) / s);
                }
            }

            var t = x - RightEndPt.arcLengthPosition;
            normal = RightEndPt.normal;
            hitGround = RightEndPt.HitGround;
            return RightEndPt.point + t * RightEndPt.normal.CWPerp();
        }

        for (int i = numFwdIntervals; i > 0; i--)
        {
            ref var p = ref map[i - 1];
            if (p.arcLengthPosition < x)
            {
                ref var q = ref map[i];
                var s = p.arcLengthPosition - q.arcLengthPosition;
                normal = q.normal;
                hitGround = q.HitGround;
                return Vector2.Lerp(q.point, p.point, (x - q.arcLengthPosition) / s);
            }
        }

        var u = x - LeftEndPt.arcLengthPosition;
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
                if (map[i + 1].arcLengthPosition > x)
                {
                    var s = map[i + 1].arcLengthPosition - map[i].arcLengthPosition;
                    pt.Set(Vector2.Lerp(map[i].point, map[i + 1].point, (x - map[i].arcLengthPosition) / s), map[i].normal, map[i].right, 0, 0, map[i].groundCollider);
                }
            }
            pt = RightEndPt;
        }

        for (int i = numFwdIntervals; i > 0; i--)
        {
            if (map[i - 1].arcLengthPosition < x)
            {
                var s = map[i - 1].arcLengthPosition - map[i].arcLengthPosition;
                pt.Set(Vector2.Lerp(map[i].point, map[i - 1].point, (x - map[i].arcLengthPosition) / s), map[i].normal, map[i].right, 0, 0, map[i].groundCollider);
            }
        }
        pt = LeftEndPt;
    }

    public Vector2 TrueClosestPoint(Vector2 p, out float arcLengthPosition, out Vector2 normal, out bool hitGround)
    {
        var bestIndex = -1;
        var bestDistSqrd = Mathf.Infinity;

        for (int i = 0; i < map.Length; i++)
        {
            var d2 = (p - map[i].point).sqrMagnitude;
            if (d2 < bestDistSqrd)
            {
                bestIndex = i;
                bestDistSqrd = d2;
            }
        }

        var q = map[bestIndex].point;
        arcLengthPosition = map[bestIndex].arcLengthPosition;
        var q1 = q;
        var v = p - q;
        normal = map[bestIndex].normal;
        hitGround = map[bestIndex].HitGround;

        //see if a point on next or prev segments is closer
        if (bestIndex < map.Length - 1)
        {
            var w = map[bestIndex + 1].point - q;
            var dot = Vector2.Dot(v, w);
            if (dot > 0 && dot < w.sqrMagnitude)
            {
                var t = dot / w.magnitude;
                q1 = Vector2.Lerp(q, map[bestIndex + 1].point, t);
                arcLengthPosition = Mathf.Lerp(arcLengthPosition, map[bestIndex + 1].arcLengthPosition, t);
                bestDistSqrd = (p - q1).sqrMagnitude;
                normal = MathTools.CheapRotationalLerp(map[bestIndex].normal, map[bestIndex + 1].normal, t, out _);
            }
        }

        if (bestIndex > 0)
        {
            var w = map[bestIndex - 1].point - q;
            var dot = Vector2.Dot(v, w);
            if (dot > 0 && dot < w.sqrMagnitude)
            {
                var t = dot / w.magnitude;
                var q2 = Vector2.Lerp(q, map[bestIndex - 1].point, t);
                var dist2 = (p - q2).sqrMagnitude;
                if (dist2 < bestDistSqrd)
                {
                    q1 = q2;
                    arcLengthPosition = Mathf.Lerp(arcLengthPosition, map[bestIndex - 1].arcLengthPosition, t);
                    normal = MathTools.CheapRotationalLerp(map[bestIndex].normal, map[bestIndex - 1].normal, t, out _);
                }
            }
        }

        return q1;
    }

    //finds first local min in distance and returns that point
    public Vector2 FastClosestPoint(Vector2 p, out Vector2 normal, out bool hitGround)
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
            if (map[i].arcLengthPosition < maxPos && map[i].arcLengthPosition > minPos)
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
            if (map[i].arcLengthPosition < maxPos && map[i].arcLengthPosition > minPos)
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

    public void UpdateMap(Vector2 origin, Vector2 originDown, Vector2 originRight, float raycastLength, int raycastLayerMask)
    {
        int n = numFwdIntervals << 1;//last index of map array
        LastOrigin = origin;
        LastOriginRight = originRight;

        //var r = MathTools.DebugRaycast(origin, originDown, raycastLength, raycastLayerMask, RaycastColor0);
        var r = Physics2D.Raycast(origin, originDown, raycastLength, raycastLayerMask);
        if (r)
        {
            map[CentralIndex].Set(r.point, r.normal, r.normal.CWPerp(), 0, r.distance, r.collider);
        }
        else
        {
            map[CentralIndex].Set(origin + raycastLength * originDown, -originDown, originRight, 0, raycastLength, null);
        }

        for (int i = CentralIndex; i < n; i++)
        {
            //set map[i + 1]
            ref var lastMapPt = ref map[i];
            var o = lastMapPt.point + lastMapPt.raycastDistance * lastMapPt.normal;//lastRaycastLength * lastNormal;

            //first raycast horizontally to check if ground kicks UP suddenly (running into a wall e.g.)
            //r = MathTools.DebugRaycast(o, lastRight, intervalWidth, raycastLayerMask, RaycastColor1);
            r = Physics2D.Raycast(lastMapPt.point + intervalWidth * lastMapPt.normal, lastMapPt.right, 2 * intervalWidth, raycastLayerMask);

            if (r)
            {
                map[i + 1].Set(r.point, r.normal, r.normal.CWPerp(), lastMapPt.arcLengthPosition + Vector2.Distance(lastMapPt.point, r.point),
                    r.distance, r.collider);
                //Debug.DrawLine(map[i].point, map[i + 1].point, Color.red);
                //Debug.DrawLine(map[i + 1].point, map[i + 1].point + 0.25f * map[i + 1].normal, Color.red);
            }
            else
            {
                //now shift forward and raycast down
                o += intervalWidth * lastMapPt.right;
                //r = MathTools.DebugRaycast(o, -lastNormal, raycastLength, raycastLayerMask, RaycastColor2);
                r = Physics2D.Raycast(o, -lastMapPt.normal, raycastLength, raycastLayerMask);

                if (r && r.distance == 0)
                {
                    var y = intervalWidth;
                    while (y < lastMapPt.raycastDistance)
                    {
                        r = Physics2D.Raycast(o - y * lastMapPt.normal, -lastMapPt.normal, raycastLength, raycastLayerMask);
                        if (!r || r.distance > 0)
                        {
                            break;
                        }

                        y += intervalWidth;
                    }
                }

                if (r)
                {
                    var l = Vector2.Distance(lastMapPt.point, r.point);
                    if (intervalWidth < l * MathTools.sin15)//if l > ~3.86 * intervalWidth...
                    {
                        var u = (r.point - lastMapPt.point) / l;
                        r = Physics2D.Raycast(lastMapPt.point + 0.25f * intervalWidth * u, u, 0.75f * intervalWidth, raycastLayerMask);
                        if (r)
                        {
                            r.distance += 0.25f * intervalWidth;
                            var raycastDistance = (lastMapPt.raycastDistance - Vector2.Dot(r.point - lastMapPt.point, lastMapPt.normal)) * Vector2.Dot(lastMapPt.normal, r.normal);
                            map[i + 1].Set(r.point, r.normal, r.normal.CWPerp(), lastMapPt.arcLengthPosition + r.distance,
                                raycastDistance, r.collider);
                        }
                        else
                        {
                            var raycastDistance = (lastMapPt.raycastDistance - intervalWidth * Vector2.Dot(u, lastMapPt.normal)) * Vector2.Dot(u.CCWPerp(), lastMapPt.normal);
                            map[i + 1].Set(lastMapPt.point + intervalWidth * u, u.CCWPerp(), u, lastMapPt.arcLengthPosition + intervalWidth,
                                raycastDistance, null);
                        }
                    }
                    else
                    {
                        map[i + 1].Set(r.point, r.normal, r.normal.CWPerp(), lastMapPt.arcLengthPosition + l,
                        r.distance * Vector2.Dot(lastMapPt.normal, r.normal), r.collider);
                    }
                    //if (r.distance == 0)
                    //{
                    //    Debug.Log("vert hit with no distance");
                    //}
                    //map[i + 1].Set(r.point, r.normal, r.normal.CWPerp(), lastMapPt.arcLengthPosition + Vector2.Distance(lastMapPt.point, r.point),
                    //    r.distance * Vector2.Dot(lastMapPt.normal, r.normal), r.collider);
                }
                else
                {
                    var l = Mathf.Min(lastMapPt.raycastDistance + 0.5f * intervalWidth, raycastLength);
                    r = Physics2D.Raycast(o - l * lastMapPt.normal, -lastMapPt.right, 2 * intervalWidth, raycastLayerMask);
                    if (r)
                    {
                        map[i + 1].Set(r.point, r.normal, r.normal.CWPerp(), lastMapPt.arcLengthPosition + Vector2.Distance(lastMapPt.point, r.point), r.distance, r.collider);
                    }
                    else
                    {
                        o -= raycastLength * lastMapPt.normal;
                        map[i + 1].Set(o, lastMapPt.normal, lastMapPt.right, lastMapPt.arcLengthPosition + Vector2.Distance(lastMapPt.point, o), raycastLength, null);
                    }
                }
            }
        }

        for (int i = CentralIndex; i > 0; i--)
        {
            //set map[i - 1]
            ref var lastMapPt = ref map[i];
            var o = lastMapPt.point + lastMapPt.raycastDistance * lastMapPt.normal;//lastRaycastLength * lastNormal;

            //r = MathTools.DebugRaycast(o, lastTangent, intervalWidth, raycastLayerMask, RaycastColor1);
            r = Physics2D.Raycast(lastMapPt.point + intervalWidth * lastMapPt.normal, -lastMapPt.right, 2 * intervalWidth, raycastLayerMask);
            if (r)
            {
                map[i - 1].Set(r.point, r.normal, r.normal.CWPerp(), lastMapPt.arcLengthPosition - Vector2.Distance(lastMapPt.point, r.point), r.distance, r.collider);
            }
            else
            {
                //now shift forward and raycast down
                o -= intervalWidth * lastMapPt.right;
                //r = MathTools.DebugRaycast(o, -lastNormal, raycastLength, raycastLayerMask, RaycastColor2);
                r = Physics2D.Raycast(o, -lastMapPt.normal, raycastLength, raycastLayerMask); 
                
                if (r && r.distance == 0)
                {
                    var y = intervalWidth;
                    while (y < lastMapPt.raycastDistance)
                    {
                        r = Physics2D.Raycast(o - y * lastMapPt.normal, -lastMapPt.normal, raycastLength, raycastLayerMask);
                        if (!r || r.distance > 0)
                        {
                            break;
                        }

                        y += intervalWidth;
                    }
                }

                if (r)
                {
                    var l = Vector2.Distance(lastMapPt.point, r.point);
                    if (intervalWidth < l * MathTools.sin15)//if l > ~3.86 * intervalWidth...
                    {
                        var u = (r.point - lastMapPt.point) / l;
                        r = Physics2D.Raycast(lastMapPt.point + 0.25f * intervalWidth * u, u, 0.75f * intervalWidth, raycastLayerMask);
                        if (r)
                        {
                            r.distance += 0.25f * intervalWidth;
                            var raycastDistance = (lastMapPt.raycastDistance - Vector2.Dot(r.point - lastMapPt.point, lastMapPt.normal)) * Vector2.Dot(lastMapPt.normal, r.normal);
                            map[i - 1].Set(r.point, r.normal, r.normal.CWPerp(), lastMapPt.arcLengthPosition - r.distance,
                                raycastDistance, r.collider);
                        }
                        else
                        {
                            var raycastDistance = (lastMapPt.raycastDistance - intervalWidth * Vector2.Dot(u.CWPerp(), lastMapPt.normal)) * Vector2.Dot(u.CCWPerp(), lastMapPt.normal);
                            map[i - 1].Set(lastMapPt.point + intervalWidth * u, u.CWPerp(), -u, lastMapPt.arcLengthPosition - intervalWidth,
                                raycastDistance, null);
                        }
                    }
                    else
                    {
                        map[i - 1].Set(r.point, r.normal, r.normal.CWPerp(), lastMapPt.arcLengthPosition - Vector2.Distance(lastMapPt.point, r.point),
                            r.distance * Vector2.Dot(lastMapPt.normal, r.normal), r.collider);
                    }
                    //map[i - 1].Set(r.point, r.normal, r.normal.CWPerp(), lastMapPt.arcLengthPosition - Vector2.Distance(lastMapPt.point, r.point),
                    //    r.distance * Vector2.Dot(lastMapPt.normal, r.normal), r.collider);
                }
                else
                {
                    var l = Mathf.Min(lastMapPt.raycastDistance + 0.5f * intervalWidth, raycastLength);
                    r = Physics2D.Raycast(o - l * lastMapPt.normal, lastMapPt.right, 2 * intervalWidth, raycastLayerMask);
                    if (r)
                    {
                        map[i - 1].Set(r.point, r.normal, r.normal.CWPerp(), lastMapPt.arcLengthPosition - Vector2.Distance(lastMapPt.point, r.point),
                            r.distance, r.collider);
                    }
                    else
                    {
                        o -= raycastLength * lastMapPt.normal;
                        map[i - 1].Set(o, lastMapPt.normal, lastMapPt.right, lastMapPt.arcLengthPosition - Vector2.Distance(lastMapPt.point, o), raycastLength, null);
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
    public float arcLengthPosition;
    public float raycastDistance;
    public Vector2 point;
    public Vector2 normal;
    public Vector2 right;
    public Collider2D groundCollider;

    public bool HitGround => groundCollider != null;

    public GroundMapPt(Vector2 point, Vector2 normal, Vector2 right, float arcLengthPosition, float raycastDistance, Collider2D groundCollider)
    {
        this.point = point;
        this.normal = normal;
        this.right = right;
        this.arcLengthPosition = arcLengthPosition;
        this.raycastDistance = raycastDistance;
        this.groundCollider = groundCollider;
    }

    public void Set(Vector2 point, Vector2 normal, Vector2 right, float arcLengthPosition, float raycastDistance, Collider2D groundCollider)
    {
        this.point = point;
        this.normal = normal;
        this.right = right;
        this.arcLengthPosition = arcLengthPosition;
        this.raycastDistance = raycastDistance;
        this.groundCollider = groundCollider;
    }
}