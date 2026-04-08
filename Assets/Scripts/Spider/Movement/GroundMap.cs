using System;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using Unity.U2D.Physics;
using UnityEngine;

[Serializable]
public class GroundMap
{
    [SerializeField] int numFwdIntervals;
    [SerializeField] float intervalWidth;

    //bool swap1Public;
    NativeArray<GroundMapPt> map1;
    //NativeArray<GroundMapPt> map2;
    NativeReference<int> indexOfFirstGroundHitFromCenter1;
    //NativeReference<int> indexOfFirstGroundHitFromCenter2;
    //JobHandle jobHandle;

    //Vector2 lastJobOrigin;
    //Vector2 lastJobOriginRight;

    public GroundMapPt MapPoint(int i) => map1[i];//swap1Public ? map1[i] : map2[i];
    //^get rid of this and make map public now that we're running synchronously

    public int CentralIndex { get; private set; }
    public int NumPoints { get; private set; }
    public int IndexOfFirstGroundHitFromCenter => indexOfFirstGroundHitFromCenter1.Value;//swap1Public ? indexOfFirstGroundHitFromCenter1.Value : indexOfFirstGroundHitFromCenter2.Value;
    public float MapHalfWidth => intervalWidth * numFwdIntervals;
    public float2 Origin { get; private set; }
    public float2 OriginRight { get; private set; }
    public GroundMapPt Center => MapPoint(CentralIndex);//NOTE: you will modify the array element through this property! it's not a copy!!
    public GroundMapPt LeftEndPt => MapPoint(0);
    public GroundMapPt RightEndPt => MapPoint(NumPoints - 1);

    Color RaycastColor0 => Color.clear;//Color.red;
    Color RaycastColor1 => Color.clear;//Color.blue;
    Color RaycastColor2 => Color.clear;//Color.cyan;

    Color GizmoColorCenter => Color.red;
    Color GizmoColorRight => Color.green;
    Color GizmoColorLeft => Color.yellow;

    public float2 ProjectOntoGroundByArcLength(float2 p, out float2 normal, out bool hitGround, float htFraction = 1, bool onlyUseHtFractionIfNotHitGround = false)
    {
        p = PointFromCenterByArcLength(Vector2.Dot(p - Origin, OriginRight), out normal, out hitGround);
        if (htFraction < 1 && (!onlyUseHtFractionIfNotHitGround || !hitGround))
        {
            var y = math.dot(Origin - p, normal);
            if (y > 0)
            {
                p += (1 - htFraction) * y * normal;
            }
        }

        return p;
    }

    public float2 PointFromCenterByArcLength(float x, out float2 normal, out bool hitGround)
    {
        if (x > 0)
        {
            for (int i = numFwdIntervals; i < NumPoints - 1; i++)
            {
                var p = MapPoint(i + 1);
                if (p.arcLengthPosition > x)
                {
                    var q = MapPoint(i);
                    var s = p.arcLengthPosition - q.arcLengthPosition;
                    normal = q.normal;
                    hitGround = q.hitGround;
                    return math.lerp(q.point, p.point, (x - q.arcLengthPosition) / s);
                }
            }

            var t = x - RightEndPt.arcLengthPosition;
            normal = RightEndPt.normal;
            hitGround = RightEndPt.hitGround;
            return RightEndPt.point + t * RightEndPt.normal.CWPerp();
        }

        for (int i = numFwdIntervals; i > 0; i--)
        {
            var p = MapPoint(i - 1);
            if (p.arcLengthPosition < x)
            {
                var q = MapPoint(i);
                var s = p.arcLengthPosition - q.arcLengthPosition;
                normal = q.normal;
                hitGround = q.hitGround;
                return math.lerp(q.point, p.point, (x - q.arcLengthPosition) / s);
            }
        }

        var u = x - LeftEndPt.arcLengthPosition;
        normal = LeftEndPt.normal;
        hitGround = LeftEndPt.hitGround;
        return LeftEndPt.point + u * LeftEndPt.normal.CWPerp();
    }

    //find point q on the ground such that p - q parallel to line cast direction
    public bool LineCastToGround(float2 p, float2 dir, out GroundMapPt hit)
    {
        var a1 = MathTools.Cross2D(p - MapPoint(0).point, dir);
        if (a1 == 0)
        {
            hit = MapPoint(0);
            return true;
        }
        for (int i = 1; i < NumPoints; i++)
        {
            var a2 = MathTools.Cross2D(p - MapPoint(i).point, dir);
            if (a2 == 0)
            {
                if (math.dot(MapPoint(i).normal, dir) < 0)
                {
                    hit = MapPoint(i);
                    return true;
                }
            }
            else if (MathTools.OppositeSigns(a1, a2))
            {
                var t =  math.abs(a1 / (a2 - a1));
                var q0 = MapPoint(i - 1);
                var q1 = MapPoint(i);
                var normal = MathTools.CheapRotationalLerp(q0.normal, q1.normal, t, out _);
                if (Vector2.Dot(normal, dir) < 0)
                {
                    hit = new()
                    {
                        point = math.lerp(q0.point, q1.point, t),
                        normal = normal,
                        hitGround = t < 0.5f ? q0.hitGround : q1.hitGround,
                        arcLengthPosition = math.lerp(q0.arcLengthPosition, q1.arcLengthPosition, t)
                    };
                    return true;
                }
            }
            a1 = a2;
        }

        hit = default;
        return false;
    }

    public float2 TrueClosestPoint(float2 p, out float arcLengthPosition, out float2 normal, out bool hitGround)
    {
        var bestIndex = -1;
        var bestDistSqrd = Mathf.Infinity;

        for (int i = 0; i < NumPoints; i++)
        {
            var d2 = math.lengthsq(p - MapPoint(i).point);
            if (d2 < bestDistSqrd)
            {
                bestIndex = i;
                bestDistSqrd = d2;
            }
        }

        var cur = MapPoint(bestIndex);
        var q = cur.point;
        arcLengthPosition = cur.arcLengthPosition;
        var q1 = q;
        var v = p - q;
        normal = cur.normal;
        hitGround = cur.hitGround;

        //see if a point on next or prev segments is closer
        if (bestIndex < NumPoints - 1)
        {
            var w = MapPoint(bestIndex + 1).point - q;
            var dot = math.dot(v, w);
            if (dot > 0 && dot < math.lengthsq(w))
            {
                var next = MapPoint(bestIndex + 1);
                var t = dot / math.length(w);
                q1 = math.lerp(q, next.point, t);
                arcLengthPosition = math.lerp(arcLengthPosition, next.arcLengthPosition, t);
                bestDistSqrd = math.lengthsq(p - q1);
                normal = MathTools.CheapRotationalLerp(cur.normal, next.normal, t, out _);
            }
        }

        if (bestIndex > 0)
        {
            var prev = MapPoint(bestIndex - 1);
            var w = prev.point - q;
            var dot = math.dot(v, w);
            if (dot > 0 && dot < math.lengthsq(w))
            {
                var t = dot / math.length(w);
                var q2 = math.lerp(q, prev.point, t);
                var dist2 = math.lengthsq(p - q2);
                if (dist2 < bestDistSqrd)
                {
                    q1 = q2;
                    arcLengthPosition = math.lerp(arcLengthPosition, prev.arcLengthPosition, t);
                    normal = MathTools.CheapRotationalLerp(cur.normal, prev.normal, t, out _);
                }
            }
        }

        return q1;
    }

    //finds first local min in distance and returns that point
    public float2 FastClosestPoint(float2 p, out float2 normal, out bool hitGround)
    {
        var mapPt = MapPoint(0);
        var a1 = MathTools.Cross2D(p - mapPt.point, mapPt.normal);
        if (a1 == 0)
        {
            hitGround = mapPt.hitGround;
            normal = mapPt.normal;
            return mapPt.point;
        }
        for (int i = 1; i < NumPoints; i++)
        {
            mapPt = MapPoint(i);
            var a2 = MathTools.Cross2D(p - mapPt.point, mapPt.normal);
            if (a2 == 0)
            {
                hitGround = mapPt.hitGround;
                normal = mapPt.normal;
                return mapPt.point;
            }
            if (MathTools.OppositeSigns(a1, a2))
            {
                var prev = MapPoint(i - 1);
                var t = math.abs(a1 / (a2 - a1));
                hitGround = t < 0.5f ? prev.hitGround : mapPt.hitGround;
                normal = MathTools.CheapRotationalLerp(prev.normal, mapPt.normal, t, out _);
                return math.lerp(prev.point, mapPt.point, t);
            }
            a1 = a2;
        }

        if (math.dot(p - RightEndPt.point, RightEndPt.Right) > 0)
        {
            hitGround = RightEndPt.hitGround;
            normal = RightEndPt.normal;
            return RightEndPt.point;
        }

        hitGround = LeftEndPt.hitGround;
        normal = LeftEndPt.normal;
        return LeftEndPt.point;
    }

    public float2 AveragePointFromCenter(float minPos, float maxPos)
    {
        maxPos += Mathf.Epsilon;//so we can use < instead of <=
        minPos -= Mathf.Epsilon;
        float2 sum = 0;
        int ct = 0;

        for (int i = 0; i < NumPoints; i++)
        {
            var p = MapPoint(i);
            if (p.arcLengthPosition < maxPos && p.arcLengthPosition > minPos)
            {
                sum += p.point;
                ct++;
            }
        }

        return ct == 0 ? sum : sum / ct;
    }

    public float2 AverageNormalFromCenter(float minPos, float maxPos)
    {
        maxPos += Mathf.Epsilon;//so we can use < instead of <=
        minPos -= Mathf.Epsilon;
        float2 sum = 0;
        int ct = 0;

        for (int i = 0; i < NumPoints; i++)
        {
            var p = MapPoint(i);
            if (p.arcLengthPosition < maxPos && p.arcLengthPosition > minPos)
            {
                sum += p.normal;
                ct++;
            }
        }

        return ct == 0 ? sum : sum / ct;
    }

    public void Initialize()
    {
        NumPoints = 2 * numFwdIntervals + 1;
        CentralIndex = numFwdIntervals;
        map1 = new NativeArray<GroundMapPt>(NumPoints, Allocator.Persistent);
        //map2 = new NativeArray<GroundMapPt>(NumPoints, Allocator.Persistent);
        indexOfFirstGroundHitFromCenter1 = new NativeReference<int>(CentralIndex, Allocator.Persistent);
        //indexOfFirstGroundHitFromCenter2 = new NativeReference<int>(CentralIndex, Allocator.Persistent);
    }

    public void Dispose()
    {
        //jobHandle.Complete();

        if (map1.IsCreated)
        {
            map1.Dispose();
        }
        //if (map2.IsCreated)
        //{
        //    map2.Dispose();
        //}
        if (indexOfFirstGroundHitFromCenter1.IsCreated)
        {
            indexOfFirstGroundHitFromCenter1.Dispose();
        }
        //if (indexOfFirstGroundHitFromCenter2.IsCreated)
        //{
        //    indexOfFirstGroundHitFromCenter2.Dispose();
        //}
    }

    //public void UpdateMapImmediate(PhysicsWorld world, PhysicsQuery.QueryFilter filter, Vector2 origin, Vector2 originDown, Vector2 originRight, float raycastLength, bool searchRightFirst)
    //{
    //    if (jobHandle.IsCompleted)
    //    {
    //        jobHandle.Complete();

    //        swap1Public = !swap1Public;

    //        var job = new GroundMapUpdate()
    //        {
    //            map = swap1Public ? map2 : map1,
    //            indexOfFirstGroundHitFromCenter = swap1Public ? indexOfFirstGroundHitFromCenter2 : indexOfFirstGroundHitFromCenter1,
    //            world = world,
    //            filter = filter,
    //            origin = origin,
    //            originDown = originDown,
    //            originRight = originRight,
    //            raycastLength = raycastLength,
    //            intervalWidth = intervalWidth,
    //            searchRightFirst = searchRightFirst
    //        };

    //        job.Run();

    //        Origin = origin;
    //        OriginRight = originRight;
    //        lastJobOrigin = origin;
    //        lastJobOriginRight = originRight;
    //    }
    //}

    public void UpdateMap(PhysicsWorld world, PhysicsQuery.QueryFilter filter, Vector2 origin, Vector2 originDown, Vector2 originRight, float raycastLength, bool searchRightFirst)
    {
        var job = new GroundMapUpdate(map1, indexOfFirstGroundHitFromCenter1, world, filter, 
            origin, originDown, originRight, raycastLength, intervalWidth, searchRightFirst);

        job.Run();
        Origin = origin;//lastJobOrigin;
        OriginRight = originRight;//lastJobOriginRight;
        //if (jobHandle.IsCompleted)
        //{
        //    jobHandle.Complete();

        //    swap1Public = !swap1Public;

        //    var job = new GroundMapUpdate()
        //    {
        //        map = swap1Public ? map2 : map1,
        //        indexOfFirstGroundHitFromCenter = swap1Public ? indexOfFirstGroundHitFromCenter2 : indexOfFirstGroundHitFromCenter1,
        //        world = world,
        //        filter = filter,
        //        origin = origin,
        //        originDown = originDown,
        //        originRight = originRight,
        //        raycastLength = raycastLength,
        //        intervalWidth = intervalWidth,
        //        searchRightFirst = searchRightFirst
        //    };

        //    Origin = lastJobOrigin;
        //    OriginRight = lastJobOriginRight;

        //    lastJobOrigin = origin;
        //    lastJobOriginRight = originRight;

        //    jobHandle = job.Schedule();
        //}
    }

    public void DrawGizmos()
    {
        //if (swap1Public ? (!map1.IsCreated || map1.Length == 0) : (!map2.IsCreated || map2.Length == 0)) return;

        if (!map1.IsCreated) return;

        Gizmos.color = GizmoColorLeft;
        for (int i = 0; i < numFwdIntervals; i++)
        {
            Gizmos.DrawSphere((Vector2)MapPoint(i).point, 0.1f);
        }
        Gizmos.color = GizmoColorCenter;
        Gizmos.DrawSphere((Vector2)MapPoint(CentralIndex).point, 0.1f);
        Gizmos.color = GizmoColorRight;
        for (int i = numFwdIntervals + 1; i < NumPoints; i++)
        {
            Gizmos.DrawSphere((Vector2)MapPoint(i).point, 0.1f);
        }
    }
}

[Serializable]//just so i can watch them in the inspector
public struct GroundMapPt
{
    public float arcLengthPosition;
    public float raycastDistance;
    public float2 point;
    public float2 normal;
    public bool hitGround;

    //public bool HitGround => groundCollider != null;

    public float2 Right => normal.CWPerp();

    public GroundMapPt(float2 point, float2 normal, float arcLengthPosition, float raycastDistance, bool hitGround)
    {
        this.point = point;
        this.normal = normal;
        this.arcLengthPosition = arcLengthPosition;
        this.raycastDistance = raycastDistance;
        this.hitGround = hitGround;
        //this.groundCollider = groundCollider;
    }
}