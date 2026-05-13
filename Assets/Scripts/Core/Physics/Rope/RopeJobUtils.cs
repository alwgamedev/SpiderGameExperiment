using Unity.Collections;
using Unity.Mathematics;
using Unity.U2D.Physics;
using Unity.Burst.CompilerServices;

public static class RopeJobUtils
{
    //MOVEMENT
    public static void Anchor(int i, NativeArray<float2> position, NativeArray<float2> lastPosition)
    {
        lastPosition[i] = position[i];
    }

    /// <summary> Moves until impact, bounces, then applies the remaining movement without collision detection (good when movement is expected to be small, like constraints). </summary>
    public static (float2 pos, float2 lastPos, float2 velocity) MoveNodeFast(float2 position, float radius, float2 lastPosition, float2 movement,
        NativeParallelHashMap<uint, PhysicsCoreHelper.ShapeProxyForJobs> shapeCapture, PhysicsWorld world, PhysicsQuery.QueryFilter collisionFilter, float collisionBounciness)
    {
        var (pos, lastPos, velocity, castResult) = MoveNodeUntilImpact(position, radius, lastPosition, movement, 1, 0, shapeCapture, world, collisionFilter, collisionBounciness);

        if (!castResult.isValid)
        {
            return (pos, lastPos, velocity);
        }

        pos += (1 - castResult.fraction) * velocity;
        return (pos, lastPos, velocity);
    }

    /// <summary> Moves until impact, bounces, and does a second cast for the remaining movement (if there's a second impact, it just stops at that point). </summary>
    public static (float2 pos, float2 lastPos, float2 velocity) MoveNodeCareful(float2 position, float radius, float2 lastPosition, float2 movement,
        NativeParallelHashMap<uint, PhysicsCoreHelper.ShapeProxyForJobs> shapeCapture, PhysicsWorld world, PhysicsQuery.QueryFilter collisionFilter, float collisionBounciness)
    {
        var (pos, lastPos, velocity, castResult) = MoveNodeUntilImpact(position, radius, lastPosition, movement, 1, 0.0001f, shapeCapture, world, collisionFilter, collisionBounciness);

        if (!castResult.isValid)
        {
            return (pos, lastPos, velocity);
        }

        (pos, lastPos, velocity, _) = MoveNodeUntilImpact(pos, radius, lastPos, velocity, 1 - castResult.fraction, 0, shapeCapture, world, collisionFilter, collisionBounciness);
        return (pos, lastPos, velocity);
    }

    /// <summary> Casts circle along movement ray, and resolves overlap at the final position of the cast. Goal movement is dt * velocity.</summary>
    public static unsafe (float2 pos, float2 lastPos, float2 velocity, PhysicsQuery.WorldCastResult castResult) 
        MoveNodeUntilImpact(float2 position, float radius, float2 lastPosition, float2 velocity, float dt, float overlapBuffer,
        NativeParallelHashMap<uint, PhysicsCoreHelper.ShapeProxyForJobs> shapeCapture, PhysicsWorld world, PhysicsQuery.QueryFilter collisionFilter, float collisionBounciness)
    {
        var movement = dt * velocity;
        //if (math.lengthsq(movement) < MathTools.o91)//still resolve overlaps
        //{
        //    return (position, lastPosition, velocity, default);
        //}

        var node = new CircleGeometry() { center = position, radius = radius };
        var castResults = world.CastGeometry(node, movement, collisionFilter);

        if (castResults.Length == 0)
        {
            return (position + movement, lastPosition, velocity, default);
        }

        var result = castResults[0];
        if (Hint.Unlikely(!shapeCapture.TryGetValue(result.shape.Id(), out var proxy1)))
        {
            return (position + movement, lastPosition, velocity, default);
        }

        float2 pos = position + result.fraction * movement;
        float2 lastPos = lastPosition;
        float2 collisionNormal1 = result.normal;

        if (result.fraction == 0)
        {
            float separation;
            (separation, collisionNormal1) = CollisionUtilities.SeparateCircleFromShape(pos, radius + overlapBuffer, proxy1, result.shape.transform);
            //^add a little cushion to the radius, so the cast in step 2 doesn't grab on the overlap we just resolved (0.001f seems to be enough of a buffer for this to work most of the time)

            var sepVector = separation * collisionNormal1;
            pos += sepVector;
            lastPos += sepVector;//add the correction to lastPos to keep velocity smooth
        }

        if (math.dot(movement, collisionNormal1) < 0)
        {
            var tangent1 = new float2(-collisionNormal1.y, collisionNormal1.x);
            velocity = -collisionBounciness * (movement - 2 * math.dot(movement, tangent1) * tangent1);
        }

        return (pos, lastPos, velocity, result);
    }

    public static void MoveTerminusWithDynamicAnchor(NativeArray<float2> position, float2 movement,
    NativeParallelHashMap<uint, PhysicsCoreHelper.ShapeProxyForJobs> shapeCapture, 
    PhysicsCoreHelper.ShapeProxyForJobs anchorGeometry, NativeReference<PhysicsShape> terminusAnchor, NativeReference<float2> terminusAnchorLocalPos,
    PhysicsWorld world, PhysicsQuery.QueryFilter collisionFilter, float collisionBounciness)
    {
        var anchor = terminusAnchor.Value;
        if (anchor.isValid)
        {
            collisionFilter.hitCategories &= ~terminusAnchor.Value.contactFilter.categories;

            var transform = anchor.transform;
            float2 d = transform.TransformPoint(terminusAnchorLocalPos.Value) - transform.position;
            transform.position = position[^1] - d;

            //we don't integrate anchor, so just get position
            float2 pos = transform.TransformPoint(anchorGeometry.Center1);
            (pos, _, _) = MoveNodeFast(pos, anchorGeometry.Radius, pos, movement, shapeCapture, world, collisionFilter, collisionBounciness);
            position[^1] = pos + d;
        }
    }

    public static void MoveTerminusUnanchored(NativeArray<float2> position, float radius, NativeArray<float2> lastPosition, float2 movement,
    NativeParallelHashMap<uint, PhysicsCoreHelper.ShapeProxyForJobs> shapeCapture,
    NativeReference<PhysicsShape> terminusAnchor, NativeReference<float2> terminusAnchorLocalPos, NativeReference<FastRope.TerminusAnchorMode> terminusAnchorMode,
    PhysicsWorld world, PhysicsQuery.QueryFilter collisionFilter, float collisionBounciness, bool stepVelocity)
    {
        var (pos, lastPos, velocity, castResult) = MoveNodeUntilImpact(position[^1], radius, lastPosition[^1], movement, 1, 0, shapeCapture, world, collisionFilter, collisionBounciness);

        if (Hint.Unlikely(castResult.isValid))
        {
            position[^1] = pos;
            lastPosition[^1] = math.select(lastPos, pos - velocity, stepVelocity);

            var shape = castResult.shape;
            terminusAnchor.Value = shape;
            terminusAnchorLocalPos.Value = shape.body.transform.InverseTransformPoint(pos);
            if (shape.body.type == PhysicsBody.BodyType.Dynamic)
            {
                terminusAnchorMode.Value = FastRope.TerminusAnchorMode.dynamicAnchor;
            }
            else
            {
                terminusAnchorMode.Value = FastRope.TerminusAnchorMode.staticAnchor;
            }
        }
        else//there was no impact, so we already completed the movement
        {
            position[^1] = pos;
            lastPosition[^1] = math.select(lastPos, pos - velocity, stepVelocity);
        }
    }


    //CONSTRAINTS

    /// <summary> (where i = sourceIndex + 1) </summary>
    public static void CalculateFirstConstraint(int i, NativeArray<float2> position, NativeArray<float2> constraintDelta,
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

    public static void CalculateConstraint(int i, NativeArray<float2> position, NativeArray<float2> constraintDelta,
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
    public static void CalculateLastConstraint(int i, NativeArray<float2> position, NativeArray<float2> constraintDelta,
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