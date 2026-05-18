using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.U2D.Physics;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;

[BurstCompile]
public struct NewGroundMapUpdate : IJob
{
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
        NativeReference<int> endRight, NativeReference<int> endLeft, NativeReference<int> firstHitRight,NativeReference<int> firstHitLeft,
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

    public unsafe void Execute()
    {
        int* firstHitRight = this.firstHitRight.GetUnsafePtr();
        int* firstHitLeft = this.firstHitLeft.GetUnsafePtr();
        int* endRight = this.endRight.GetUnsafePtr();
        int* endLeft = this.endLeft.GetUnsafePtr();
        *endRight = point.Length - 1;
        *endLeft = 0;

        var castResults = world.CastRay(origin, -raycastLength * originUp, filter);

        if (SuccessfulCast(castResults))
        {
            *firstHitRight = CentralIndex;
            *firstHitLeft = CentralIndex;
            MapPolygonUntilEnd(CentralIndex, ref *endRight, 1, castResults[0]);
            MapPolygonUntilEnd(CentralIndex, ref *endLeft, -1, castResults[0]);
        }
        else
        {
            *firstHitRight = point.Length;
            *firstHitLeft = -1;
            var p0 = origin - raycastLength * originUp;
            point[CentralIndex] = p0;
            normal[CentralIndex] = originUp;
            arcLengthPos[CentralIndex] = 0;
            FillMapHalf(1, ref *endRight, ref *firstHitRight, p0);
            FillMapHalf(-1, ref *endLeft, ref *firstHitLeft, p0);
        }
    }

    //use this if central cast was unsuccessful
    private unsafe void FillMapHalf(int sign, ref int end, ref int firstHit, float2 p0)
    {
        var x = intervalWidth * originUp.CWPerp();
        var y = raycastLength * originUp;
        var o = p0 + y;

        int iter = 0;
        int itersEnd = sign * point.Length / 2;
        while (iter != itersEnd)
        {
            iter += sign;
            var castResults = world.CastRay(o + sign * iter * x , -y, filter);

            if (SuccessfulCast(castResults))
            {
                //first order of business - if the cast fraction was zero (e.g. we passed through a wall between last cast and this one)
                //then we cast horizontally from last cast ray towards this one, from bottom up (we know previous cast didn't hit)

                //now handle cast
                int i = CentralIndex + sign;
                if (iter != sign)
                {
                    //if more than one iteration happened, add a point just before the first successful hit
                    //so we have a long flat segment of ground to represent all the failed casts
                    point[i] = p0 + (iter - sign) * x;
                    normal[i] = originUp;
                    arcLengthPos[i] = (iter - sign) * intervalWidth;
                    i += sign;
                }

                firstHit = i;
                MapPolygonUntilEnd(i, ref end, sign, castResults[0]);
                return;
            }
        }

        //we got through the while loop without any hits; add a point to represent this long interval of flat ground
        point[CentralIndex + sign] = p0 + sign * iter * x;
        normal[CentralIndex + sign] = originUp;
        arcLengthPos[CentralIndex + sign] = sign * iter * intervalWidth;
        end = CentralIndex + sign;
    }

    bool SuccessfulCast(NativeArray<PhysicsQuery.WorldCastResult> castResults)
    {
        if (castResults.Length == 0)
        {
            return false;
        }

        var id = castResults[0].shape.Id();
        return PhysicsCoreHelper.ShapeProxyForJobs.ShapeValid(id, shapeCapture) && shapeCapture[id].ShapeType == PhysicsShape.ShapeType.Polygon;
    }

    private (PhysicsCoreHelper.ShapeProxyForJobs shapeProxy, PhysicsTransform transform, int edge, int count, float signedDist) 
        GetDataFromSuccessfulCast(PhysicsQuery.WorldCastResult castResult)
    {
        var poly = shapeCapture[castResult.shape.Id()];
        var transform = castResult.shape.transform;
        var polyVertex = poly.VertexArray;
        var polyNormal = poly.NormalArray;

        var p = castResult.point;
        var pLocal = transform.InverseTransformPoint(p);

        var (jRight, ct, signedDist) = CollisionUtilities.ClosestEdge(pLocal, polyVertex, polyNormal);
        return (poly, transform, jRight, ct, signedDist);
    }

    private void MapPolygonUntilEnd(int i, ref int end, int sign, PhysicsQuery.WorldCastResult hit)
    {
        int k = 0;
        while (i != end && k < 100)
        {
            k++;
            hit = MapPolygon(ref i, ref end, sign, hit);
        }
    }

    //when we first hit a polygon, we'll travel from vertex to vertex along the polygon until we hit another shape or fill the array
    private PhysicsQuery.WorldCastResult MapPolygon(ref int i, ref int end, int sign, PhysicsQuery.WorldCastResult hit)
    {
        var (shapeProxy, transform, edge, ct, signedDist) = GetDataFromSuccessfulCast(hit);
        var polyVertex = shapeProxy.VertexArray;
        var polyNormal = shapeProxy.NormalArray;
        float2 n = transform.rotation.RotateVector(polyNormal[edge]);
        var pt = (float2)hit.point + signedDist * n;
        point[i] = pt;
        normal[i] = n;
        var s = math.select(0, arcLengthPos[i - sign] + sign * math.distance(point[i - sign], pt), i == CentralIndex);
        arcLengthPos[i] = s;

        while (i != end)
        {
            var nextEdge = math.select(Left(edge, ct), Right(edge, ct), sign > 0);
            float2 nextVertex = transform.TransformPoint(polyVertex[nextEdge]);
            var edgeDir = sign * n.CWPerp();
            var xBuffer = 0.001f * edgeDir;
            var d = nextVertex - pt;
            var cast = world.CastRay(pt + 0.05f * n - xBuffer, d + 2 * xBuffer, filter);
            if (SuccessfulCast(cast))//2DO: this can and does get you stuck in an infinite loop
            {
                return cast[0];
            }

            i += sign;
            edge = nextEdge;
            pt = nextVertex;
            n = transform.rotation.RotateVector(polyNormal[edge]);
            point[i] = pt;
            normal[i] = n;
            s += sign * math.max(math.dot(d, edgeDir), 0);
            arcLengthPos[i] = s;

            if (sign * s > arcLengthMax)
            {
                end = i;
                return default;
            }
        }

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