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

    public static (float2 pos, float2 lastPos) MoveNode(float2 position, float radius, float2 lastPosition, float2 movement,
        NativeParallelHashMap<uint, PhysicsCoreHelper.ShapeProxy> shapeCapture, PhysicsWorld world, PhysicsQuery.QueryFilter collisionFilter, 
        float collisionBounciness, float nodeMass, bool stepVelocity)
    {
        var (pos, lastPos, move, castResults) = MoveNodeStep1(position, radius, lastPosition, movement, shapeCapture, world, collisionFilter, collisionBounciness, nodeMass, stepVelocity);

        if (Hint.Likely(castResults.IsCreated && castResults.Length > 0))
        {
            return MoveNodeStep2(pos, radius, lastPos, move, castResults[0].fraction, world, collisionFilter, stepVelocity);
        }

        return (pos, lastPos);
    }
    //public static (float2 pos, float2 lastPos) MoveNode(float2 position, float radius, float2 lastPosition, float2 movement,
    //    NativeParallelHashMap<uint, PhysicsCoreHelper.ShapeProxy> shapeCapture, PhysicsWorld world, PhysicsQuery.QueryFilter collisionFilter, float collisionBounciness, bool stepVelocity)
    //{
    //    if (math.lengthsq(movement) < MathTools.o91)
    //    {
    //        return (position, lastPosition);
    //    }

    //    var node1 = new CircleGeometry() { center = position, radius = radius };
    //    var castResults1 = world.CastGeometry(node1, movement, collisionFilter);


    //    if (castResults1.Length == 0)
    //    {
    //        return (position + movement, math.select(lastPosition, position, stepVelocity));
    //    }

    //    var result1 = castResults1[0];
    //    if (!shapeCapture.TryGetValue(result1.shape.Id(), out var proxy1))
    //    {
    //        return (position + movement, math.select(lastPosition, position, stepVelocity));
    //    }

    //    float2 pos = position + result1.fraction * movement;
    //    float2 lastPos = lastPosition;
    //    float2 collisionNormal1 = result1.normal;

    //    if (result1.fraction == 0)
    //    {
    //        float separation;
    //        (separation, collisionNormal1) = CollisionUtilities.SeparateCircleFromShape(pos, radius + 0.001f, proxy1, result1.shape.transform);
    //        float2 sepVector = separation * collisionNormal1;
    //        pos += sepVector;
    //        lastPos += sepVector;
    //    }

    //    if (math.dot(movement, collisionNormal1) < 0)
    //    {
    //        var tangent1 = new float2(-collisionNormal1.y, collisionNormal1.x);
    //        movement = -collisionBounciness * (movement - 2 * math.dot(movement, tangent1) * tangent1);
    //    }

    //    var node2 = new CircleGeometry() { center = pos, radius = radius };
    //    var remainingMovement = (1 - result1.fraction) * movement;
    //    var castResults2 = world.CastGeometry(node2, remainingMovement, collisionFilter);

    //    if (castResults2.Length == 0)
    //    {
    //        pos += remainingMovement;
    //        return (pos, math.select(lastPos, pos - movement, stepVelocity));
    //    }

    //    pos += castResults2[0].fraction * remainingMovement;
    //    return (pos, math.select(lastPos, pos - movement, stepVelocity));
    //}

    public static unsafe (float2 pos, float2 lastPos, float2 movement, NativeArray<PhysicsQuery.WorldCastResult> castResults) MoveNodeStep1(float2 position, float radius, float2 lastPosition, float2 movement,
        NativeParallelHashMap<uint, PhysicsCoreHelper.ShapeProxy> shapeCapture, PhysicsWorld world, PhysicsQuery.QueryFilter collisionFilter, float collisionBounciness, float nodeMass, 
        bool stepVelocity)
    {
        if (math.lengthsq(movement) < MathTools.o91)
        {
            return (position, lastPosition, 0, default);
        }

        var node = new CircleGeometry() { center = position, radius = radius };
        var castResults = world.CastGeometry(node, movement, collisionFilter);


        if (castResults.Length == 0)
        {
            return (position + movement, math.select(lastPosition, position, stepVelocity), 0, default);
        }

        var result = castResults[0];
        if (!shapeCapture.TryGetValue(result.shape.Id(), out var proxy1))
        {
            return (position + movement, math.select(lastPosition, position, stepVelocity), 0, default);
        }

        float2 pos = position + result.fraction * movement;
        float2 lastPos = lastPosition;
        float2 collisionNormal1 = result.normal;

        if (result.fraction == 0)
        {
            float separation;
            (separation, collisionNormal1) = CollisionUtilities.SeparateCircleFromShape(pos, radius + 0.001f, proxy1, result.shape.transform);
            //^add a little cushion to the radius, so the cast in step 2 doesn't grab on the overlap we just resolved (0.001f seems to be enough of a buffer for this to work most of the time)

            var sepVector = separation * collisionNormal1;

            var body = result.shape.body;
            if (body.type == PhysicsBody.BodyType.Dynamic)
            {
                var impulse = nodeMass / (body.mass + nodeMass) * collisionBounciness * sepVector;
                result.shape.body.ApplyForce(impulse, pos);
            }

            pos += sepVector;
            lastPos += sepVector;//add the correction to lastPos to keep velocity smooth
        }

        if (math.dot(movement, collisionNormal1) < 0)
        {
            var tangent1 = new float2(-collisionNormal1.y, collisionNormal1.x);
            movement = -collisionBounciness * (movement - 2 * math.dot(movement, tangent1) * tangent1);
        }

        return (pos, lastPos, movement, castResults);
    }

    public static (float2 pos, float2 lastPos) MoveNodeStep2(float2 position, float radius, float2 lastPos, float2 movement, float step1HitFraction,
        PhysicsWorld world, PhysicsQuery.QueryFilter collisionFilter, bool stepVelocity)
    {
        var node = new CircleGeometry() { center = position, radius = radius };
        var remainingMovement = (1 - step1HitFraction) * movement;
        var castResults = world.CastGeometry(node, remainingMovement, collisionFilter);

        if (castResults.Length == 0)
        {
            position += remainingMovement;
            return (position, math.select(lastPos, position - movement, stepVelocity));
        }

        //if there's a second hit along our trajectory, we just stop there
        position += castResults[0].fraction * remainingMovement;
        return (position, math.select(lastPos, position - movement, stepVelocity));
    }

    public static void MoveTerminusWithDynamicAnchor(NativeArray<float2> position, float2 movement,
    NativeParallelHashMap<uint, PhysicsCoreHelper.ShapeProxy> shapeCapture, 
    PhysicsCoreHelper.ShapeProxy anchorGeometry, NativeReference<PhysicsShape> terminusAnchor, NativeReference<float2> terminusAnchorLocalPos,
    PhysicsWorld world, PhysicsQuery.QueryFilter collisionFilter, float collisionBounciness, float anchorMass, bool stepVelocity)
    {
        var anchor = terminusAnchor.Value;
        if (anchor.isValid)
        {
            collisionFilter.hitCategories &= ~terminusAnchor.Value.contactFilter.categories;

            //var nodePos = position[^1];
            var transform = anchor.transform;
            float2 d = transform.TransformPoint(terminusAnchorLocalPos.Value) - transform.position;
            transform.position = position[^1] - d;

            float2 pos = transform.TransformPoint(anchorGeometry.Center1);
            //var d = nodePos - pos;
            (pos, _) = MoveNode(pos, anchorGeometry.Radius, pos, movement, shapeCapture, world, collisionFilter, collisionBounciness, anchorMass, stepVelocity);
            position[^1] = pos + d;
            //lastPosition[^1] = lastPos + d;
        }
    }

    public static void MoveTerminusUnanchored(NativeArray<float2> position, float radius, NativeArray<float2> lastPosition, float2 movement,
    NativeParallelHashMap<uint, PhysicsCoreHelper.ShapeProxy> shapeCapture,
    NativeReference<PhysicsShape> terminusAnchor, NativeReference<float2> terminusAnchorLocalPos, NativeReference<FastRope.TerminusAnchorMode> terminusAnchorMode,
    PhysicsWorld world, PhysicsQuery.QueryFilter collisionFilter, float collisionBounciness, float nodeMass, bool stepVelocity)
    {
        var (pos, lastPos, _, castResults) = MoveNodeStep1(position[^1], radius, lastPosition[^1], movement, shapeCapture, world, collisionFilter, collisionBounciness, nodeMass, stepVelocity);

        if (Hint.Unlikely(castResults.IsCreated && castResults.Length > 0))
        {
            position[^1] = pos;
            lastPosition[^1] = pos;

            var shape = castResults[0].shape;
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
        else
        {
            position[^1] = pos;
            lastPosition[^1] = lastPos;
        }
    }

    public static void MoveAndAnchorTerminus(float2 deltaPosition, NativeArray<float2> position, NativeArray<float2> lastPosition,
    NativeArray<RopeCollisionDebugData> collisionData, NativeParallelHashMap<uint, PhysicsCoreHelper.ShapeProxy> shapeCapture,
    NativeReference<PhysicsShape> terminusAnchor, NativeReference<float2> terminusAnchorLocalPos, NativeReference<FastRope.TerminusAnchorMode> terminusAnchorMode,
    PhysicsWorld world, PhysicsQuery.QueryFilter collisionFilter, float nodeRadius, float collisionBounciness, bool stepVelocity)
    {
        MoveNode(position.Length - 1, deltaPosition, position, lastPosition, collisionData, shapeCapture,
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
        NativeArray<RopeCollisionDebugData> collisionData, NativeParallelHashMap<uint, PhysicsCoreHelper.ShapeProxy> shapeCapture,
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

        HandleCastResults(i, deltaPosition, position, lastPosition, collisionData, shapeCapture, collisionBounciness, stepVelocity, position[i], nodeRadius,
            castResults);
    }

    public static unsafe void MoveDynamicAnchor(in PhysicsCoreHelper.ShapeProxy anchorGeometry, PhysicsShape terminusAnchor, float2 terminusAnchorLocalPos,
    float2 deltaPosition, NativeArray<float2> position, NativeArray<float2> lastPosition,
    NativeArray<RopeCollisionDebugData> collisionData, NativeParallelHashMap<uint, PhysicsCoreHelper.ShapeProxy> shapeCapture,
    PhysicsWorld world, PhysicsQuery.QueryFilter collisionFilter, float collisionBounciness, bool stepVelocity)
    {
        if (!terminusAnchor.isValid || math.lengthsq(deltaPosition) < MathTools.o91)
        {
            return;
        }

        NativeArray<PhysicsQuery.WorldCastResult> castResults = default;

        var transform = terminusAnchor.transform;
        transform.position = (float2)(transform.position - transform.TransformPoint(terminusAnchorLocalPos)) + position[^1];
        collisionFilter.hitCategories &= ~terminusAnchor.contactFilter.categories;

        switch (anchorGeometry.ShapeType)
        {
            case PhysicsShape.ShapeType.Circle:
                {
                    var geom = anchorGeometry.CircleGeometry().Transform(transform);
                    castResults = world.CastGeometry(geom, deltaPosition, collisionFilter);
                    break;
                }
            case PhysicsShape.ShapeType.Polygon:
                {
                    var geom = anchorGeometry.PolygonGeometry().Transform(transform);
                    castResults = world.CastGeometry(geom, deltaPosition, collisionFilter);
                    break;
                }
            case PhysicsShape.ShapeType.Capsule:
                {
                    var geom = anchorGeometry.CapsuleGeometry().Transform(transform);
                    castResults = world.CastGeometry(geom, deltaPosition, collisionFilter);
                    break;
                }
            default:
                return;
        }

        //2do: anchorGeometry->Radius is just temporary (since testing circle anchor) until we add more shapes (or give every shape an encapsulating radius and treat as circle)
        HandleCastResults(position.Length - 1, deltaPosition, position, lastPosition, collisionData, shapeCapture, collisionBounciness, stepVelocity,
            transform.TransformPoint(anchorGeometry.Center1), anchorGeometry.Radius, castResults);
    }

    public static void HandleCastResults(int i, float2 deltaPosition, NativeArray<float2> position, NativeArray<float2> lastPosition,
        NativeArray<RopeCollisionDebugData> collisionData, NativeParallelHashMap<uint, PhysicsCoreHelper.ShapeProxy> shapeCapture,
        float collisionBounciness, bool stepVelocity, float2 center, float radius,
        NativeArray<PhysicsQuery.WorldCastResult> castResults)
    {
        if (castResults.Length > 0)
        {
            var result = castResults[0];
            float2 normal = result.normal;
            var positionAtTimeOfImpact = position[i] + result.fraction * deltaPosition;
            RopeCollisionDebugData colData = new();

            if (result.fraction == 0)//there was initial overlap
            {
                var shape = result.shape;
                if (shapeCapture.TryGetValue(shape.Id(), out var proxy) /*&& proxy.OverlapPoint(shape.transform, result.point, out var escapeNormal, out var escapeDistance)*/)
                {
                    //float escapeDistance;
                    //(normal, escapeDistance) = proxy.OverlapPoint(shape.transform, result.point);
                    float separation;
                    (separation, normal) = CollisionUtilities.SeparateCircleFromShape(positionAtTimeOfImpact + center - position[i], radius + 0.001f, proxy, shape.transform);
                    positionAtTimeOfImpact += separation * normal;
                    lastPosition[i] += separation * normal;
                }
            }

            colData.normal = normal;
            collisionData[i] = colData;

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