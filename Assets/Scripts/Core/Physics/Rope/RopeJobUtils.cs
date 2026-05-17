using Unity.Collections;
using Unity.Mathematics;
using Unity.U2D.Physics;
using Unity.Burst.CompilerServices;

public static class RopeJobUtils
{
    const float OVERLAP_BUFFER = 0.001f;

    //MOVEMENT
    public static void Anchor(int i, NativeArray<float2> position, NativeArray<float2> lastPosition)
    {
        lastPosition[i] = position[i];
    }

    /// <summary> Moves until impact, bounces, then applies the remaining movement without collision detection (good when movement is expected to be small, like constraints). </summary>
    public static (float2 pos, float2 lastPos, float2 velocity) MoveNodeFast(float2 position, float radius, float mass, float2 lastPosition, float2 movement,
        NativeArray<PhysicsCoreHelper.ShapeProxyForJobs> shapeCapture, PhysicsWorld world, PhysicsQuery.QueryFilter collisionFilter, float collisionBounciness, float dynamicCollisionForce)
    {
        var node = new CircleGeometry() { center = position, radius = radius };
        var castResults = world.CastGeometry(node, movement, collisionFilter);
        var (pos, lastPos, velocity, _) = MoveNodeUntilImpact(position, radius, mass, lastPosition, movement, movement, OVERLAP_BUFFER, castResults, 
            shapeCapture, collisionBounciness, dynamicCollisionForce);

        if (castResults.Length == 0)
        {
            return (pos, lastPos, velocity);
        }

        return (pos + (1 - castResults[0].fraction) * velocity, lastPos, velocity);
    }

    /// <summary> Moves until impact, bounces, and does a second cast for the remaining movement (if there's a second impact, it just stops at that point). </summary>
    public static (float2 pos, float2 lastPos, float2 velocity) MoveNodeCareful(float2 position, float radius, float mass, float2 lastPosition, float2 movement,
        NativeArray<PhysicsCoreHelper.ShapeProxyForJobs> shapeCapture, PhysicsWorld world, PhysicsQuery.QueryFilter collisionFilter, float collisionBounciness, float dynamicCollisionForce)
    {
        var node1 = new CircleGeometry() { center = position, radius = radius };
        var castResults1 = world.CastGeometry(node1, movement, collisionFilter);
        var (pos, lastPos, velocity, _) = MoveNodeUntilImpact(position, radius, mass, lastPosition, movement, movement, OVERLAP_BUFFER, castResults1,
            shapeCapture, collisionBounciness, dynamicCollisionForce);

        if (castResults1.Length == 0)
        {
            return (pos, lastPos, velocity);
        }

        var node2 = new CircleGeometry() { center = pos, radius = radius };
        var remainingMovement = (1 - castResults1[0].fraction) * velocity;
        var castResults2 = world.CastGeometry(node2, remainingMovement, collisionFilter);
        (pos, lastPos, velocity, _) = MoveNodeUntilImpact(pos, radius, mass, lastPos, velocity, remainingMovement, OVERLAP_BUFFER, castResults2, 
            shapeCapture, collisionBounciness, dynamicCollisionForce);

        return (pos, lastPos, velocity);
    }

    /// <summary> Casts circle along movement ray, and resolves overlap at the final position of the cast. Goal movement is dt * velocity.</summary>
    public static unsafe (float2 pos, float2 lastPos, float2 velocity, float2 collisionNormal)
        MoveNodeUntilImpact(float2 position, float radius, float mass, float2 lastPosition, float2 velocity, float2 movement, float overlapBuffer, NativeArray<PhysicsQuery.WorldCastResult> castResults,
        NativeArray<PhysicsCoreHelper.ShapeProxyForJobs> shapeCapture, float collisionBounciness, float dynamicCollisionForce)
    {
        if (castResults.Length == 0)
        {
            return (position + movement, lastPosition, velocity, 0);
        }

        var result = castResults[0];
        var shapeId = result.shape.Id();
        if (Hint.Unlikely(!PhysicsCoreHelper.ShapeProxyForJobs.ShapeValid(shapeId, shapeCapture)))
        {
            return (position + movement, lastPosition, velocity, 0);
        }

        float2 pos = position + result.fraction * movement;
        float2 lastPos = lastPosition;
        float2 collisionNormal1 = result.normal;

        if (result.fraction == 0)
        {
            float separation;
            (separation, collisionNormal1) = CollisionUtilities.SeparateCircleFromShape(pos, radius + overlapBuffer, shapeCapture[shapeId], result.shape.transform);
            //^add a little cushion to the radius, so the cast in step 2 doesn't grab on the overlap we just resolved (0.001f seems to be enough of a buffer for this to work most of the time)

            var sepVector = separation * collisionNormal1;

            pos += sepVector;
            lastPos += sepVector;//add the correction to lastPos to keep velocity smooth

            var body = result.shape.body;
            if (body.type == PhysicsBody.BodyType.Dynamic)
            {
                var bodySep = mass / (mass + body.mass) * sepVector;
                var f = -dynamicCollisionForce * bodySep;
                pos -= bodySep;
                lastPos -= bodySep;
                body.ApplyLinearImpulse(f, pos);
            }
        }

        if (math.dot(movement, collisionNormal1) < 0)
        {
            var tangent1 = new float2(-collisionNormal1.y, collisionNormal1.x);
            velocity = -collisionBounciness * (movement - 2 * math.dot(movement, tangent1) * tangent1);
        }

        return (pos, lastPos, velocity, collisionNormal1);
    }

    //public static unsafe void MoveTerminusWithDynamicAnchor(NativeArray<float2> position, float2 movement,
    //NativeArray<PhysicsCoreHelper.ShapeProxyForJobs> shapeCapture,
    //NativeReference<CircleGeometry> anchorGeometry, NativeReference<PhysicsShape> terminusAnchor, NativeReference<float2> terminusAnchorLocalPos,
    //PhysicsWorld world, PhysicsQuery.QueryFilter collisionFilter, float collisionBounciness)
    //{
    //    var anchor = terminusAnchor.Value;
    //    var geom = anchorGeometry.Value;
    //    if (anchor.isValid && geom.isValid)
    //    {
    //        collisionFilter.hitCategories &= ~terminusAnchor.Value.contactFilter.categories;

    //        var transform = anchor.transform;
    //        float2 d = transform.TransformPoint(terminusAnchorLocalPos.Value) - transform.position;
    //        transform.position = position[^1] - d;

    //        //we don't integrate anchor, so just get position
    //        float2 pos = transform.TransformPoint(geom.center);
    //        (pos, _, _) = MoveNodeFast(pos, geom.radius, pos, movement, shapeCapture, world, collisionFilter, collisionBounciness);
    //        position[^1] = pos + d;
    //    }
    //}

    public static void MoveTerminusUnanchored(NativeArray<float2> position, float radius, float mass, NativeArray<float2> lastPosition, float2 movement,
    NativeArray<PhysicsCoreHelper.ShapeProxyForJobs> shapeCapture,
    NativeReference<PhysicsShape> terminusAnchor, NativeReference<float2> terminusAnchorLocalPos, NativeReference<FastRope.TerminusAnchorMode> terminusAnchorMode,
    PhysicsWorld world, PhysicsQuery.QueryFilter collisionFilter, float collisionBounciness, float dynamicCollisionForce, bool stepVelocity)
    {
        var node = new CircleGeometry() { center = position[^1], radius = radius };
        var castResults = world.CastGeometry(node, movement, collisionFilter);
        var (pos, lastPos, velocity, _) = MoveNodeUntilImpact(position[^1], radius, mass, lastPosition[^1], movement, movement, 0, castResults, shapeCapture, collisionBounciness, dynamicCollisionForce);

        if (Hint.Unlikely(castResults.Length > 0 && IsValidAnchor(castResults[0].shape)))
        {
            position[^1] = pos;
            lastPosition[^1] = math.select(lastPos, pos - velocity, stepVelocity);

            var shape = castResults[0].shape;
            terminusAnchor.Value = shape;
            var localPos = shape.body.transform.InverseTransformPoint(pos);
            terminusAnchorLocalPos.Value = localPos;
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

    public static bool IsValidAnchor(PhysicsShape shape)
    {
        return shape.body.type != PhysicsBody.BodyType.Dynamic 
            || shape.shapeType == PhysicsShape.ShapeType.Circle || shape.shapeType == PhysicsShape.ShapeType.Capsule || shape.shapeType == PhysicsShape.ShapeType.Polygon;
    }


    //CONSTRAINTS

    public static float4 CalculateConstraint(float2 p0, float mass0, float2 p1, float mass1, float nodeSpacing, float nodeSpacing2, float constraintStiffness)
    {
        var d = p1 - p0;
        var l = math.lengthsq(d);

        var c = math.select(0, constraintStiffness * (1 - nodeSpacing * math.rsqrt(l)) * d, l > nodeSpacing2);
        var denom = 1 / (mass0 + mass1);//zero if one or both masses are infinite (as long as using FloatMode.Strict (the default))

        var c0 = math.select(mass1 * denom * c, c, math.isinf(mass1));
        return new float4(c0.x, c0.y, c0.x - c.x, c0.y - c.y);//(c0, c0 - c)
    }

    public static void CalculateConstraint(int i, NativeArray<float2> position, NativeArray<float2> constraintDelta,
        float nodeSpacing, float nodeSpacing2, float prevNodeMass, float nodeMass, float constraintStiffness)
    {
        var d = position[i] - position[i - 1];
        var l = math.lengthsq(d);

        var c = math.select(0, constraintStiffness * (1 - nodeSpacing * math.rsqrt(l)) * d, l > nodeSpacing2);
        var denom = 1 / (prevNodeMass + nodeMass);//zero if one or both masses are infinite (as long as using FloatMode.Strict (the default))

        var c1 = math.select(nodeMass * denom * c, c, math.isinf(nodeMass));
        var c2 = c1 - c;
        constraintDelta[i - 1] += c1;
        constraintDelta[i] += c2;
    }
}