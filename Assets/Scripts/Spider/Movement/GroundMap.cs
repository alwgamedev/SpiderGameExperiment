using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.U2D.Physics;
using UnityEngine;

[Serializable]
public class GroundMap
{
    public const float DEFAULT_PRECISION = 0.5f;

    [SerializeField] int numFwdIntervals;
    [SerializeField] float intervalWidth;

    NativeArray<float2> point;
    NativeArray<float2> readPoint;
    NativeArray<float2> normal;
    NativeArray<float2> readNormal;
    NativeArray<float> arcLengthPos;
    NativeArray<float> readArcLengthPos;
    NativeArray<float> raycastDistance;//only need during job
    NativeArray<bool> hitGround;
    NativeArray<bool> readHitGround;
    AlwaysAccessibleNativeReference<int> indexOfFirstGroundHitFromCenter;

    JobHandle jobHandle;

    Color GizmoColorCenter => Color.red;
    Color GizmoColorRight => Color.green;
    Color GizmoColorLeft => Color.yellow;
    public int CentralIndex => NumPoints / 2;
    public int NumPoints => readPoint.Length;
    public int IndexOfFirstGroundHitFromCenter => indexOfFirstGroundHitFromCenter.Value;

    public float2 Point(int i) => readPoint[i];

    public float2 Normal(int i) => readNormal[i];

    public float2 Right(int i) => Normal(i).CWPerp();

    public float2 Left(int i) => Normal(i).CCWPerp();

    public float ArcLengthPos(int i) => readArcLengthPos[i];

    public bool HitGround(int i) => readHitGround[i];

    public float2 Point(int i, float arcLength)
    {
        (int j, float t) = AddArcLength(i, arcLength);
        return PointFromReducedPosition(j, t);
    }

    public float2 PointFromReducedPosition(int i, float arcLength)
    {
        if (arcLength > 0)
        {
            return i < NumPoints - 1 ?
                math.lerp(Point(i), Point(i + 1), arcLength / (ArcLengthPos(i + 1) - ArcLengthPos(i)))
                : Point(i);
        }
        else
        {
            return i > 0 ?
                math.lerp(Point(i), Point(i - 1), arcLength / (ArcLengthPos(i - 1) - ArcLengthPos(i)))
                : Point(i);
        }
    }

    public float2 Normal(int i, float arcLength)
    {
        (int j, float t) = AddArcLength(i, arcLength);
        return NormalFromReducedPosition(j, t);
    }

    public float2 NormalFromReducedPosition(int i, float arcLength)
    {
        if (arcLength > 0)
        {
            return i < NumPoints - 1 ?
                MathTools.CheapRotationalLerp(Normal(i), Normal(i + 1), arcLength / (ArcLengthPos(i + 1) - ArcLengthPos(i)), out _)
                : Normal(i);
        }
        else
        {
            return i > 0 ?
                MathTools.CheapRotationalLerp(Normal(i), Normal(i - 1), arcLength / (ArcLengthPos(i - 1) - ArcLengthPos(i)), out _)
                : Normal(i);
        }
    }

    /// <summary> Average point between ArcLengthPos(i) + t0 and ArcLengthPos(i) + t1. </summary>
    public float2 AveragePoint(int i, float s0, float s1)
    {
        float length0 = s1 - s0;
        float length = length0;
        if (length < 0)
        {
            return Point(i);
        }

        (int j, float t) = AddArcLength(i, s0);

        float2 sum = 0;

        if (t < 0)
        {
            if (-t > length)
            {
                return 0.5f * (PointFromReducedPosition(j, t) + PointFromReducedPosition(j, t + length));
            }
            else
            {
                sum += 0.5f * -t * (PointFromReducedPosition(j, t) + Point(j));
                length += t;
            }
        }
        else if (t > 0)
        {
            if (j == NumPoints - 1)
            {
                return Point(j);
            }
            else
            {
                var dt = ArcLengthPos(j + 1) - ArcLengthPos(j) - t;
                if (dt > length)
                {
                    return 0.5f * (PointFromReducedPosition(j, t) + PointFromReducedPosition(j, t + length));
                }
                else
                {
                    sum += 0.5f * dt * (PointFromReducedPosition(j, t) + Point(j + 1));
                    length -= dt;
                    j++;
                }
            }
        }

        while (length > 0 && j < NumPoints - 1)
        {
            var dt = ArcLengthPos(j + 1) - ArcLengthPos(j);
            if (dt > length)
            {
                sum += 0.5f * length * (Point(j) + PointFromReducedPosition(j, length));
                length = 0;
                break;
            }

            sum += 0.5f * dt * (Point(j) + Point(j + 1));
            length -= dt;
            j++;
        }

        if (j == NumPoints - 1 && length > 0)
        {
            sum += length * Point(j);
            //length = 0;
        }

        return sum / length0;
    }

    /// <summary> Average normal between ArcLengthPos(i) + t0 and ArcLengthPos(i) + t1. </summary>
    public float2 AverageNormal(int i, float s0, float s1)
    {
        float length = s1 - s0;
        if (length < 0)
        {
            return Normal(i);
        }

        (int j, float t) = AddArcLength(i, s0);

        float2 avg = 0;
        float wt = 0;

        static float2 Avg(float2 n1, float2 n2)
        {
            return MathTools.CheapRotationalLerp(n1, n2, 0.5f, out _);
        }

        static void CombineAvg(ref float2 avg1, ref float wt1, float2 avg2, float wt2)
        {
            wt1 += wt2;
            avg1 = MathTools.CheapRotationalLerp(avg1, avg2, wt2 / wt1, out _);
        }

        if (t < 0)
        {
            if (-t > length)
            {
                return Avg(NormalFromReducedPosition(j, t), NormalFromReducedPosition(j, t + length));
            }
            else
            {
                avg = Avg(NormalFromReducedPosition(j, t), Normal(j));
                wt = -t;
                length += t;
            }
        }
        else if (t > 0)
        {
            if (j == NumPoints - 1)
            {
                return Normal(j);
            }
            else
            {
                var dt = ArcLengthPos(j + 1) - ArcLengthPos(j) - t;
                if (dt > length)
                {
                    return Avg(NormalFromReducedPosition(j, t), NormalFromReducedPosition(j, t + length));
                }
                else
                {
                    avg = Avg(NormalFromReducedPosition(j, t), Normal(j + 1));
                    length -= dt;
                    wt = dt;
                    j++;
                }
            }
        }

        while (length > 0 && j < NumPoints - 1)
        {
            var dt = ArcLengthPos(j + 1) - ArcLengthPos(j);
            float2 segAvg;
            if (dt > length)
            {
                segAvg = Avg(Normal(j), NormalFromReducedPosition(j, length));
                CombineAvg(ref avg, ref wt, segAvg, length);
                return avg;
            }

            segAvg = Avg(Normal(j), Normal(j + 1));
            CombineAvg(ref avg, ref wt, segAvg, dt);
            length -= dt;
            j++;
        }

        if (j == NumPoints - 1 && length > 0)
        {
            CombineAvg(ref avg, ref wt, Normal(j), length);
        }

        return avg;
    }

    /// <summary> Moves from point i by given arc length and returns reduced "index with arc length remainder" (j, t). 
    /// Note that t can be negative.</summary>
    public (int, float) AddArcLength(int i, float arcLength)
    {
        if (arcLength > 0)
        {
            while (i < NumPoints - 1)
            {
                var d = ArcLengthPos(i + 1) - ArcLengthPos(i);
                if (arcLength < d)
                {
                    return (i, arcLength);
                }
                i++;
                arcLength -= d;
            }

            return (i, arcLength);
        }
        else
        {
            while (i > 0)
            {
                var d = ArcLengthPos(i - 1) - ArcLengthPos(i);
                if (arcLength > d)
                {
                    return (i, arcLength);
                }
                i--;
                arcLength -= d;
            }

            return (i, arcLength);
        }
    }

    public (int, float) LineCastOrClosest(float2 p, float2 castDir, float precision)
    {
        var (i, t) = LineCastToGround(p, castDir, precision, out var d2);
        if (math.isinf(d2))
        {
            (i, t) = ClosestPoint(p, precision);
        }

        return (i, t);
    }

    /// <summary> 
    /// Tries to find a point q on ground map such that q - p is parallel to castDir.
    /// If there are multiple such points, returns closest one.
    /// If the cast did not hit, the out parameter bestSqDist will be infinity.
    /// The method uses a sort of tree search to find where Cross(q-p,castDir) changes sign:
    /// it splits the ground map into intervals of width = precision, 
    /// and if start and end of an interval have different cross signs, it splits the interval in two and searches further,
    /// otherwise it assumes there is no sign change (line cast hit) in that interval.
    /// </summary>
    public (int, float) LineCastToGround(float2 p, float2 castDir, float precision, out float bestSqDist)
    {
        int interval = (int)Mathf.Ceil(precision / intervalWidth);

        bestSqDist = math.INFINITY;
        (int, float) bestPt = (0, 0);

        for (int i = 0; i < (NumPoints - 1) / interval; i++)
        {
            int i0 = i * interval;
            int i1 = Mathf.Min(i0 + interval, NumPoints - 1);

            var d0 = Point(i0) - p;
            var d1 = Point(i1) - p;
            var a0 = MathTools.Cross2D(d0, castDir);
            var a1 = MathTools.Cross2D(d1, castDir);

            Search(p, castDir, i0, i1, a0, a1, d0, d1, ref bestSqDist, ref bestPt);
        }

        if (bestPt.Item2 > 0)
        {
            bestPt.Item2 *= ArcLengthPos(bestPt.Item1 + 1) - ArcLengthPos(bestPt.Item1);
        }

        return bestPt;

        //note that if aj = 0, then whatever it gets paired with it will always continue searching 
        //so we don't have to update result when we encounter an aj = 0, because it will get checked when the search terminates.
        //(although getting a zero on one of the actual map points will almost never happen)
        void Search(float2 p, float2 castDir, int i0, int i1, float a0, float a1, float2 d0, float2 d1,
            ref float bestSqDist, ref (int, float) bestPt)
        {
            if (!(i0 < i1) || (math.sign(a0) == math.sign(a1) && a0 != 0))
            {
                return;
            }

            if (i1 == i0 + 1)//we've honed in on where the sign change occurs and should record the results
            {
                if (a0 == 0)
                {
                    var d0Sq = math.lengthsq(d0);
                    if (d0Sq < bestSqDist)
                    {
                        bestSqDist = d0Sq;
                        bestPt = (i0, 0);
                    }
                }

                if (a1 == 0)
                {
                    var d1Sq = math.lengthsq(d1);
                    if (d1Sq < bestSqDist)
                    {
                        bestSqDist = d1Sq;
                        bestPt = (i1, 0);
                    }
                }
                else if (a0 != 0)//a0 != 0 && a1 != 0
                {
                    var t = a0 / (a0 - a1);//time at which zero occurs
                    var q = math.lerp(Point(i0), Point(i1), t);
                    var sqDist = math.distancesq(p, q);
                    if (sqDist < bestSqDist)
                    {
                        bestSqDist = sqDist;
                        bestPt = (i0, t);
                    }
                }

                return;
            }

            var iMid = (i0 + i1) / 2;
            var dMid = Point(iMid) - p;
            var aMid = MathTools.Cross2D(dMid, castDir);

            //recursion not a big deal when our array is smallish (groundMap usually < 100 points)
            Search(p, castDir, i0, iMid, a0, aMid, d0, dMid, ref bestSqDist, ref bestPt);//search left half
            Search(p, castDir, iMid, i1, aMid, a1, dMid, d1, ref bestSqDist, ref bestPt);//search right half
        }
    }

    /// <summary> Same tree traversal approach as LineCast, but looking for a point q on ground map whose normal is parallel
    /// to q - p. If it finds multiple such points (local extremes of distance function), it picks the closest one.
    /// If no local extremes found, returns the closer of the two map endpoints.
    /// </summary>
    public (int, float) ClosestPoint(float2 p, float precision)
    {
        int interval = (int)Mathf.Ceil(precision / intervalWidth);

        float bestSqDist;
        (int, float) bestPt;

        //start by checking dist to endpoints of map (so we can return something meaningful even if no local extremes found)
        var left2 = math.lengthsq(Point(0) - p);
        var right2 = math.lengthsq(Point(NumPoints - 1) - p);
        if (left2 < right2)
        {
            bestSqDist = left2;
            bestPt = (0, 0);
        }
        else
        {
            bestSqDist = right2;
            bestPt = (NumPoints - 1, 0);
        }

        for (int i = 0; i < (NumPoints - 1) / interval; i++)
        {
            int i0 = i * interval;
            int i1 = Mathf.Min(i0 + interval, NumPoints - 1);

            var d0 = Point(i0) - p;
            var d1 = Point(i1) - p;
            var a0 = MathTools.Cross2D(d0, Normal(i0));
            var a1 = MathTools.Cross2D(d1, Normal(i1));

            Search(p, i0, i1, a0, a1, d0, d1, ref bestSqDist, ref bestPt);
        }

        if (bestPt.Item2 > 0)
        {
            bestPt.Item2 *= ArcLengthPos(bestPt.Item1 + 1) - ArcLengthPos(bestPt.Item1);
        }

        return bestPt;

        //note that if aj = 0, then whatever it gets paired with it will always continue searching 
        //so we don't have to update result when we encounter an aj = 0, because it will get checked when the search terminates
        //(although aj = 0 will rarely ever happen)
        void Search(float2 p, int i0, int i1, float a0, float a1, float2 d0, float2 d1,
            ref float bestSqDist, ref (int, float) bestPt)
        {
            if (!(i0 < i1) || (math.sign(a0) == math.sign(a1) && a0 != 0))
            {
                return;
            }

            if (i1 == i0 + 1)//we've honed in on where the sign change occurs and should record the results
            {
                if (a0 == 0)
                {
                    var d0Sq = math.lengthsq(d0);
                    if (d0Sq < bestSqDist)
                    {
                        bestSqDist = d0Sq;
                        bestPt = (i0, 0);
                    }
                }

                if (a1 == 0)
                {
                    var d1Sq = math.lengthsq(d1);
                    if (d1Sq < bestSqDist)
                    {
                        bestSqDist = d1Sq;
                        bestPt = (i1, 0);
                    }
                }
                else if (a0 != 0)//a0 != 0 && a1 != 0
                {
                    //find the closest point to p between q0 and q1
                    var q0 = Point(i0);
                    var q1 = Point(i1);
                    var n = (q1 - q0).CCWPerp();//don't need to normalize (scale cancels out when computing t)
                    var b0 = MathTools.Cross2D(d0, n);
                    var b1 = MathTools.Cross2D(d1, n);
                    var t = Mathf.Clamp(b0 / (b0 - b1), 0, 1);//clamp since we used different normals for detecting sign change
                    var q = math.lerp(q0, q1, t);
                    var sqDist = math.distancesq(p, q);
                    if (sqDist < bestSqDist)
                    {
                        bestSqDist = sqDist;
                        bestPt = (i0, t);
                    }
                }

                return;
            }

            var iMid = (i0 + i1) / 2;
            var dMid = Point(iMid) - p;
            var aMid = MathTools.Cross2D(dMid, Normal(iMid));

            //recursion not a big deal when our array is smallish (groundMap usually < 100 points)
            Search(p, i0, iMid, a0, aMid, d0, dMid, ref bestSqDist, ref bestPt);//search left half
            Search(p, iMid, i1, aMid, a1, dMid, d1, ref bestSqDist, ref bestPt);//search right half
        }
    }


    //public float2 ProjectOntoGroundByArcLength(float2 p, out float2 normal, out bool hitGround, float htFraction = 1, bool onlyUseHtFractionIfNotHitGround = false)
    //{
    //    p = PointFromCenterByArcLength(Vector2.Dot(p - Origin, OriginRight), out normal, out hitGround);
    //    if (htFraction < 1 && (!onlyUseHtFractionIfNotHitGround || !hitGround))
    //    {
    //        var y = math.dot(Origin - p, normal);
    //        if (y > 0)
    //        {
    //            p += (1 - htFraction) * y * normal;
    //        }
    //    }

    //    return p;
    //}

    //public float2 PointFromCenterByArcLength(float x, out float2 normal, out bool hitGround)
    //{
    //    if (x > 0)
    //    {
    //        for (int i = numFwdIntervals; i < NumPoints - 1; i++)
    //        {
    //            var p = MapPoint(i + 1);
    //            if (p.arcLengthPosition > x)
    //            {
    //                var q = MapPoint(i);
    //                var s = p.arcLengthPosition - q.arcLengthPosition;
    //                normal = q.normal;
    //                hitGround = q.hitGround;
    //                return math.lerp(q.point, p.point, (x - q.arcLengthPosition) / s);
    //            }
    //        }

    //        var t = x - RightEndPt.arcLengthPosition;
    //        normal = RightEndPt.normal;
    //        hitGround = RightEndPt.hitGround;
    //        return RightEndPt.point + t * RightEndPt.normal.CWPerp();
    //    }

    //    for (int i = numFwdIntervals; i > 0; i--)
    //    {
    //        var p = MapPoint(i - 1);
    //        if (p.arcLengthPosition < x)
    //        {
    //            var q = MapPoint(i);
    //            var s = p.arcLengthPosition - q.arcLengthPosition;
    //            normal = q.normal;
    //            hitGround = q.hitGround;
    //            return math.lerp(q.point, p.point, (x - q.arcLengthPosition) / s);
    //        }
    //    }

    //    var u = x - LeftEndPt.arcLengthPosition;
    //    normal = LeftEndPt.normal;
    //    hitGround = LeftEndPt.hitGround;
    //    return LeftEndPt.point + u * LeftEndPt.normal.CWPerp();
    //}

    ////find point q on the ground such that p - q parallel to line cast direction
    //public bool LineCastToGround(float2 p, float2 dir, out GroundMapPt hit)
    //{
    //    var a1 = MathTools.Cross2D(p - MapPoint(0).point, dir);
    //    if (a1 == 0)
    //    {
    //        hit = MapPoint(0);
    //        return true;
    //    }
    //    for (int i = 1; i < NumPoints; i++)
    //    {
    //        var a2 = MathTools.Cross2D(p - MapPoint(i).point, dir);
    //        if (a2 == 0)
    //        {
    //            if (math.dot(MapPoint(i).normal, dir) < 0)
    //            {
    //                hit = MapPoint(i);
    //                return true;
    //            }
    //        }
    //        else if (MathTools.OppositeSigns(a1, a2))
    //        {
    //            var t =  math.abs(a1 / (a2 - a1));
    //            var q0 = MapPoint(i - 1);
    //            var q1 = MapPoint(i);
    //            var normal = MathTools.CheapRotationalLerp(q0.normal, q1.normal, t, out _);
    //            if (Vector2.Dot(normal, dir) < 0)
    //            {
    //                hit = new()
    //                {
    //                    point = math.lerp(q0.point, q1.point, t),
    //                    normal = normal,
    //                    hitGround = t < 0.5f ? q0.hitGround : q1.hitGround,
    //                    arcLengthPosition = math.lerp(q0.arcLengthPosition, q1.arcLengthPosition, t)
    //                };
    //                return true;
    //            }
    //        }
    //        a1 = a2;
    //    }

    //    hit = default;
    //    return false;
    //}

    //public float2 TrueClosestPoint(float2 p, out float arcLengthPosition, out float2 normal, out bool hitGround)
    //{
    //    var bestIndex = -1;
    //    var bestDistSqrd = Mathf.Infinity;

    //    for (int i = 0; i < NumPoints; i++)
    //    {
    //        var d2 = math.lengthsq(p - MapPoint(i).point);
    //        if (d2 < bestDistSqrd)
    //        {
    //            bestIndex = i;
    //            bestDistSqrd = d2;
    //        }
    //    }

    //    var cur = MapPoint(bestIndex);
    //    var q = cur.point;
    //    arcLengthPosition = cur.arcLengthPosition;
    //    var q1 = q;
    //    var v = p - q;
    //    normal = cur.normal;
    //    hitGround = cur.hitGround;

    //    //see if a point on next or prev segments is closer
    //    if (bestIndex < NumPoints - 1)
    //    {
    //        var w = MapPoint(bestIndex + 1).point - q;
    //        var dot = math.dot(v, w);
    //        if (dot > 0 && dot < math.lengthsq(w))
    //        {
    //            var next = MapPoint(bestIndex + 1);
    //            var t = dot / math.length(w);
    //            q1 = math.lerp(q, next.point, t);
    //            arcLengthPosition = math.lerp(arcLengthPosition, next.arcLengthPosition, t);
    //            bestDistSqrd = math.lengthsq(p - q1);
    //            normal = MathTools.CheapRotationalLerp(cur.normal, next.normal, t, out _);
    //        }
    //    }

    //    if (bestIndex > 0)
    //    {
    //        var prev = MapPoint(bestIndex - 1);
    //        var w = prev.point - q;
    //        var dot = math.dot(v, w);
    //        if (dot > 0 && dot < math.lengthsq(w))
    //        {
    //            var t = dot / math.length(w);
    //            var q2 = math.lerp(q, prev.point, t);
    //            var dist2 = math.lengthsq(p - q2);
    //            if (dist2 < bestDistSqrd)
    //            {
    //                q1 = q2;
    //                arcLengthPosition = math.lerp(arcLengthPosition, prev.arcLengthPosition, t);
    //                normal = MathTools.CheapRotationalLerp(cur.normal, prev.normal, t, out _);
    //            }
    //        }
    //    }

    //    return q1;
    //}

    ////finds first local min in distance and returns that point
    //public float2 FastClosestPoint(float2 p, out float2 normal, out bool hitGround)
    //{
    //    var mapPt = MapPoint(0);
    //    var a1 = MathTools.Cross2D(p - mapPt.point, mapPt.normal);
    //    if (a1 == 0)
    //    {
    //        hitGround = mapPt.hitGround;
    //        normal = mapPt.normal;
    //        return mapPt.point;
    //    }
    //    for (int i = 1; i < NumPoints; i++)
    //    {
    //        mapPt = MapPoint(i);
    //        var a2 = MathTools.Cross2D(p - mapPt.point, mapPt.normal);
    //        if (a2 == 0)
    //        {
    //            hitGround = mapPt.hitGround;
    //            normal = mapPt.normal;
    //            return mapPt.point;
    //        }
    //        if (MathTools.OppositeSigns(a1, a2))
    //        {
    //            var prev = MapPoint(i - 1);
    //            var t = math.abs(a1 / (a2 - a1));
    //            hitGround = t < 0.5f ? prev.hitGround : mapPt.hitGround;
    //            normal = MathTools.CheapRotationalLerp(prev.normal, mapPt.normal, t, out _);
    //            return math.lerp(prev.point, mapPt.point, t);
    //        }
    //        a1 = a2;
    //    }

    //    if (math.dot(p - RightEndPt.point, RightEndPt.Right) > 0)
    //    {
    //        hitGround = RightEndPt.hitGround;
    //        normal = RightEndPt.normal;
    //        return RightEndPt.point;
    //    }

    //    hitGround = LeftEndPt.hitGround;
    //    normal = LeftEndPt.normal;
    //    return LeftEndPt.point;
    //}

    //public float2 AveragePointFromCenter(float minPos, float maxPos)
    //{
    //    maxPos += Mathf.Epsilon;//so we can use < instead of <=
    //    minPos -= Mathf.Epsilon;
    //    float2 sum = 0;
    //    int ct = 0;

    //    for (int i = 0; i < NumPoints; i++)
    //    {
    //        var p = MapPoint(i);
    //        if (p.arcLengthPosition < maxPos && p.arcLengthPosition > minPos)
    //        {
    //            sum += p.point;
    //            ct++;
    //        }
    //    }

    //    return ct == 0 ? sum : sum / ct;
    //}

    //public float2 AverageNormalFromCenter(float minPos, float maxPos)
    //{
    //    maxPos += Mathf.Epsilon;//so we can use < instead of <=
    //    minPos -= Mathf.Epsilon;
    //    float2 sum = 0;
    //    int ct = 0;

    //    for (int i = 0; i < NumPoints; i++)
    //    {
    //        var p = MapPoint(i);
    //        if (p.arcLengthPosition < maxPos && p.arcLengthPosition > minPos)
    //        {
    //            sum += p.normal;
    //            ct++;
    //        }
    //    }

    //    return ct == 0 ? sum : sum / ct;
    //}

    public void Initialize(float2 origin, float2 originRight, float raycastLength)
    {
        var numPoints = 2 * numFwdIntervals + 1;
        var centralIndex = numFwdIntervals;
        //map = new NativeArray<GroundMapPt>(numPoints, Allocator.Persistent);
        //readMap = new NativeArray<GroundMapPt>(numPoints, Allocator.Persistent);
        point = new NativeArray<float2>(numPoints, Allocator.Persistent);
        readPoint = new NativeArray<float2>(numPoints, Allocator.Persistent);
        normal = new NativeArray<float2>(numPoints, Allocator.Persistent);
        readNormal = new NativeArray<float2>(numPoints, Allocator.Persistent);
        arcLengthPos = new NativeArray<float>(numPoints, Allocator.Persistent);
        readArcLengthPos = new NativeArray<float>(numPoints, Allocator.Persistent);
        raycastDistance = new NativeArray<float>(numPoints, Allocator.Persistent);
        hitGround = new NativeArray<bool>(numPoints, Allocator.Persistent);
        readHitGround = new NativeArray<bool>(numPoints, Allocator.Persistent);
        indexOfFirstGroundHitFromCenter = new(centralIndex, Allocator.Persistent);

        //this.origin = origin;
        //this.originRight = originRight;
        //nextOrigin = origin;
        //nextOriginRight = originRight;

        //initialize map with flat ground until first job comes in
        var up = originRight.CCWPerp();
        float2 centerPt = origin - raycastLength * up;
        for (int i = 0; i < numPoints; i++)
        {
            var k = i - centralIndex;
            var s = k * intervalWidth;
            arcLengthPos[i] = s;
            point[i] = centerPt + s * originRight;
            normal[i] = up;
            hitGround[i] = false;
            raycastDistance[i] = raycastLength;
        }

        CopyToReadableArrays();
    }

    public void UpdateMap(PhysicsWorld world, PhysicsQuery.QueryFilter filter, Vector2 origin, Vector2 originDown, Vector2 originRight,
        float raycastLength, bool searchRightFirst)
    {
        jobHandle.Complete();
        CopyToReadableArrays();

        indexOfFirstGroundHitFromCenter.Locked = false;//flick lock off/on to update public value
        indexOfFirstGroundHitFromCenter.Locked = true;

        //this.origin = nextOrigin;
        //this.originRight = nextOriginRight;
        //nextOrigin = origin;
        //nextOriginRight = originRight;

        var job = new GroundMapUpdate(point, normal, arcLengthPos, raycastDistance, hitGround, world, filter,
            origin, originDown, originRight, indexOfFirstGroundHitFromCenter.native, raycastLength, intervalWidth, searchRightFirst);

        jobHandle = job.Schedule();
    }

    public void DrawGizmos()
    {
        //if (swap1Public ? (!map1.IsCreated || map1.Length == 0) : (!map2.IsCreated || map2.Length == 0)) return;

        if (!readPoint.IsCreated) return;

        Gizmos.color = GizmoColorLeft;
        for (int i = 0; i < numFwdIntervals; i++)
        {
            Gizmos.DrawSphere((Vector2)Point(i), 0.1f);
        }
        Gizmos.color = GizmoColorCenter;
        Gizmos.DrawSphere((Vector2)Point(CentralIndex), 0.1f);
        Gizmos.color = GizmoColorRight;
        for (int i = numFwdIntervals + 1; i < NumPoints; i++)
        {
            Gizmos.DrawSphere((Vector2)Point(i), 0.1f);
        }
    }

    public void Dispose()
    {
        jobHandle.Complete();

        if (point.IsCreated)
        {
            point.Dispose();
        }
        if (readPoint.IsCreated)
        {
            readPoint.Dispose();
        }
        if (normal.IsCreated)
        {
            normal.Dispose();
        }
        if (readNormal.IsCreated)
        {
            readNormal.Dispose();
        }
        if (arcLengthPos.IsCreated)
        {
            arcLengthPos.Dispose();
        }
        if (readArcLengthPos.IsCreated)
        {
            readArcLengthPos.Dispose();
        }
        if (raycastDistance.IsCreated)
        {
            raycastDistance.Dispose();
        }
        if (hitGround.IsCreated)
        {
            hitGround.Dispose();
        }
        if (readHitGround.IsCreated)
        {
            readHitGround.Dispose();
        }
        if (indexOfFirstGroundHitFromCenter.native.IsCreated)
        {
            indexOfFirstGroundHitFromCenter.Dispose();
        }
    }

    private void CopyToReadableArrays()
    {
        readPoint.CopyFrom(point);
        readNormal.CopyFrom(normal);
        readArcLengthPos.CopyFrom(arcLengthPos);
        readHitGround.CopyFrom(hitGround);
    }
}

//[Serializable]//just so i can watch them in the inspector
//public struct GroundMapPt
//{
//    public float2 point;
//    public float2 normal;
//    public float arcLengthPosition;
//    public float raycastDistance;
//    public bool hitGround;

//    //public bool HitGround => groundCollider != null;

//    public float2 Right => normal.CWPerp();

//    public GroundMapPt(float2 point, float2 normal, float arcLengthPosition, float raycastDistance, bool hitGround)
//    {
//        this.point = point;
//        this.normal = normal;
//        this.arcLengthPosition = arcLengthPosition;
//        this.raycastDistance = raycastDistance;
//        this.hitGround = hitGround;
//        //this.groundCollider = groundCollider;
//    }
//}