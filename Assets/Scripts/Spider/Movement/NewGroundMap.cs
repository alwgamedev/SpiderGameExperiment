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

    //normals, unlike points, are piecewise constant
    //if i >= CentralIndex, then normal[i] is held on the interval to the right[i, i + 1),
    //and if i <= CentralIndex, then normal[i] is held on the interval to the left (i - 1, i],
    //(this is important for the new system where we can have long flat segments
    //-- when all the points were tightly spaced it made more sense to interpolate normals btwn points, but now it doesn't)
    public float2 NormalFromReducedPosition(int i, float arcLength)
    {
        if (arcLength == 0)
        {
            return Normal(i);
        }
        else if (arcLength > 0)
        {
            return i < CentralIndex ? Normal(i + 1) : Normal(i);
        }
        else
        {
            return i > CentralIndex ? Normal(i - 1) : Normal(i);
        }
        //if (arcLength > 0)
        //{
        //    return i < EndRight ?
        //        MathTools.CheapRotationalLerp(Normal(i), Normal(i + 1), arcLength / (ArcLengthPos(i + 1) - ArcLengthPos(i)), out _)
        //        : Normal(i);
        //}
        //else
        //{
        //    return i > EndLeft ?
        //        MathTools.CheapRotationalLerp(Normal(i), Normal(i - 1), arcLength / (ArcLengthPos(i - 1) - ArcLengthPos(i)), out _)
        //        : Normal(i);
        //}
    }

    /// <summary> Average point between ArcLengthPos(i) + s0 and ArcLengthPos(i) + s1. </summary>
    public float2 AveragePoint(int i, float s0, float s1)
    {
        if (s1 < s0)
        {
            return Point(i);
        }

        (int j, float t) = AddArcLength(i, s0);
        (int jEnd, float tEnd) = AddArcLength(i, s1);

        if (tEnd < 0)
        {
            if (jEnd == EndLeft)
            {
                return Point(EndLeft);
            }

            tEnd += ArcLengthPos(jEnd) - ArcLengthPos(--jEnd);
        }

        if (j == jEnd && !MathTools.OppositeSigns(t, tEnd))//this is (*)
        {
            //start and end point lie in the same segment
            return PointFromReducedPosition(j, 0.5f * (t + tEnd));
        }

        float2 sum;
        float length;

        if (t < 0)
        {
            sum = -t * PointFromReducedPosition(j, 0.5f * t);
            length = -t;
        }
        else
        {
            length = ArcLengthPos(j + 1) - ArcLengthPos(j) - t;
            sum = length * PointFromReducedPosition(j, t + 0.5f * length);
            j++;//increment is valid after (*)
        }

        while (j < jEnd)
        {
            var dt = ArcLengthPos(j + 1) - ArcLengthPos(j);
            sum += dt * PointFromReducedPosition(j, 0.5f * dt);
            length += dt;
            j++;
        }

        if (tEnd > 0)
        {
            sum += tEnd * PointFromReducedPosition(jEnd, 0.5f * tEnd);
            length += tEnd;
        }

        return sum / length;

        //float length0 = s1 - s0;
        //float length = length0;
        //if (length < 0)
        //{
        //    return Point(i);
        //}

        //(int j, float t) = AddArcLength(i, s0);

        //float2 sum = 0;

        //if (t < 0)
        //{
        //    if (-t > length)
        //    {
        //        return 0.5f * (PointFromReducedPosition(j, t) + PointFromReducedPosition(j, t + length));
        //    }
        //    else
        //    {
        //        sum += 0.5f * -t * (PointFromReducedPosition(j, t) + Point(j));
        //        length += t;
        //    }
        //}
        //else if (t > 0)
        //{
        //    if (j == EndRight)
        //    {
        //        return Point(j);
        //    }
        //    else
        //    {
        //        var dt = ArcLengthPos(j + 1) - ArcLengthPos(j) - t;
        //        if (dt > length)
        //        {
        //            return 0.5f * (PointFromReducedPosition(j, t) + PointFromReducedPosition(j, t + length));
        //        }
        //        else
        //        {
        //            sum += 0.5f * dt * (PointFromReducedPosition(j, t) + Point(j + 1));
        //            length -= dt;
        //            j++;
        //        }
        //    }
        //}

        //while (length > 0 && j < EndRight)
        //{
        //    var dt = ArcLengthPos(j + 1) - ArcLengthPos(j);
        //    if (dt > length)
        //    {
        //        sum += 0.5f * length * (Point(j) + PointFromReducedPosition(j, length));
        //        length = 0;
        //        break;
        //    }

        //    sum += 0.5f * dt * (Point(j) + Point(j + 1));
        //    length -= dt;
        //    j++;
        //}

        //if (j == EndRight && length > 0)
        //{
        //    sum += length * Point(j);
        //    //length = 0;
        //}

        //return sum / length0;
    }

    /// <summary> Average normal between ArcLengthPos(i) + s0 and ArcLengthPos(i) + s1. </summary>
    public float2 AverageNormal(int i, float s0, float s1)
    {
        if (s1 < s0)
        {
            return Normal(i);
        }

        (int j, float t) = AddArcLength(i, s0);
        (int jEnd, float tEnd) = AddArcLength(i, s1);

        if (tEnd < 0)
        {
            if (jEnd == EndLeft)
            {
                return Normal(EndLeft);
            }

            tEnd += ArcLengthPos(jEnd) - ArcLengthPos(--jEnd);
        }

        if (j == jEnd && !MathTools.OppositeSigns(t, tEnd))//this is (*)
        {
            //start and end point lie in the same segment
            return Normal(j, t);
        }

        float2 n;
        float wt;

        if (t < 0)
        {
            n = NormalFromReducedPosition(j, t);
            wt = -t;
        }
        else
        {
            n = NormalFromReducedPosition(j, t);
            wt = -t - ArcLengthPos(j) + ArcLengthPos(++j);//increment is valid after (*)
        }

        while (j < jEnd)
        {
            //use Normal(j, epsilon) to make sure we sample the normal in the interior of the interval
            //(due to the discrepancy in how normals are attached for segments left of center and right of center)
            (n, wt) = Avg(n, wt, NormalFromReducedPosition(j, float.Epsilon), -ArcLengthPos(j) + ArcLengthPos(++j));
        }

        if (tEnd > 0)
        {
            (n, _) = Avg(n, wt, NormalFromReducedPosition(jEnd, float.Epsilon), tEnd);
        }

        return n;

        static (float2 n, float wt) Avg(float2 n1, float wt1, float2 n2, float wt2)
        {
            var wt = wt1 + wt2;
            var n = MathTools.CheapRotationalLerp(n1, n2, wt2 / wt, out _);
            return (n, wt);
        }
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
        var result = LineCastToGround(p, castDir, out var d2);
        if (!float.IsFinite(d2))
        {
            result = ClosestPoint(p);
        }

        return result;
    }

    /// <summary> 
    /// Tries to find a point q on ground map such that q - p is parallel to castDir.
    /// If there are multiple such points, returns closest one.
    /// </summary>
    
    //2do: might right static bursted versions of some of these (the number of active points now is pretty low)
    public (int, float) LineCastToGround(float2 p, float2 castDir, out float bestSqDist)
    {
        bestSqDist = Mathf.Infinity;
        (int j, float s) = (0, 0);//best pt

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
                (j, s) = (i, 0);
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
                    (j, s) = (i, 0);
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
                    (j, s) = (i - 1, t);
                }
            }

            p0 = p1;
            a0 = a1;
        }

        if (j < EndRight)
        {
            s *= ArcLengthPos(j + 1) - ArcLengthPos(j);
        }
        return (j, s);
    }

    public (int, float) ClosestPoint(float2 p)
    {
        float bestSqDist;
        int j;
        float s;

        int i = EndLeft;
        var p0 = Point(i);
        bestSqDist = Vector2.SqrMagnitude(p0 - p);
        (j, s) = (i, 0);

        while (i < EndRight)
        {
            i++;
            var p1 = Point(i);
            var v = p1 - p0;
            var t = Vector2.Dot(p - p0, v) / Vector2.SqrMagnitude(v);
            if (!(t < 0) && !(t > 1))
            {
                float2 q = Vector2.Lerp(p0, p1, t);
                var d = q - p;
                var dSq = Vector2.SqrMagnitude(d);
                if (dSq < bestSqDist)
                {
                    bestSqDist = dSq;
                    (j, s) = (i - 1, t);
                }
            }
            else
            {
                var dSq = Vector2.SqrMagnitude(p1 - p);
                if (dSq < bestSqDist)
                {
                    bestSqDist = dSq;
                    (j, s) = (i, 0);
                }
            }

            p0 = p1;
        }

        if (j < EndRight)
        {
            s *= ArcLengthPos(j + 1) - ArcLengthPos(j);
        }
        return (j, s);
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
        jobHandle.Complete();
        CopyToReadableArrays();

        CaptureShapes(world, filter, origin);

        var job = new NewGroundMapUpdate(point, normal, arcLengthPos, endRight.native, endLeft.native, firstHitRight.native, firstHitLeft.native,
            shapeCapture, world, filter, origin, originUp, raycastLength, intervalWidth);

        //job.Run();
        //CopyToReadableArrays();

        jobHandle = job.Schedule();
    }

    public void DrawGizmos()
    {
        if (!readPoint.IsCreated) return;

        int i = EndLeft;
        Vector2 p = Point(i);
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(p, 0.025f);
        Gizmos.DrawLine(p, p + 0.25f * (Vector2)Normal(i));

        while (i < endRight.Value)
        {
            i++;
            Vector2 q = readPoint[i];
            Gizmos.DrawLine(p, q);
            Gizmos.DrawSphere(q, 0.025f);
            Gizmos.DrawLine(q, q + 0.25f * (Vector2)Normal(i));
            p = q;
        }

        Gizmos.color = Color.blue;
        Gizmos.DrawSphere((Vector2)Point(CentralIndex), 0.025f);
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