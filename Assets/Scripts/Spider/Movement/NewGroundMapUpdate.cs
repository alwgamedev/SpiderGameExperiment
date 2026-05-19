using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.U2D.Physics;
using UnityEngine;

[BurstCompile]
public struct NewGroundMapUpdate : IJob
{
    //want to keep the edge buffer big enough to prevent self-hits,
    //and the cast length buffer small-ish, while having the poly angle min small enough to accommodate almost any angle we come across
    const float POLY_EDGE_BUFFER = 0.0025f;//1f / 256
    const float POLY_CAST_LENGTH_BUFFER_MAX = 0.1f;
    const float POLY_ANGLE_MIN = POLY_EDGE_BUFFER / (POLY_CAST_LENGTH_BUFFER_MAX - POLY_EDGE_BUFFER);//~0.025 which let's us go down to angle of ~1.5 deg

    const float CIRCLE_ANGLE_STEP_MAX = 0.125f * math.PI;
    const float CIRCLE_EDGE_BUFFER = 0.0025f;
    const float CIRCLE_EDGE_BUFFER_HELPER = 1.02f;
    //^helper should be at least sqrt(2) / sqrt(1 + cos(angleStepMax))
    //(see use of helper below)

    public NativeArray<float2> point;
    public NativeArray<float2> normal;
    public NativeArray<float> arcLengthPos;
    [ReadOnly] public NativeArray<PhysicsCoreHelper.ShapeProxyForJobs> shapeCapture;
    public NativeReference<int> endRight;
    public NativeReference<int> endLeft;
    public NativeReference<int> firstHitRight;
    public NativeReference<int> firstHitLeft;
    public readonly PhysicsQuery.QueryFilter filter;
    [ReadOnly] public PhysicsWorld world;
    public readonly float2 origin;
    public readonly float2 originUp;
    public readonly float raycastLength;
    public readonly float intervalWidth;
    public readonly float arcLengthMax;

    int CentralIndex => point.Length / 2;

    public NewGroundMapUpdate(NativeArray<float2> point, NativeArray<float2> normal, NativeArray<float> arcLengthPos,
        NativeReference<int> endRight, NativeReference<int> endLeft, NativeReference<int> firstHitRight, NativeReference<int> firstHitLeft,
        NativeArray<PhysicsCoreHelper.ShapeProxyForJobs> shapeCapture, PhysicsWorld world, PhysicsQuery.QueryFilter filter,
        float2 origin, float2 originUp, float raycastLength, float intervalWidth)
    {
        this.point = point;
        this.normal = normal;
        this.arcLengthPos = arcLengthPos;
        this.endRight = endRight;
        this.endLeft = endLeft;
        this.firstHitRight = firstHitRight;
        this.firstHitLeft = firstHitLeft;
        this.shapeCapture = shapeCapture;
        this.world = world;
        this.filter = filter;
        this.origin = origin;
        this.originUp = originUp;
        this.raycastLength = raycastLength;
        this.intervalWidth = intervalWidth;
        arcLengthMax = intervalWidth * point.Length / 2;
    }

    public void Execute()
    {
        var endRightLocal = point.Length - 1;
        var endLeftLocal = 0;

        var castResults = world.CastRay(origin, -raycastLength * originUp, filter);

        if (SuccessfulCast(castResults))
        {
            firstHitRight.Value = CentralIndex;
            firstHitLeft.Value = CentralIndex;
            ChaseHit(CentralIndex, ref endRightLocal, 1, castResults[0]);
            ChaseHit(CentralIndex, ref endLeftLocal, -1, castResults[0]);
        }
        else
        {
            var p0 = origin - raycastLength * originUp;
            point[CentralIndex] = p0;
            normal[CentralIndex] = originUp;
            arcLengthPos[CentralIndex] = 0;

            var firstHitRightLocal = point.Length;
            var firstHitLeftLocal = -1;
            FillMapHalf(1, ref endRightLocal, ref firstHitRightLocal, p0);
            FillMapHalf(-1, ref endLeftLocal, ref firstHitLeftLocal, p0);

            firstHitRight.Value = firstHitRightLocal;
            firstHitLeft.Value = firstHitLeftLocal;
        }

        endRight.Value = endRightLocal;
        endLeft.Value = endLeftLocal;
    }

    //use this if central cast was unsuccessful
    private void FillMapHalf(int sign, ref int end, ref int firstHit, float2 p0)
    {
        var x = intervalWidth * originUp.CWPerp();
        var y = raycastLength * originUp;
        var o = p0 + y;

        int iter = 0;
        int itersEnd = sign * point.Length / 2;
        while (iter != itersEnd)
        {
            iter += sign;
            var castResults = world.CastRay(o + iter * x, -y, filter);

            if (SuccessfulCast(castResults))
            {
                int i = CentralIndex + sign;
                if (iter != sign)
                {
                    //if more than one iteration happened, add a point just before the first successful hit
                    //to have a long flat segment of ground for all the failed casts
                    point[i] = p0 + (iter - sign) * x;
                    normal[i] = originUp;
                    arcLengthPos[i] = (iter - sign) * intervalWidth;
                    i += sign;
                }

                firstHit = i;
                ChaseHit(i, ref end, sign, castResults[0]);
                return;
            }
        }

        //we got through the loop without any hits; add a point to represent this long interval of flat ground
        point[CentralIndex + sign] = p0 + iter * x;
        normal[CentralIndex + sign] = originUp;
        arcLengthPos[CentralIndex + sign] = iter * intervalWidth;
        end = CentralIndex + sign;
    }

    private readonly bool SuccessfulCast(NativeArray<PhysicsQuery.WorldCastResult> castResults)
    {
        return castResults.Length > 0 && PhysicsCoreHelper.ShapeProxyForJobs.ShapeValid(castResults[0].shape.Id(), shapeCapture);
    }

    private void ChaseHit(int i, ref int end, int sign, PhysicsQuery.WorldCastResult hit)
    {
        while (i != end + sign)
        {
            hit = MapHit(ref i, ref end, sign, hit);
        }
    }

    private PhysicsQuery.WorldCastResult MapHit(ref int i, ref int end, int sign, PhysicsQuery.WorldCastResult hit)
    {
        switch (shapeCapture[hit.shape.Id()].ShapeType)
        {
            case PhysicsShape.ShapeType.Polygon:
                return MapPolygon(ref i, ref end, sign, hit);
            case PhysicsShape.ShapeType.Circle:
                return MapCircle(ref i, ref end, sign, hit);
            case PhysicsShape.ShapeType.Capsule:
                return MapCapsule(ref i, ref end, sign, hit);
            default:
                end = i;
                i = end + sign;
                return default;
        }
    }

    private (PhysicsCoreHelper.ShapeProxyForJobs shapeProxy, PhysicsTransform transform, int edge, int count, float signedDist)
        GetDataFromPolygonHit(PhysicsQuery.WorldCastResult hit)
    {
        var poly = shapeCapture[hit.shape.Id()];
        var transform = hit.shape.transform;
        var polyVertex = poly.VertexArray;
        var polyNormal = poly.NormalArray;

        var p = hit.point;
        var pLocal = transform.InverseTransformPoint(p);

        var (jRight, ct, signedDist) = CollisionUtilities.ClosestEdge(pLocal, polyVertex, polyNormal);
        return (poly, transform, jRight, ct, signedDist);
    }

    private PhysicsQuery.WorldCastResult MapPolygon(ref int i, ref int end, int sign, PhysicsQuery.WorldCastResult hit)
    {
        var (shapeProxy, transform, edge, ct, signedDist) = GetDataFromPolygonHit(hit);
        var polyVertex = shapeProxy.VertexArray;
        var polyNormal = shapeProxy.NormalArray;
        float2 n = transform.rotation.RotateVector(polyNormal[edge]);
        var pt = (float2)hit.point + signedDist * n;
        Debug.DrawLine((Vector2)pt, (Vector2)(pt + 0.25f * n), Color.orange);

        point[i] = pt;
        normal[i] = n;
        var s = math.select(arcLengthPos[i - sign] + sign * math.distance(point[i - sign], pt), 0, i == CentralIndex);
        arcLengthPos[i] = s;
        float2 prevCastEnd = pt + POLY_EDGE_BUFFER * n;

        while (i != end)
        {
            //we'll cast towards targetVertex
            var targetVertexIndex = math.select(Left(edge, ct), edge, sign > 0);
            float2 targetVertex = transform.TransformPoint(polyVertex[targetVertexIndex]);
            var edgeDir = sign * n.CWPerp();

            //add a little extra buffer to cast length to make sure the next cast (which starts at this cast's endpoint)
            //stays outside of the next edge
            var nextEdge = math.select(Left(edge, ct), Right(edge, ct), sign > 0);
            var nextEdgeNormal = transform.rotation.RotateVector(polyNormal[nextEdge]);
            var nextEdgeDir = sign * nextEdgeNormal.CWPerp();
            var sin = math.dot(n, nextEdgeDir);//-sine of the next angle in the polygon
            var cos = math.dot(edgeDir, nextEdgeDir);
            if (cos < 0 && math.abs(sin) < POLY_ANGLE_MIN)
            {
                //if next angle in the polygon is extremely sharp, our xBuffer would be unreasonably large, so end mapping
                end = i;
                i = end + sign;
                return default;
            }

            var x = math.select(POLY_EDGE_BUFFER * (1 + cos / sin), POLY_EDGE_BUFFER, cos > 0);
            var xBuffer = x * edgeDir;

            i += sign;
            float2 o = prevCastEnd;
            float2 t = targetVertex + POLY_EDGE_BUFFER * n - prevCastEnd + xBuffer;
            var cast = world.CastRay(o, t, filter);
            prevCastEnd = o + t;
            Debug.DrawLine((Vector2)o, (Vector2)(o + t), Color.rebeccaPurple);

            if (SuccessfulCast(cast) && cast[0].fraction > 0)//check fraction > 0, because sometimes first cast is overlapping the previous shape we came from
            {
                return cast[0];
            }

            s += sign * math.max(math.dot(targetVertex - pt, edgeDir), 0);
            edge = nextEdge;
            n = nextEdgeNormal;
            pt = targetVertex;
            point[i] = pt;
            normal[i] = n;
            arcLengthPos[i] = s;

            if (sign * s > arcLengthMax)
            {
                end = i;
                i = end + sign;
                return default;
            }
        }

        i += sign;
        return default;
    }

    private PhysicsQuery.WorldCastResult MapCircle(ref int i, ref int end, int sign, PhysicsQuery.WorldCastResult hit)
    {
        var circleShape = hit.shape;
        var shapeProxy = shapeCapture[circleShape.Id()];
        float2 center = hit.shape.transform.TransformPoint(shapeProxy.Center1);
        var radius = shapeProxy.Radius;
        var bigR = CIRCLE_EDGE_BUFFER_HELPER * (radius + CIRCLE_EDGE_BUFFER);

        var n = math.normalize((float2)hit.point - center);
        var pt = center + radius * n;
        point[i] = pt;
        normal[i] = n;
        var s = math.select(arcLengthPos[i - sign] + sign * math.distance(point[i - sign], pt), 0, i == CentralIndex);
        arcLengthPos[i] = s;

        var stepRotY = -sign * math.min(intervalWidth / radius, CIRCLE_ANGLE_STEP_MAX);//want steps to have arclength = intervalWidth
        var stepRotX = math.sqrt(math.max(1 - stepRotY * stepRotY, 0));
        var stepRotation = new PhysicsRotate(new float2(stepRotX, stepRotY));
        var arcLengthStep = -radius * stepRotY;

        while (i != end)
        {
            i += sign;

            float2 nextNormal = stepRotation.RotateVector(n);
            var o = center + bigR * n;
            var t = bigR * (nextNormal - n);
            Debug.DrawLine((Vector2)o, (Vector2)(o + t), Color.rebeccaPurple);
            var cast = world.CastRay(o, t, filter);

            //check fraction > 0, because sometimes the first cast is overlapping with the shape we just came from
            if (SuccessfulCast(cast) && cast[0].fraction > 0)
            {
                return cast[0];
            }

            n = nextNormal;
            s += arcLengthStep;
            point[i] = center + radius * n;
            normal[i] = n;
            arcLengthPos[i] = s;

            if (sign * s > arcLengthMax)
            {
                end = i;
                i = end + sign;
                return default;
            }
        }

        i += sign;
        return default;
    }

    /// <summary> -1 = circle1, 0 = box, 1 = circle2 </summary>
    private static (int seg, float uCoord) CapsuleSegment(float2 pointRelToCenter1, float width, float2 u)
    {
        var uCoord = math.dot(pointRelToCenter1, u);
        int seg = math.select(math.select(0, -1, uCoord < 0), 1, uCoord > width);
        return (seg, uCoord);
    }

    private static (float2 pt, float2 n) ClosestPointOnCapsule(float2 point, int seg, float uCoord, float2 u, float2 v, float2 center1, float2 center2, float radius)
    {
        switch (seg)
        {
            case 1://closest to circle2
                {
                    var n = math.normalize(point - center2);
                    return (center2 + radius * n, n);
                }
            case -1://closest to circle1
                {
                    var n = math.normalize(point - center1);
                    return (center1 + radius * n, n);
                }
            default://closest to box
                {
                    var n = math.select(-v, v, math.dot(point - center1, v) > 0);
                    var pt = center1 + uCoord * u + radius * n;
                    return (pt, n);
                }
        }
    }

    private PhysicsQuery.WorldCastResult MapCapsule(ref int i, ref int end, int sign, PhysicsQuery.WorldCastResult hit)
    {
        var capsuleShape = hit.shape;
        var transform = capsuleShape.transform;
        var shapeProxy = shapeCapture[capsuleShape.Id()];
        float2 center1 = transform.TransformPoint(shapeProxy.Center1);
        float2 center2 = transform.TransformPoint(shapeProxy.Center2);
        var radius = shapeProxy.Radius;
        var bigR = CIRCLE_EDGE_BUFFER_HELPER * (radius + CIRCLE_EDGE_BUFFER);
        var radiusBuffer = bigR - radius;

        var width = math.distance(center1, center2);
        var u = (center2 - center1) / width;
        var v = u.CCWPerp();

        var stepRotY = -sign * math.min(intervalWidth / radius, CIRCLE_ANGLE_STEP_MAX);//want steps to have arclength = intervalWidth
        var stepRotX = math.sqrt(math.max(1 - stepRotY * stepRotY, 0));
        var stepRotation = new PhysicsRotate(new float2(stepRotX, stepRotY));
        var arcLengthStep = -radius * stepRotY;

        //set initial point;
        var (seg, uCoord) = CapsuleSegment((float2)hit.point - center1, width, u);
        var (pt, n) = ClosestPointOnCapsule(hit.point, seg, uCoord, u, v, center1, center2, radius);
        Debug.DrawLine((Vector2)pt, (Vector2)(pt + 0.25f * n), Color.orange);
        var s = math.select(arcLengthPos[i - sign] + sign * math.distance(point[i - sign], pt), 0, i == CentralIndex);
        point[i] = pt;
        normal[i] = n;
        arcLengthPos[i] = s;
        var prevCastEnd = pt + radiusBuffer * n;

        while (i != end)
        {
            i += sign;

            switch (seg)
            {
                case 0://box case
                    {
                        var topSign = math.select(-1, 1, math.dot(prevCastEnd - center1, v) > 0);
                        var headedTowardsCenter2 = sign * topSign > 0;
                        var o = prevCastEnd;
                        var t = math.select(-uCoord - radiusBuffer, width - uCoord + radiusBuffer, headedTowardsCenter2);
                        prevCastEnd = o + t * u;
                        Debug.DrawLine((Vector2)o, (Vector2)prevCastEnd, Color.rebeccaPurple);
                        var cast = world.CastRay(o, t * u, filter);

                        if (SuccessfulCast(cast) && cast[0].fraction > 0)
                        {
                            return cast[0];
                        }

                        //we'll handle cast results right here
                        n = math.select(-v, v, topSign > 0);
                        pt = math.select(center1, center2, headedTowardsCenter2) + radius * n;
                        s += sign * (math.abs(t) - radiusBuffer);
                        seg = math.select(-1, 1, headedTowardsCenter2);//force set next seg to one of the circles

                        point[i] = pt;
                        normal[i] = n;
                        arcLengthPos[i] = s;
                    }
                    break;
                default://circle case
                    {
                        n = stepRotation.RotateVector(n);
                        var exitCircle = seg * math.dot(n, u) < 0;

                        //clamp n to avoid cutting through box with cast
                        var clampedNormal = math.select(-v, v, math.dot(n, v) > 0);
                        n = math.select(n, clampedNormal, exitCircle);

                        var center = math.select(center1, center2, seg > 0);
                        var o = prevCastEnd;
                        prevCastEnd = center + bigR * n;
                        Debug.DrawLine((Vector2)o, (Vector2)prevCastEnd, Color.rebeccaPurple);
                        var cast = world.CastRay(o, prevCastEnd - o, filter);

                        if (SuccessfulCast(cast) && cast[0].fraction > 0)
                        {
                            return cast[0];
                        }


                        uCoord = math.select(0, width, seg > 0); 
                        seg = math.select(seg, 0, exitCircle);
                        pt = center + radius * n;
                        s += arcLengthStep;//inaccurate when exiting circle, but with small interval width the error isn't worth worrying about
                        point[i] = pt;
                        normal[i] = n;
                        arcLengthPos[i] = s;
                    }
                    break;
            }


            if (sign * s > arcLengthMax)
            {
                end = i;
                i += sign;
                return default;
            }
        }

        i += sign;
        return default;
    }

    static int Right(int j, int ct)
    {
        return math.select(ct - 1, j - 1, j > 0);
    }

    static int Left(int j, int ct)
    {
        return math.select(0, j + 1, j < ct - 1);
    }
}