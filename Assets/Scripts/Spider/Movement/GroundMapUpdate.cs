using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.U2D.Physics;
using Unity.Mathematics;

[BurstCompile]
public struct GroundMapUpdate : IJob
{
    public NativeArray<GroundMapPt> map;
    public NativeReference<int> indexOfFirstGroundHitFromCenter;
    [ReadOnly] public PhysicsWorld world;//[ReadOnly] in the job so it is safe to be read by other threads during the job
    public PhysicsQuery.QueryFilter filter;
    public float2 origin;
    public float2 originDown;
    public float2 originRight;
    public float raycastLength;
    public float intervalWidth;
    public bool searchRightFirst;//just for now (won't be in parallel setup)

    int CentralIndex => map.Length / 2;

    public GroundMapUpdate(NativeArray<GroundMapPt> map, NativeReference<int> indexOfFirstGroundHitFromCenter, 
        PhysicsWorld world, PhysicsQuery.QueryFilter filter, float2 origin, float2 originDown, float2 originRight, 
        float raycastLength, float intervalWidth, bool searchRightFirst)
    {
        this.map = map;
        this.indexOfFirstGroundHitFromCenter = indexOfFirstGroundHitFromCenter;
        this.world = world;
        this.filter = filter;
        this.origin = origin;
        this.originDown = originDown;
        this.originRight = originRight;
        this.raycastLength = raycastLength;
        this.intervalWidth = intervalWidth;
        this.searchRightFirst = searchRightFirst;
    }

    public void Execute()
    {
        indexOfFirstGroundHitFromCenter.Value = -1;
        var castOuput = world.CastRay(origin, raycastLength * originDown, filter);

        if (castOuput.Length > 0)
        {
            var result = castOuput[0];
            map[CentralIndex] = new(result.point, result.normal, 0, result.fraction * raycastLength, true);
            indexOfFirstGroundHitFromCenter.Value = CentralIndex;
        }
        else
        {
            map[CentralIndex] = new(origin + raycastLength * originDown, -originDown, 0, raycastLength, false);
        }

        if (searchRightFirst)
        {
            FillRight();
            FillLeft();
        }
        else
        {
            FillLeft();
            FillRight();
        }

        if (indexOfFirstGroundHitFromCenter.Value < 0)
        {
            indexOfFirstGroundHitFromCenter.Value = CentralIndex;
        }
    }


    private void FillRight()
    {
        for (int i = map.Length / 2; i < map.Length - 1; i++)
        {
            //set map[i + 1]

            var lastMapPt = map[i];

            //first raycast horizontally to check if ground kicks UP suddenly (running into a wall e.g.)
            var castOuput = world.CastRay(lastMapPt.point + intervalWidth * lastMapPt.normal, 2 * intervalWidth * lastMapPt.Right, filter);

            if (castOuput.Length > 0)
            {
                var result = castOuput[0];
                map[i + 1] = new(result.point, result.normal, lastMapPt.arcLengthPosition + math.distance(lastMapPt.point, result.point),
                    result.fraction * 2 * intervalWidth, true);

                if (indexOfFirstGroundHitFromCenter.Value < 0)
                {
                    indexOfFirstGroundHitFromCenter.Value = i + 1;
                }
            }
            else
            {
                //now shift forward and raycast down
                var o = lastMapPt.point + lastMapPt.raycastDistance * lastMapPt.normal + intervalWidth * lastMapPt.Right;
                castOuput = world.CastRay(o, -raycastLength * lastMapPt.normal, filter);

                if (castOuput.Length > 0 && castOuput[0].fraction == 0)
                {
                    var y = intervalWidth;
                    while (y < lastMapPt.raycastDistance)
                    {
                        castOuput = world.CastRay(o - y * lastMapPt.normal, -raycastLength * lastMapPt.normal, filter);
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
                    var l = math.distance(lastMapPt.point, result.point);
                    if (intervalWidth < l * MathTools.sin15)//if l > ~3.86 * intervalWidth...
                    {
                        var u = ((float2)result.point - lastMapPt.point) / l;
                        castOuput = world.CastRay(lastMapPt.point + 0.25f * intervalWidth * u, 0.75f * intervalWidth * u, filter);

                        if (castOuput.Length > 0)
                        {
                            result = castOuput[0];
                            var dist = 0.25f * intervalWidth * (3 * result.fraction + 1);//(cast distance + 0.25f * intervalWidth)
                            var raycastDistance = (lastMapPt.raycastDistance - math.dot((float2)result.point - lastMapPt.point, lastMapPt.normal)) 
                                * math.dot(lastMapPt.normal, result.normal);
                            map[i + 1] = new(result.point, result.normal, lastMapPt.arcLengthPosition + dist,
                                raycastDistance, true);

                            if (indexOfFirstGroundHitFromCenter.Value < 0)
                            {
                                indexOfFirstGroundHitFromCenter.Value = i + 1;
                            }
                        }
                        else
                        {
                            var raycastDistance = (lastMapPt.raycastDistance - intervalWidth * math.dot(u, lastMapPt.normal)) * math.dot(u.CCWPerp(), lastMapPt.normal);
                            map[i + 1] = new(lastMapPt.point + intervalWidth * u, u.CCWPerp(), lastMapPt.arcLengthPosition + intervalWidth,
                                raycastDistance, false);
                        }
                    }
                    else
                    {
                        map[i + 1] = new(result.point, result.normal, lastMapPt.arcLengthPosition + l,
                            result.fraction * raycastLength * math.dot(lastMapPt.normal, result.normal), true);

                        if (indexOfFirstGroundHitFromCenter.Value < 0)
                        {
                            indexOfFirstGroundHitFromCenter.Value = i + 1;
                        }
                    }
                }
                else
                {
                    var l = math.min(lastMapPt.raycastDistance + 0.5f * intervalWidth, raycastLength);
                    castOuput = world.CastRay(o - l * lastMapPt.normal, -2 * intervalWidth * lastMapPt.Right, filter);

                    if (castOuput.Length > 0)
                    {
                        var result = castOuput[0];
                        map[i + 1] = new(result.point, result.normal, lastMapPt.arcLengthPosition + math.distance(lastMapPt.point, result.point),
                            result.fraction * 2 * intervalWidth, true);

                        if (indexOfFirstGroundHitFromCenter.Value < 0)
                        {
                            indexOfFirstGroundHitFromCenter.Value = i + 1;
                        }
                    }
                    else
                    {
                        o -= raycastLength * lastMapPt.normal;
                        map[i + 1] = new(o, lastMapPt.normal, lastMapPt.arcLengthPosition + math.distance(lastMapPt.point, o), raycastLength, false);
                    }
                }
            }
        }
    }

    void FillLeft()
    {
        for (int i = CentralIndex; i > 0; i--)
        {
            //set map[i - 1]

            var lastMapPt = map[i];

            //first raycast horizontally to check if ground kicks UP suddenly (running into a wall e.g.)
            var castOuput = world.CastRay(lastMapPt.point + intervalWidth * lastMapPt.normal, -2 * intervalWidth * lastMapPt.Right, filter);

            if (castOuput.Length > 0)
            {
                var result = castOuput[0];
                map[i - 1] = new(result.point, result.normal, lastMapPt.arcLengthPosition - math.distance(lastMapPt.point, result.point),
                    result.fraction * 2 * intervalWidth, true);

                if (indexOfFirstGroundHitFromCenter.Value < 0)
                {
                    indexOfFirstGroundHitFromCenter.Value = i - 1;
                }
            }
            else
            {
                //now shift forward and raycast down
                var o = lastMapPt.point + lastMapPt.raycastDistance * lastMapPt.normal - intervalWidth * lastMapPt.Right;
                castOuput = world.CastRay(o, -raycastLength * lastMapPt.normal, filter);

                if (castOuput.Length > 0 && castOuput[0].fraction == 0)
                {
                    var y = intervalWidth;
                    while (y < lastMapPt.raycastDistance)
                    {
                        castOuput = world.CastRay(o - y * lastMapPt.normal, -raycastLength * lastMapPt.normal, filter);
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
                    var l = math.distance(lastMapPt.point, result.point);
                    if (intervalWidth < l * MathTools.sin15)//if l > ~3.86 * intervalWidth...
                    {
                        var u = ((float2)result.point - lastMapPt.point) / l;
                        castOuput = world.CastRay(lastMapPt.point + 0.25f * intervalWidth * u, 0.75f * intervalWidth * u, filter);
                        if (castOuput.Length > 0)
                        {
                            result = castOuput[0];
                            var dist = 0.25f * intervalWidth * (3 * result.fraction + 1);//(cast distance + 0.25f * intervalWidth)
                            var raycastDistance = (lastMapPt.raycastDistance - math.dot((float2)result.point - lastMapPt.point, lastMapPt.normal)) * math.dot(lastMapPt.normal, result.normal);
                            map[i - 1] = new(result.point, result.normal, lastMapPt.arcLengthPosition - dist,
                                raycastDistance, true);

                            if (indexOfFirstGroundHitFromCenter.Value < 0)
                            {
                                indexOfFirstGroundHitFromCenter.Value = i - 1;
                            }
                        }
                        else
                        {
                            var raycastDistance = (lastMapPt.raycastDistance - intervalWidth * math.dot(u, lastMapPt.normal)) * math.dot(u.CCWPerp(), lastMapPt.normal);
                            map[i - 1] = new(lastMapPt.point + intervalWidth * u, u.CWPerp(), lastMapPt.arcLengthPosition - intervalWidth,
                                raycastDistance, false);
                        }
                    }
                    else
                    {
                        map[i - 1] = new(result.point, result.normal, lastMapPt.arcLengthPosition - l,
                            result.fraction * raycastLength * math.dot(lastMapPt.normal, result.normal), true);

                        if (indexOfFirstGroundHitFromCenter.Value < 0)
                        {
                            indexOfFirstGroundHitFromCenter.Value = i - 1;
                        }
                    }
                }
                else
                {
                    var l = math.min(lastMapPt.raycastDistance + 0.5f * intervalWidth, raycastLength);
                    castOuput = world.CastRay(o - l * lastMapPt.normal, 2 * intervalWidth * lastMapPt.Right, filter);
                    if (castOuput.Length > 0)
                    {
                        var result = castOuput[0];
                        map[i - 1] = new(result.point, result.normal, lastMapPt.arcLengthPosition - math.distance(lastMapPt.point, result.point),
                            result.fraction * 2 * intervalWidth, true);

                        if (indexOfFirstGroundHitFromCenter.Value < 0)
                        {
                            indexOfFirstGroundHitFromCenter.Value = i - 1;
                        }
                    }
                    else
                    {
                        o -= raycastLength * lastMapPt.normal;
                        map[i - 1] = new(o, lastMapPt.normal, lastMapPt.arcLengthPosition - math.distance(lastMapPt.point, o), raycastLength, false);
                    }
                }
            }
        }
    }
}