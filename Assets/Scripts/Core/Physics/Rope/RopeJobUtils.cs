using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Unity.U2D.Physics;

public static class RopeJobUtils
{
    const float CONSTRAINTS_TOLERANCE = MathTools.o41;

    //MISC

    public static void Anchor(int i, NativeArray<float2> position, NativeArray<float2> lastPosition,
        NativeArray<bool> nearCollision, NativeArray<bool> hadCollision, NativeArray<float2> lastCollisionNormal)
    {
        lastPosition[i] = position[i];
        nearCollision[i] = false;
        hadCollision[i] = false;
        lastCollisionNormal[i] = 0;
    }

    [BurstCompile]
    public static void MoveNodeWithCollisionResolution(int i, NativeArray<float2> position, NativeArray<float2> lastPosition,
        NativeArray<float2> lastCollisionNormal, NativeArray<bool> collisionStatus,
        PhysicsQuery.QueryFilter filter, float2 targetPosition, float nodeRadius, float collisionBounciness, bool stepVelocity)
    {
        var dp = targetPosition - position[i];
        var circle = new CircleGeometry()
        {
            center = position[i],
            radius = nodeRadius
        };

        var castResults = PhysicsWorld.defaultWorld.CastGeometry(circle, dp, filter);

        if (castResults.Length > 0)
        {
            collisionStatus[i] = true;
            var result = castResults[0];
            var positionAtTimeOfImpact = position[i] + result.fraction * dp;

            float2 normal;
            if (result.normal.x == 0 && result.normal.y == 0)
            {
                normal = lastCollisionNormal[i];
            }
            else
            {
                lastCollisionNormal[i] = result.normal;
                normal = result.normal;
            }

            if (math.dot(dp, normal) < 0)
            {
                dp = -collisionBounciness * (dp - 2 * (-dp.x * normal.y + dp.y * normal.x) * new float2(-normal.y, normal.x));
            }

            position[i] = positionAtTimeOfImpact + (1 - result.fraction) * dp;
            lastPosition[i] = position[i] - dp;
        }
        else
        {
            collisionStatus[i] = false;
            if (stepVelocity)
            {
                lastPosition[i] = position[i];
            }
            position[i] = targetPosition;
        }
    }


    //COLLISION -- can get rid of this now, we'll just use "MoveWithCollisionResolution" everywhere

    public static void ResolveTerminusCollision(int terminusIndex, NativeArray<float2> position, NativeArray<float2> lastPosition, NativeArray<float2> lastCollisionNormal,
        NativeArray<bool> nearCollision, NativeArray<bool> hadCollision, NativeArray<float2> raycastDirections,
        NativeReference<PhysicsShape> terminusAnchor, NativeReference<float2> terminusAnchorLocalPos, NativeReference<FastRope.TerminusAnchorMode> terminusAnchorMode,
        PhysicsWorld world, PhysicsQuery.QueryFilter filter, float collisionSearchRadius, float collisionThreshold, float tunnelEscapeRadius,
        float collisionBounciness)
    {
        var p = position[terminusIndex];
        ResolveCollision(terminusIndex, position, lastPosition, lastCollisionNormal, nearCollision, hadCollision, raycastDirections,
            world, filter, collisionSearchRadius, collisionThreshold, tunnelEscapeRadius, collisionBounciness);

        if (nearCollision[terminusIndex])
        {
            var v = p - position[terminusIndex];
            var circleGeom = new CircleGeometry()
            {
                center = position[terminusIndex],
                radius = collisionThreshold
            };
            var castOutput = world.CastGeometry(circleGeom, v, filter);
            if (castOutput.Length > 0)
            {
                //anchor just outside collider, so that nodes near lastIndex don't get caught in perpetual collision
                var result = castOutput[0];
                p = result.point + collisionThreshold * result.normal;
                position[terminusIndex] = p;

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
                Anchor(terminusIndex, position, lastPosition, nearCollision, hadCollision, lastCollisionNormal);//kills terminus velocity

                //TerminusBecameAnchored.Invoke();
            }
        }
    }

    public static void SolveNodeMovement(int i, NativeArray<float2> position, NativeArray<float2> lastPosition,
        PhysicsWorld world, PhysicsQuery.QueryFilter filter, float collisionThreshold, float dtInverse, float dt, int maxIterations, float moveTolerance, float2 targetPosition)
    {
        var mover = new PhysicsQuery.WorldMoverInput()
        {
            geometry = new CapsuleGeometry() { center1 = position[i], center2 = position[i], radius = collisionThreshold },
            transform = PhysicsTransform.identity,
            targetPosition = targetPosition,
            velocity = dtInverse * (position[i] - lastPosition[i]),
            maxIterations = maxIterations,
            moveTolerance = moveTolerance,
            overlapFilter = filter,
            castFilter = filter
            //there is also a "collisionResults" bool if you want to get back an array of all the collision results collected while solving
        };

        var moverResult = world.CastMover(mover);
        position[i] = moverResult.transform.position;
        lastPosition[i] = position[i] - dt * (float2)moverResult.velocity;
    }

    public static void ResolveCollision(int i, NativeArray<float2> position, NativeArray<float2> lastPosition, NativeArray<float2> lastCollisionNormal,
        NativeArray<bool> nearCollision, NativeArray<bool> hadCollision, NativeArray<float2> raycastDirections,
        PhysicsWorld world, PhysicsQuery.QueryFilter filter, float collisionSearchRadius, float collisionThreshold, float tunnelEscapeRadius,
        float collisionBounciness)
    {
        if (!nearCollision[i])
        {
            var circleGeom = new CircleGeometry()
            {
                center = position[i],
                radius = collisionSearchRadius
            };

            if (world.TestOverlapGeometry(circleGeom, filter))
            {
                nearCollision[i] = true;
            }
            else
            {
                hadCollision[i] = false;
                return;
            }
        }

        bool tunneling = false;
        var castOutput = world.CastRay(position[i], collisionSearchRadius * raycastDirections[0], filter);
        if (castOutput.Length == 0)
        {
            for (int j = 1; j < raycastDirections.Length; j++)
            {
                castOutput = world.CastRay(position[i], collisionSearchRadius * raycastDirections[j], filter);
                if (castOutput.Length > 0)
                {
                    break;
                }
            }
        }

        if (castOutput.Length > 0 && castOutput[0].fraction == 0)
        {
            tunneling = true;
            for (int j = 0; j < raycastDirections.Length; j++)
            {
                var d = tunnelEscapeRadius * raycastDirections[j];
                castOutput = world.CastRay(PhysicsQuery.CastRayInput.FromTo(position[i] + d, position[i]), filter);
                if (castOutput.Length > 0 && castOutput[0].fraction > 0)
                {
                    break;
                }
            }
        }

        if (castOutput.Length > 0)
        {
            var result = castOutput[0];
            var distance = tunneling ? ((1 - result.fraction) * tunnelEscapeRadius) : (result.fraction * collisionSearchRadius);
            var effectiveCollisionThrehsold = tunneling ? tunnelEscapeRadius : collisionThreshold;
            HandlePotentialCollision(i, position, lastPosition, lastCollisionNormal, hadCollision,
                distance, result.normal, collisionThreshold, effectiveCollisionThrehsold, collisionBounciness);
        }
        else
        {
            nearCollision[i] = false;
            hadCollision[i] = false;
        }
    }

    private static void HandlePotentialCollision(int i, NativeArray<float2> position, NativeArray<float2> lastPosition,
        NativeArray<float2> lastCollisionNormal, NativeArray<bool> hadCollision,
        float distance, float2 normal, float collisionThreshold, float effectiveCollisionThreshold, float collisionBounciness)
    {
        if (distance != 0)
        {
            lastCollisionNormal[i] = normal;
        }
        else
        {
            normal = lastCollisionNormal[i];
        }

        if (distance < effectiveCollisionThreshold)
        {
            var diff = math.min(effectiveCollisionThreshold - distance, collisionThreshold);
            var velocity = position[i] - lastPosition[i];
            position[i] += diff * normal;
            var b = math.dot(velocity, normal);
            if (b < 0)
            {
                var tang = normal.CWPerp();
                var a = math.dot(velocity, tang);
                var newVelocity = -collisionBounciness * (velocity - 2 * a * tang);
                lastPosition[i] = position[i] - newVelocity;
            }
        }
        else
        {
            hadCollision[i] = false;
        }
    }


    //CONSTRAINTS

    public static void CoverAllSpacingConstraint(int i, NativeArray<float2> position, NativeArray<float2> lastPosition, NativeArray<float2> lastCollisionNormal,
        NativeArray<bool> nearCollision, NativeArray<bool> hadCollision, NativeArray<float2> raycastDirections,
        NativeReference<PhysicsShape> terminusAnchor, NativeReference<float2> terminusAnchorLocalPos, /*NativeReference<float2> forceToApplyToDynamicAnchor,*/
        NativeReference<FastRope.TerminusAnchorMode> terminusAnchorMode,
        PhysicsWorld world, PhysicsQuery.QueryFilter filter, float collisionSearchRadius, float collisionThreshold, float tunnelEscapeRadius,
        float collisionBounciness, float nodeSpacing, float nodeMass, float ownerMass, float terminusMass, 
        float dynamicAnchorPullForce,
        int startIndex)
    {
        if (i == startIndex + 1)
        {
            FirstConstraint(startIndex, position, lastPosition, lastCollisionNormal, nearCollision, hadCollision,
                raycastDirections, world, filter, collisionSearchRadius, collisionThreshold, tunnelEscapeRadius, collisionBounciness, nodeSpacing,
                nodeMass, ownerMass);
        }
        else if (i == position.Length - 1)
        {
            LastConstraint(i, position, lastPosition, lastCollisionNormal, nearCollision, hadCollision, raycastDirections,
                terminusAnchor, terminusAnchorLocalPos, /*forceToApplyToDynamicAnchor,*/ terminusAnchorMode,
                world, filter, collisionSearchRadius, collisionThreshold, tunnelEscapeRadius, collisionBounciness,
                nodeSpacing, nodeMass, terminusMass, dynamicAnchorPullForce);
        }
        else
        {
            SpacingConstraint(i, position, lastPosition, lastCollisionNormal, nearCollision, hadCollision,
                raycastDirections, world, filter, collisionSearchRadius, collisionThreshold, tunnelEscapeRadius, collisionBounciness, nodeSpacing);
        }
    }

    //accesses 2 nodes: i, i - 1
    public static void SpacingConstraint(int i, NativeArray<float2> position, NativeArray<float2> lastPosition, NativeArray<float2> lastCollisionNormal,
        NativeArray<bool> nearCollision, NativeArray<bool> hadCollision, NativeArray<float2> raycastDirections,
        PhysicsWorld world, PhysicsQuery.QueryFilter filter, float collisionSearchRadius, float collisionThreshold, float tunnelEscapeRadius,
        float collisionBounciness, float nodeSpacing)
    {
        var d = position[i] - position[i - 1];
        var l = math.length(d);

        var error = l - nodeSpacing;

        if (error > CONSTRAINTS_TOLERANCE)
        {
            if (hadCollision[i - 1] && hadCollision[i])
            {
                var v0 = lastCollisionNormal[i - 1].CCWPerp();
                var v1 = lastCollisionNormal[i].CCWPerp();
                if (math.dot(v0, d) < 0)
                {
                    v0 = -v0;
                }
                if (math.dot(v1, d) < 0)
                {
                    v1 = -v1;
                }

                var t = 0.5f * error / l;
                var tangentScale = 0.25f * l;//this seems to work well
                v0 *= tangentScale;
                v1 *= tangentScale;
                position[i - 1] = MathTools.CubicInterpolation(position[i - 1], v0, position[i], v1, t);
                position[i] = MathTools.CubicInterpolation(position[i - 1], v0, position[i], v1, 1 - t);
            }
            else
            {
                var c = 0.5f * error / l * d;
                position[i - 1] += c;
                position[i] -= c;
            }

            //RELIABLE COLLISION IS THE #1 PRIORITY!
            //constraints pulling nodes through obstacles was the biggest problem with collision,
            //and it's much, MUCH better when you resolve collisions immediately after each constraint
            ResolveCollision(i - 1, position, lastPosition, lastCollisionNormal, nearCollision, hadCollision, raycastDirections,
                    world, filter, collisionSearchRadius, collisionThreshold, tunnelEscapeRadius, collisionBounciness);
            ResolveCollision(i, position, lastPosition, lastCollisionNormal, nearCollision, hadCollision, raycastDirections,
                    world, filter, collisionSearchRadius, collisionThreshold, tunnelEscapeRadius, collisionBounciness);
        }
    }

    //accesses 2 nodes: startIndex, startIndex + 1
    public static void FirstConstraint(int startIndex, NativeArray<float2> position, NativeArray<float2> lastPosition, NativeArray<float2> lastCollisionNormal,
        NativeArray<bool> nearCollision, NativeArray<bool> hadCollision, NativeArray<float2> raycastDirections,
        PhysicsWorld world, PhysicsQuery.QueryFilter filter,
        float collisionSearchRadius, float collisionThreshold, float tunnelEscapeRadius, float collisionBounciness, float nodeSpacing,
        float nodeMass, float ownerMass)
    {
        var d = position[startIndex + 1] - position[startIndex];
        var l = math.length(d);

        var error = l - nodeSpacing;

        if (error > CONSTRAINTS_TOLERANCE)
        {
            if (math.isinf(ownerMass))
            {
                position[startIndex + 1] -= error / l * d;
            }
            else
            {
                d *= error / l;
                var c = ownerMass / (nodeMass + ownerMass) * d;
                position[startIndex + 1] -= c;
                position[startIndex] += d - c;
            }

            ResolveCollision(startIndex + 1, position, lastPosition, lastCollisionNormal, nearCollision, hadCollision, raycastDirections,
                    world, filter, collisionSearchRadius, collisionThreshold, tunnelEscapeRadius, collisionBounciness);
        }
    }

    //accesses nodes terminusIndex - 1 and terminusIndex
    public static void LastConstraint(int terminusIndex, NativeArray<float2> position, NativeArray<float2> lastPosition, NativeArray<float2> lastCollisionNormal,
        NativeArray<bool> nearCollision, NativeArray<bool> hadCollision, NativeArray<float2> raycastDirections,
        NativeReference<PhysicsShape> terminusAnchor, NativeReference<float2> terminusAnchorLocalPos, /*NativeReference<float2> forceToApplyToDynamicAnchor,*/
        NativeReference<FastRope.TerminusAnchorMode> terminusAnchorMode,
        PhysicsWorld world, PhysicsQuery.QueryFilter filter, float collisionSearchRadius, float collisionThreshold, float tunnelEscapeRadius,
        float collisionBounciness, float nodeSpacing, float nodeMass, float terminusMass, float dynamicAnchorPullForce)
    {
        var d = position[terminusIndex] - position[terminusIndex - 1];
        var l = math.length(d);

        var error = l - nodeSpacing;

        if (error > CONSTRAINTS_TOLERANCE)
        {
            switch (terminusAnchorMode.Value)
            {
                case FastRope.TerminusAnchorMode.staticAnchor:
                    position[terminusIndex - 1] += error / l * d;
                    ResolveCollision(terminusIndex - 1, position, lastPosition, lastCollisionNormal, nearCollision, hadCollision, raycastDirections,
                    world, filter, collisionSearchRadius, collisionThreshold, tunnelEscapeRadius, collisionBounciness);
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
                        position[terminusIndex - 1] += c;
                        if (terminusAnchor.Value.isValid)
                        {
                            terminusAnchor.Value.body.ApplyForce(-dynamicAnchorPullForce * c2, position[terminusIndex]);
                        }
                        position[terminusIndex] -= c2;
                        ResolveCollision(terminusIndex - 1, position, lastPosition, lastCollisionNormal, nearCollision, hadCollision, raycastDirections,
                            world, filter, collisionSearchRadius, collisionThreshold, tunnelEscapeRadius, collisionBounciness);
                        break;
                    }
                case FastRope.TerminusAnchorMode.notAnchored:
                    {
                        d *= error / l;
                        var c = terminusMass / (nodeMass + terminusMass) * d;
                        position[terminusIndex - 1] += c;
                        position[terminusIndex] -= d - c;
                        ResolveCollision(terminusIndex - 1, position, lastPosition, lastCollisionNormal, nearCollision, hadCollision, raycastDirections,
                            world, filter, collisionSearchRadius, collisionThreshold, tunnelEscapeRadius, collisionBounciness);
                        ResolveTerminusCollision(terminusIndex, position, lastPosition, lastCollisionNormal, nearCollision, hadCollision, raycastDirections,
                            terminusAnchor, terminusAnchorLocalPos, terminusAnchorMode,
                            world, filter, collisionSearchRadius, collisionThreshold, tunnelEscapeRadius, collisionBounciness);
                        break;
                    }
            }
        }
    }
}