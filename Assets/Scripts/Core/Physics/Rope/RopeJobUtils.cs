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
            castResults, 0);
    }

    public static unsafe void MoveDynamicAnchor(in PhysicsCoreHelper.ShapeProxy* anchorGeometry, PhysicsShape terminusAnchor, float2 terminusAnchorLocalPos,
    float2 deltaPosition, NativeArray<float2> position, NativeArray<float2> lastPosition,
    NativeArray<RopeCollisionDebugData> collisionData, NativeParallelHashMap<uint, PhysicsCoreHelper.ShapeProxy> shapeCapture,
    PhysicsWorld world, PhysicsQuery.QueryFilter collisionFilter, float collisionBounciness, bool stepVelocity)
    {
        if (math.lengthsq(deltaPosition) < MathTools.o91)
        {
            return;
        }

        NativeArray<PhysicsQuery.WorldCastResult> castResults = default;

        var transform = terminusAnchor.transform;
        transform.position = (float2)(transform.position - transform.TransformPoint(terminusAnchorLocalPos)) + position[^1];

        if (terminusAnchor.isValid)
        {
            switch (anchorGeometry->ShapeType)
            {
                case PhysicsShape.ShapeType.Circle:
                    {
                        var geom = anchorGeometry->CircleGeometry().Transform(transform);
                        castResults = world.CastGeometry(geom, deltaPosition, collisionFilter);
                        break;
                    }
                case PhysicsShape.ShapeType.Polygon:
                    {
                        var geom = anchorGeometry->PolygonGeometry().Transform(transform);
                        castResults = world.CastGeometry(geom, deltaPosition, collisionFilter);
                        break;
                    }
                case PhysicsShape.ShapeType.Capsule:
                    {
                        var geom = anchorGeometry->CapsuleGeometry().Transform(transform);
                        castResults = world.CastGeometry(geom, deltaPosition, collisionFilter);
                        break;
                    }
            }
        }

        //2do: anchorGeometry->Radius is just temporary (since testing circle anchor) until we add more shapes (or give every shape an encapsulating radius and treat as circle)
        HandleCastResults(position.Length - 1, deltaPosition, position, lastPosition, collisionData, shapeCapture, collisionBounciness, stepVelocity, 
            transform.TransformPoint(anchorGeometry->Center1), anchorGeometry->Radius, castResults, 0);
    }

    public static void HandleCastResults(int i, float2 deltaPosition, NativeArray<float2> position, NativeArray<float2> lastPosition,
        NativeArray<RopeCollisionDebugData> collisionData, NativeParallelHashMap<uint, PhysicsCoreHelper.ShapeProxy> shapeCapture,
        float collisionBounciness, bool stepVelocity, float2 center, float radius,
        NativeArray<PhysicsQuery.WorldCastResult> castResults, int resultIndex)
    {
        if (castResults.IsCreated && resultIndex < castResults.Length)
        {
            var result = castResults[resultIndex];
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
                    float2 separation;
                    (separation, normal) = CollisionUtilities.SeparateCircleFromShape(positionAtTimeOfImpact + center - position[i], radius + 0.001f, proxy, shape.transform);
                    positionAtTimeOfImpact += separation;
                    lastPosition[i] += separation;

                    if (separation.Equals(0))
                    {
                        colData.failure = true;
                    }
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