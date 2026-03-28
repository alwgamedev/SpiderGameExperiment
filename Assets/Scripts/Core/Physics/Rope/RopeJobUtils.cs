using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Unity.U2D.Physics;

public static class RopeJobUtils
{
    const float CONSTRAINTS_TOLERANCE = MathTools.o41;

    //MOVEMENT

    [BurstCompile]//i don't think we need [BurstCompile] on these if they are called from within a Burst compiled job
    public static void Anchor(int i, NativeArray<float2> position, NativeArray<float2> lastPosition)
    {
        lastPosition[i] = position[i];
    }

    [BurstCompile]
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

    [BurstCompile]
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

    public static void CoverAllSpacingConstraint(int i, NativeArray<float2> position, NativeArray<float2> lastPosition,
        NativeReference<PhysicsShape> terminusAnchor, NativeReference<float2> terminusAnchorLocalPos,
        NativeReference<FastRope.TerminusAnchorMode> terminusAnchorMode,
        PhysicsWorld world, PhysicsQuery.QueryFilter collisionFilter,
        float collisionBounciness, float nodeSpacing, float nodeRadius, float nodeMass, float ownerMass, float terminusMass,
        float dynamicAnchorPullForce, int sourceIndex)
    {
        if (i == sourceIndex + 1)
        {
            FirstConstraint(sourceIndex, position, lastPosition,
                world, collisionFilter, collisionBounciness, nodeSpacing, nodeRadius, nodeMass, ownerMass);
        }
        else if (i == position.Length - 1)
        {
            LastConstraint(i, position, lastPosition,
                terminusAnchor, terminusAnchorLocalPos, terminusAnchorMode,
                world, collisionFilter, collisionBounciness,
                nodeSpacing, nodeRadius, nodeMass, terminusMass, dynamicAnchorPullForce);
        }
        else
        {
            SpacingConstraint(i, position, lastPosition,
                world, collisionFilter, collisionBounciness, nodeSpacing, nodeRadius);
        }
    }

    //accesses 2 nodes: i, i - 1
    public static void SpacingConstraint(int i, NativeArray<float2> position, NativeArray<float2> lastPosition,
        PhysicsWorld world, PhysicsQuery.QueryFilter collisionFilter,
        float collisionBounciness, float nodeSpacing, float nodeRadius)
    {
        var d = position[i] - position[i - 1];
        var l = math.length(d);

        var error = l - nodeSpacing;

        if (error > CONSTRAINTS_TOLERANCE)
        {
            var c = 0.5f * error / l * d;
            MoveNode(i - 1, c, position, lastPosition,
                world, collisionFilter, nodeRadius, collisionBounciness, false, out _);
            MoveNode(i, -c, position, lastPosition,
                world, collisionFilter, nodeRadius, collisionBounciness, false, out _);
        }
    }

    //accesses 2 nodes: startIndex, startIndex + 1
    public static void FirstConstraint(int startIndex, NativeArray<float2> position, NativeArray<float2> lastPosition,
        PhysicsWorld world, PhysicsQuery.QueryFilter collisionFilter,
        float collisionBounciness, float nodeSpacing, float nodeRadius, float nodeMass, float ownerMass)
    {
        var d = position[startIndex + 1] - position[startIndex];
        var l = math.length(d);

        var error = l - nodeSpacing;

        if (error > CONSTRAINTS_TOLERANCE)
        {
            if (math.isinf(ownerMass))
            {
                MoveNode(startIndex + 1, -error / l * d, position, lastPosition,
                    world, collisionFilter, nodeRadius, collisionBounciness, false, out _);
            }
            else
            {
                d *= error / l;
                var c = ownerMass / (nodeMass + ownerMass) * d;
                MoveNode(startIndex + 1, -c, position, lastPosition,
                    world, collisionFilter, nodeRadius, collisionBounciness, false, out _);
                MoveNode(startIndex, d - c, position, lastPosition,
                    world, collisionFilter, nodeRadius, collisionBounciness, false, out _);
            }
        }
    }

    //accesses nodes terminusIndex - 1 and terminusIndex
    public static void LastConstraint(int terminusIndex, NativeArray<float2> position, NativeArray<float2> lastPosition,
        NativeReference<PhysicsShape> terminusAnchor, NativeReference<float2> terminusAnchorLocalPos,
        NativeReference<FastRope.TerminusAnchorMode> terminusAnchorMode,
        PhysicsWorld world, PhysicsQuery.QueryFilter collisionFilter,
        float collisionBounciness, float nodeSpacing, float nodeRadius, float nodeMass, float terminusMass, float dynamicAnchorPullForce)
    {
        var d = position[terminusIndex] - position[terminusIndex - 1];
        var l = math.length(d);

        var error = l - nodeSpacing;

        if (error > CONSTRAINTS_TOLERANCE)
        {
            switch (terminusAnchorMode.Value)
            {
                case FastRope.TerminusAnchorMode.staticAnchor:
                    MoveNode(terminusIndex - 1, error / l * d, position, lastPosition,
                        world, collisionFilter, nodeRadius, collisionBounciness, false, out _);
                    break;
                case FastRope.TerminusAnchorMode.dynamicAnchor:
                    {
                        //idea: run the spacing constraints as normal, updating terminus position to determine the forces that need to be applied to anchor,
                        //then we'll set terminus back to anchored position after finishing constraint iterations
                        //(this works the best of what I tried)
                        var tMass = terminusAnchor.Value.body.mass;//terminus is usually very heavy to get arching motion when shoot, so use anchor mass
                        d *= error / l;
                        var c = tMass / (nodeMass + tMass) * d;
                        var c2 = d - c;
                        MoveNode(terminusIndex - 1, c, position, lastPosition,
                            world, collisionFilter, nodeRadius, collisionBounciness, false, out _);
                        if (terminusAnchor.Value.isValid)
                        {
                            terminusAnchor.Value.body.ApplyForce(-dynamicAnchorPullForce * c2, position[terminusIndex]);
                        }
                        position[terminusIndex] -= c2;//no collision so don't use MoveNode (we're just pulling on the anchor)
                        break;
                    }
                case FastRope.TerminusAnchorMode.notAnchored:
                    {
                        d *= error / l;
                        var c = terminusMass / (nodeMass + terminusMass) * d;
                        MoveNode(terminusIndex - 1, c, position, lastPosition,
                            world, collisionFilter, nodeRadius, collisionBounciness, false, out _);
                        MoveAndAnchorTerminus(c - d, position, lastPosition,
                            terminusAnchor, terminusAnchorLocalPos, terminusAnchorMode,
                            world, collisionFilter, nodeRadius, collisionBounciness, false);
                        break;
                    }
            }
        }
    }
}