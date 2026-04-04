using Unity.Collections;
using Unity.Mathematics;
using Unity.U2D.Physics;

public static class RopeJobUtils
{
    //MOVEMENT
    public static void Anchor(int i, NativeArray<float2> position, NativeArray<float2> lastPosition)
    {
        lastPosition[i] = position[i];
    }

    public static void MoveAndAnchorTerminus(float2 deltaPosition, NativeArray<float2> position, NativeArray<float2> lastPosition,
        NativeReference<PhysicsShape> terminusAnchor, NativeReference<float2> terminusAnchorLocalPos, NativeReference<FastRope.TerminusAnchorMode> terminusAnchorMode,
        PhysicsWorld world, PhysicsQuery.QueryFilter collisionFilter, float nodeRadius, float collisionBounciness, bool stepVelocity)
    {
        MoveNode(position.Length - 1, deltaPosition, position, lastPosition,
            world, collisionFilter, nodeRadius, collisionBounciness, stepVelocity, out var castResults);

        if (castResults.IsCreated && castResults.Length > 0)
        {
            var result = castResults[0];
            var p = result.point + nodeRadius * result.normal;
            position[^1] = p;
            lastPosition[^1] = p;//kill velocity

            var shape = result.shape;
            terminusAnchor.Value = shape;
            terminusAnchorLocalPos.Value = shape.body.transform.InverseTransformPoint(p);
            if (shape.body.type == PhysicsBody.BodyType.Dynamic)
            {
                terminusAnchorMode.Value = FastRope.TerminusAnchorMode.dynamicAnchor;
            }
            else
            {
                terminusAnchorMode.Value = FastRope.TerminusAnchorMode.staticAnchor;
            }
        }
    }

    public static void MoveNode(int i, float2 deltaPosition, NativeArray<float2> position, NativeArray<float2> lastPosition,
        PhysicsWorld world, PhysicsQuery.QueryFilter collisionFilter, float nodeRadius, float collisionBounciness, bool stepVelocity,
        out NativeArray<PhysicsQuery.WorldCastResult> castResults)
    {
        if (math.lengthsq(deltaPosition) < MathTools.o91)
        {
            castResults = default;
            return;
        }

        var circle = new CircleGeometry()
        {
            center = position[i],
            radius = nodeRadius
        };

        castResults = world.CastGeometry(circle, deltaPosition, collisionFilter);

        //discovery: i think cast result always has a (valid) unit normal, unless there was initial overlap, in which case we can use position - result.point as the normal
        //(+/- depending on whether center of node is submerged)
        if (castResults.Length > 0)
        {
            var result = castResults[0];
            var positionAtTimeOfImpact = position[i] + result.fraction * deltaPosition;

            float2 normal = result.normal;
            if (normal.Equals(0))//there was initial overlap in the world cast
            {
                normal = math.select(positionAtTimeOfImpact - (float2)result.point,
                    (float2)result.point - positionAtTimeOfImpact,
                    world.TestOverlapPoint(positionAtTimeOfImpact, collisionFilter));
                normal = normal.Normalized();
                //if (world.TestOverlapPoint(positionAtTimeOfImpact, collisionFilter))
                //{
                //    normal = ((float2)result.point - positionAtTimeOfImpact).Normalized();

                //    //according to comment in WorldCastResult source, when there is initial overlap the result.point
                //    //is an "arbitrary point in the overlap," so if node position is also submerged, we don't know if normal is actually pointing towards an escape route
                //    //(we should actually test this, since this seemed to have no affect on the functionality of the rope)
                //    int j = 0;
                //    while (j < 8)
                //    {
                //        var p = EdgePoint(j);
                //        if (!world.TestOverlapPoint(p, collisionFilter))
                //        {
                //            normal = (p - (float2)result.point).Normalized();
                //            break;
                //        }

                //        j++;
                //    }

                //    float2 EdgePoint(int j)
                //    {
                //        int stretch = j / 4 + 1;
                //        j %= 4;
                //        return j switch
                //        {
                //            0 => positionAtTimeOfImpact + stretch * nodeRadius * normal,
                //            1 => positionAtTimeOfImpact - stretch * nodeRadius * normal,
                //            2 => positionAtTimeOfImpact + stretch * nodeRadius * normal.CCWPerp(),
                //            _ => positionAtTimeOfImpact + stretch * nodeRadius * normal.CWPerp(),
                //        };
                //    }
                //}
                //else
                //{
                //    normal = (positionAtTimeOfImpact - (float2)result.point).Normalized();
                //}
            }

            if (math.dot(deltaPosition, normal) < 0)
            {
                deltaPosition = -collisionBounciness * (deltaPosition - 2 * (-deltaPosition.x * normal.y + deltaPosition.y * normal.x) * new float2(-normal.y, normal.x));
            }

            position[i] = positionAtTimeOfImpact + (1 - result.fraction) * deltaPosition;
            if (stepVelocity)
            {
                lastPosition[i] = position[i] - deltaPosition;
            }
        }
        else
        {
            if (stepVelocity)
            {
                lastPosition[i] = position[i];
            }
            position[i] += deltaPosition;
        }
    }


    //CONSTRAINTS

    /// <summary> (where i = sourceIndex + 1) </summary>
    public static void CalculateFirstConstraint(int i, NativeArray<float2> position, NativeArray<float2> lastPosition, NativeArray<float2> constraintDelta,
        float nodeSpacing, float nodeSpacing2, float nodeMass, float sourceMass, float constraintStiffness)
    {
        var d = position[i] - position[i - 1];
        var l = math.lengthsq(d);

        if (l > nodeSpacing2)
        {
            var c = constraintStiffness * (1 - nodeSpacing * math.rsqrt(l)) * d;

            if (math.isinf(sourceMass))
            {
                constraintDelta[i] -= c;
            }
            else
            {
                var c1 = nodeMass / (nodeMass + sourceMass) * c;
                constraintDelta[i - 1] += c1;
                constraintDelta[i] += c1 - c;
            }
        }
    }

    public static void CalculateConstraint(int i, NativeArray<float2> position, NativeArray<float2> lastPosition, NativeArray<float2> constraintDelta,
        float nodeSpacing, float nodeSpacing2, float constraintStiffness)
    {
        var d = position[i] - position[i - 1];
        var l = math.lengthsq(d);

        if (l > nodeSpacing2)
        {
            var c = 0.5f * constraintStiffness * (1 - nodeSpacing * math.rsqrt(l)) * d;
            constraintDelta[i - 1] += c;
            constraintDelta[i] -= c;
        }
    }

    /// <summary> (where i = terminusIndex) </summary>
    public static void CalculateLastConstraint(int i, NativeArray<float2> position, NativeArray<float2> lastPosition, NativeArray<float2> constraintDelta,
        float nodeSpacing, float nodeSpacing2, float nodeMass, float terminusMass, float constraintStiffness)
    {
        var d = position[i] - position[i - 1];
        var l = math.lengthsq(d);

        if (l > nodeSpacing2)
        {
            var c = constraintStiffness * (1 - nodeSpacing * math.rsqrt(l)) * d;

            if (math.isinf(terminusMass))
            {
                constraintDelta[i - 1] += c;
            }
            else
            {
                var c1 = terminusMass / (nodeMass + terminusMass) * c;
                constraintDelta[i - 1] += c1;
                constraintDelta[i] += c1 - c;
            }
        }
    }
}