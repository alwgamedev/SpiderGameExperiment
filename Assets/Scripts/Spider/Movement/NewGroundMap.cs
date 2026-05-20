using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.U2D.Physics;
using UnityEngine;

[Serializable]
public class NewGroundMap
{
    [SerializeField] int numFwdIntervals;
    [SerializeField] float intervalWidth;

    NativeArray<float2> point;
    NativeArray<float2> readPoint;
    NativeArray<float2> normal;
    NativeArray<float2> readNormal;
    NativeArray<float> arcLengthPos;
    NativeArray<float> readArcLengthPos;
    NativeArray<PhysicsCoreHelper.ShapeProxyForJobs> shapeCapture;
    AlwaysAccessibleNativeReference<int> endRight;
    AlwaysAccessibleNativeReference<int> endLeft;
    AlwaysAccessibleNativeReference<int> firstHitRight;
    AlwaysAccessibleNativeReference<int> firstHitLeft;

    JobHandle jobHandle;

    public int CentralIndex => NumPoints / 2;
    public int NumPoints => readPoint.Length;
    public int NumActivePoints => EndRight - EndLeft + 1;
    public int EndRight => endRight.Value;
    public int EndLeft => endLeft.Value;

    public float2 Point(int i) => readPoint[i];

    public float2 Normal(int i) => readNormal[i];

    public float2 Right(int i) => Normal(i).CWPerp();

    public float2 Left(int i) => Normal(i).CCWPerp();

    public float ArcLengthPos(int i) => readArcLengthPos[i];

    public bool HitGround(int i) => !(i < firstHitRight.Value) || !(i > firstHitLeft.Value);

    public int FirstGroundHitFromCenter(bool prioritizeRight)
    {
        return prioritizeRight ?
            firstHitRight.Value < point.Length ? firstHitRight.Value : firstHitLeft.Value :
            firstHitLeft.Value > -1 ? firstHitLeft.Value : firstHitRight.Value;
    }

    public float2 Point(int i, float arcLength)
    {
        (int j, float t) = AddArcLength(i, arcLength);
        return PointFromReducedPosition(j, t);
    }

    public float2 PointFromReducedPosition(int i, float arcLength)
    {
        if (arcLength > 0)
        {
            return i < EndRight ?
                Vector2.Lerp(Point(i), Point(i + 1), arcLength / (ArcLengthPos(i + 1) - ArcLengthPos(i)))
                : Point(i);
        }
        else
        {
            return i > EndLeft ?
                Vector2.Lerp(Point(i), Point(i - 1), arcLength / (ArcLengthPos(i - 1) - ArcLengthPos(i)))
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
            return i < EndRight ?
                MathTools.CheapRotationalLerp(Normal(i), Normal(i + 1), arcLength / (ArcLengthPos(i + 1) - ArcLengthPos(i)), out _)
                : Normal(i);
        }
        else
        {
            return i > EndLeft ?
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
            if (j == EndRight)
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

        while (length > 0 && j < EndRight)
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

        if (j == EndRight && length > 0)
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
            if (j == EndRight)
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

        while (length > 0 && j < EndRight)
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

        if (j == EndRight && length > 0)
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
            while (i < EndRight)
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
            while (i > EndLeft)
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

    public (int, float) LineCastOrClosest(float2 p, float2 castDir)
    {
        var (i, t) = LineCastToGround(p, castDir, out var d2);
        if (!float.IsFinite(d2))
        {
            (i, t) = ClosestPoint(p);
        }

        return (i, t);
    }

    /// <summary> 
    /// Tries to find a point q on ground map such that q - p is parallel to castDir.
    /// If there are multiple such points, returns closest one.
    /// </summary>
    
    //2do: might right static bursted versions of some of these (the number of active points now is pretty low)
    public (int, float) LineCastToGround(float2 p, float2 castDir, out float bestSqDist)
    {
        bestSqDist = Mathf.Infinity;
        (int, float) bestPt = (-1, 0);

        int i = EndLeft;
        var p0 = Point(i);
        var d0 = p0 - p;
        var a0 = MathTools.Cross2D(d0, castDir);

        if (a0 == 0)
        {
            var d0Sq = Vector2.SqrMagnitude(d0);
            if (d0Sq < bestSqDist)
            {
                bestSqDist = d0Sq;
                bestPt = (i, 0);
            }
        }

        while (i < EndRight)
        {
            i++;
            var p1 = Point(i);
            var d1 = p1 - p;
            var a1 = MathTools.Cross2D(d1, castDir);

            if (a1 == 0)
            {
                var d1Sq = Vector2.SqrMagnitude(d1);
                if (d1Sq < bestSqDist)
                {
                    bestSqDist = d1Sq;
                    bestPt = (i, 0);
                }
            }

            if (MathTools.OppositeSigns(a0, a1))
            {
                var t = a0 / (a0 - a1);//time at which zero occurs
                float2 q = Vector2.Lerp(p0, p1, t);
                var d = q - p;
                var dSq = Vector2.SqrMagnitude(d);
                if (dSq < bestSqDist)
                {
                    bestSqDist = dSq;
                    bestPt = (i, t);
                }
            }

            p0 = p1;
            a0 = a1;
        }

        return bestPt;
    }

    /// <summary> Looks for a point q on ground map whose normal is parallel to q - p (local extreme of distance)
    /// If it finds multiple such points, it picks the closest one.
    /// If no local extremes found, returns the closer of the two map endpoints.
    /// </summary>
    public (int, float) ClosestPoint(float2 p)
    {
        float bestSqDist;
        (int, float) bestPt;

        var left2 = Vector2.SqrMagnitude(Point(EndLeft) - p);
        var right2 = Vector2.SqrMagnitude(Point(EndRight) - p);
        if (left2 < right2)
        {
            bestSqDist = left2;
            bestPt = (EndLeft, 0);
        }
        else
        {
            bestSqDist = right2;
            bestPt = (EndRight, 0);
        }

        int i = EndLeft;
        var p0 = Point(i);
        var d0 = p0 - p;
        var a0 = MathTools.Cross2D(d0, Normal(i));

        if (a0 == 0)
        {
            var d0Sq = Vector2.SqrMagnitude(d0);
            if (d0Sq < bestSqDist)
            {
                bestSqDist = d0Sq;
                bestPt = (i, 0);
            }
        }

        while (i < EndRight)
        {
            i++;
            var p1 = Point(i);
            var d1 = p1 - p;
            var a1 = MathTools.Cross2D(d1, Normal(i));

            if (a1 == 0)
            {
                var d1Sq = Vector2.SqrMagnitude(d1);
                if (d1Sq < bestSqDist)
                {
                    bestSqDist = d1Sq;
                    bestPt = (i, 0);
                }
            }

            if (MathTools.OppositeSigns(a0, a1))
            {
                var t = a0 / (a0 - a1);//time at which zero occurs
                float2 q = Vector2.Lerp(p0, p1, t);
                var d = q - p;
                var dSq = Vector2.SqrMagnitude(d);
                if (dSq < bestSqDist)
                {
                    bestSqDist = dSq;
                    bestPt = (i, t);
                }
            }

            p0 = p1;
            a0 = a1;
        }

        return bestPt;
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
        endRight = new(Allocator.Persistent);
        endLeft = new(Allocator.Persistent);
        firstHitRight = new(Allocator.Persistent);
        firstHitLeft = new(Allocator.Persistent);

        shapeCapture = new(2048, Allocator.Persistent);

        //initialize map with flat ground until first job comes in
        var up = originRight.CCWPerp();
        float2 centerPt = origin - raycastLength * up;
        for (int i = 0; i < numPoints; i++)
        {
            var s = (i - centralIndex) * intervalWidth;
            arcLengthPos[i] = s;
            point[i] = centerPt + s * originRight;
            normal[i] = up;
        }

        endRight.Value = numPoints - 1;
        endLeft.Value = 0;
        firstHitRight.Value = numPoints;
        firstHitLeft.Value = -1;

        endRight.Locked = true;
        endLeft.Locked = true;
        firstHitRight.Locked = true;
        firstHitLeft.Locked = true;

        CopyToReadableArrays();
    }

    public void UpdateMap(PhysicsWorld world, PhysicsQuery.QueryFilter filter, Vector2 origin, Vector2 originUp, float raycastLength)
    {
        //jobHandle.Complete();
        //CopyToReadableArrays();

        CaptureShapes(world, filter, origin);

        var job = new NewGroundMapUpdate(point, normal, arcLengthPos, endRight.native, endLeft.native, firstHitRight.native, firstHitLeft.native,
            shapeCapture, world, filter, origin, originUp, raycastLength, intervalWidth);

        job.Run();
        CopyToReadableArrays();

        //jobHandle = job.Schedule();
    }

    public void DrawGizmos()
    {
        if (!readPoint.IsCreated) return;

        int i = endLeft.Value;
        Vector2 p = readPoint[endLeft.Value];
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(p, 0.1f);

        while (i < endRight.Value)
        {
            i++;
            Vector2 q = readPoint[i];
            Gizmos.DrawLine(p, q);
            Gizmos.DrawSphere(q, 0.1f);
            p = q;
        }

        Gizmos.color = Color.blue;
        Gizmos.DrawSphere((Vector2)Point(CentralIndex), 0.1f);
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

        if (shapeCapture.IsCreated)
        {
            shapeCapture.Dispose();
        }

        endRight.Dispose();
        endLeft.Dispose();
        firstHitRight.Dispose();
        firstHitLeft.Dispose();
    }

    private void CaptureShapes(PhysicsWorld world, PhysicsQuery.QueryFilter filter, float2 origin)
    {
        var x = intervalWidth * NumPoints;
        var bbExtent = new float2(x, x);
        var overlap = world.OverlapAABB(new PhysicsAABB(origin - bbExtent, origin + bbExtent), filter);

        if (!(shapeCapture.Length > PhysicsRegistry.MaxShapeId))
        {
            shapeCapture.Dispose();
            shapeCapture = new(2 * PhysicsRegistry.MaxShapeId, Allocator.Persistent);
        }


        for (int i = 0; i < overlap.Length; i++)
        {
            var shape = overlap[i].shape;
            var id = shape.Id();
            if (id > 0)
            {
                switch (shape.shapeType)
                {
                    case PhysicsShape.ShapeType.Polygon:
                        shapeCapture[id] = new(shape.polygonGeometry);
                        break;
                    case PhysicsShape.ShapeType.Circle:
                        shapeCapture[id] = new(shape.circleGeometry);
                        break;
                    case PhysicsShape.ShapeType.Capsule:
                        shapeCapture[id] = new(shape.capsuleGeometry);
                        break;
                }
            }
        }
    }

    private void CopyToReadableArrays()
    {
        readPoint.CopyFrom(point);
        readNormal.CopyFrom(normal);
        readArcLengthPos.CopyFrom(arcLengthPos);

        endRight.UpdateSnapshot();
        endLeft.UpdateSnapshot();
        firstHitRight.UpdateSnapshot();
        firstHitLeft.UpdateSnapshot();
    }
}