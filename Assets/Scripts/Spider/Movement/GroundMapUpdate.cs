using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.U2D.Physics;
using Unity.Mathematics;

[BurstCompile]
public struct GroundMapUpdate : IJob
{
    public NativeArray<float2> point;
    public NativeArray<float2> normal;
    public NativeArray<float> arcLengthPos;
    public NativeArray<float> raycastDistance;
    public NativeArray<bool> hitGround;
    [ReadOnly] public PhysicsWorld world;
    public PhysicsQuery.QueryFilter filter;
    public float2 origin;
    public float2 originDown;
    public float2 originRight;
    public NativeReference<int> indexOfFirstGroundHitFromCenter;
    public float raycastLength;
    public float intervalWidth;
    public bool searchRightFirst;

    int CentralIndex => point.Length / 2;

    public GroundMapUpdate(NativeArray<float2> point, NativeArray<float2> normal, NativeArray<float> arcLengthPos, NativeArray<float> raycastDistance, 
        NativeArray<bool> hitGround, PhysicsWorld world, PhysicsQuery.QueryFilter filter, float2 origin, float2 originDown, float2 originRight, 
        NativeReference<int> indexOfFirstGroundHitFromCenter, float raycastLength, float intervalWidth, bool searchRightFirst)
    {
        this.point = point;
        this.normal = normal;
        this.arcLengthPos = arcLengthPos;
        this.raycastDistance = raycastDistance;
        this.hitGround = hitGround;
        this.world = world;
        this.filter = filter;
        this.origin = origin;
        this.originDown = originDown;
        this.originRight = originRight;
        this.indexOfFirstGroundHitFromCenter = indexOfFirstGroundHitFromCenter;
        this.raycastLength = raycastLength;
        this.intervalWidth = intervalWidth;
        this.searchRightFirst = searchRightFirst;
    }

    public static void SetMapPoint(int i, NativeArray<float2> pointArr, NativeArray<float2> normalArr, NativeArray<float> arcLengthPosArr,
        NativeArray<float> raycastDistanceArr, NativeArray<bool> hitGroundArr,
        float2 point, float2 normal, float arcLengthPos, float raycastDistance, bool hitGround)
    {
        pointArr[i] = point;
        normalArr[i] = normal;
        arcLengthPosArr[i] = arcLengthPos;
        raycastDistanceArr[i] = raycastDistance;
        hitGroundArr[i] = hitGround;
    }

    public void Execute()
    {
        indexOfFirstGroundHitFromCenter.Value = -1;
        var castOuput = world.CastRay(origin, raycastLength * originDown, filter);

        if (castOuput.Length > 0)
        {
            var result = castOuput[0];
            SetMapPoint(CentralIndex, point, normal, arcLengthPos, raycastDistance, hitGround, 
                result.point, result.normal, 0, result.fraction * raycastLength, true);
            indexOfFirstGroundHitFromCenter.Value = CentralIndex;
        }
        else
        {
            SetMapPoint(CentralIndex, point, normal, arcLengthPos, raycastDistance, hitGround,
                origin + raycastLength * originDown, -originDown, 0, raycastLength, false);
        }

        FillMapHalf(searchRightFirst);
        FillMapHalf(!searchRightFirst);

        if (indexOfFirstGroundHitFromCenter.Value < 0)
        {
            indexOfFirstGroundHitFromCenter.Value = CentralIndex;
        }
    }

    private void FillMapHalf(bool right)
    {
        int sign = math.select(-1, 1, right);
        int end = math.select(0, point.Length - 1, right);

        int i = CentralIndex;
        while (i != end)
        {
            //set map[iNext]
            var iNext = i + sign;
            var lastPt = point[i];
            var lastNormal = normal[i];
            var lastTangent = math.select(lastNormal.CCWPerp(), lastNormal.CWPerp(), right);
            var lastArcLength = arcLengthPos[i];
            var lastRaycastDistance = raycastDistance[i];

            //first raycast horizontally to check if ground kicks UP suddenly (running into a wall e.g.)
            var castOuput = world.CastRay(lastPt + intervalWidth * lastNormal, 2 * intervalWidth * lastTangent, filter);

            if (castOuput.Length > 0)
            {
                var result = castOuput[0];
                SetMapPoint(iNext, point, normal, arcLengthPos, raycastDistance, hitGround,
                    result.point, result.normal, lastArcLength + sign * math.distance(lastPt, result.point),
                    result.fraction * 2 * intervalWidth, true);

                if (indexOfFirstGroundHitFromCenter.Value < 0)
                {
                    indexOfFirstGroundHitFromCenter.Value = iNext;
                }
            }
            else
            {
                //now shift forward and raycast down
                var o = lastPt + lastRaycastDistance * lastNormal + intervalWidth * lastTangent;
                castOuput = world.CastRay(o, -raycastLength * lastNormal, filter);

                if (castOuput.Length > 0 && castOuput[0].fraction == 0)
                {
                    var y = intervalWidth;
                    while (y < lastRaycastDistance)
                    {
                        castOuput = world.CastRay(o - y * lastNormal, -raycastLength * lastNormal, filter);
                        if (castOuput.Length == 0 || castOuput[0].fraction > 0)
                        {
                            break;
                        }

                        y += intervalWidth;
                    }
                }

                if (castOuput.Length > 0)
                {
                    var result = castOuput[0];
                    var l = math.distance(lastPt, result.point);
                    if (intervalWidth < l * MathTools.sin15)//if l > ~3.86 * intervalWidth...
                    {
                        var u = ((float2)result.point - lastPt) / l;
                        castOuput = world.CastRay(lastPt + 0.25f * intervalWidth * u, 0.75f * intervalWidth * u, filter);

                        if (castOuput.Length > 0)
                        {
                            result = castOuput[0];
                            var dist = 0.25f * intervalWidth * (3 * result.fraction + 1);//(cast distance + 0.25f * intervalWidth)
                            var castDistance = (lastRaycastDistance - math.dot((float2)result.point - lastPt, lastNormal))
                                * math.dot(lastNormal, result.normal);
                            SetMapPoint(iNext, point, normal, arcLengthPos, raycastDistance, hitGround,
                                result.point, result.normal, lastArcLength + sign * dist, castDistance, true);

                            if (indexOfFirstGroundHitFromCenter.Value < 0)
                            {
                                indexOfFirstGroundHitFromCenter.Value = iNext;
                            }
                        }
                        else
                        {
                            var castDistance = (lastRaycastDistance - intervalWidth * math.dot(u, lastNormal))
                                * math.dot(u.CCWPerp(), lastNormal);
                            SetMapPoint(iNext, point, normal, arcLengthPos, raycastDistance, hitGround,
                                lastPt + intervalWidth * u, u.CCWPerp(), lastArcLength + sign * intervalWidth, castDistance, false);
                        }
                    }
                    else
                    {
                        SetMapPoint(iNext, point, normal, arcLengthPos, raycastDistance, hitGround,
                            result.point, result.normal, lastArcLength + sign * l,
                            result.fraction * raycastLength * math.dot(lastNormal, result.normal), true);

                        if (indexOfFirstGroundHitFromCenter.Value < 0)
                        {
                            indexOfFirstGroundHitFromCenter.Value = iNext;
                        }
                    }
                }
                else
                {
                    var l = math.min(lastRaycastDistance + 0.5f * intervalWidth, raycastLength);
                    castOuput = world.CastRay(o - l * lastNormal, -2 * intervalWidth * lastTangent, filter);

                    if (castOuput.Length > 0)
                    {
                        var result = castOuput[0];
                        SetMapPoint(iNext, point, normal, arcLengthPos, raycastDistance, hitGround,
                            result.point, result.normal, lastArcLength + sign * math.distance(lastPt, result.point),
                            result.fraction * 2 * intervalWidth, true);

                        if (indexOfFirstGroundHitFromCenter.Value < 0)
                        {
                            indexOfFirstGroundHitFromCenter.Value = iNext;
                        }
                    }
                    else
                    {
                        o -= raycastLength * lastNormal;
                        SetMapPoint(iNext, point, normal, arcLengthPos, raycastDistance, hitGround,
                            o, lastNormal, lastArcLength + sign * math.distance(lastPt, o), raycastLength, false);
                    }
                }
            }

            i = iNext;
        }
    }
}