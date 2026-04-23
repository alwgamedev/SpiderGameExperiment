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

    public void Initialize(float2 origin, float2 originRight, float raycastLength)
    {
        var numPoints = 2 * numFwdIntervals + 1;
        var centralIndex = numFwdIntervals;
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

        var job = new GroundMapUpdate(point, normal, arcLengthPos, raycastDistance, hitGround, world, filter,
            origin, originDown, originRight, indexOfFirstGroundHitFromCenter.native, raycastLength, intervalWidth, searchRightFirst);

        jobHandle = job.Schedule();
    }

    public void DrawGizmos()
    {
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