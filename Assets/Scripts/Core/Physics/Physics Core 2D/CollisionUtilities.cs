using System;
using Unity.Burst;
using Unity.Mathematics;
using Unity.U2D.Physics;

public static class CollisionUtilities
{
    //All separation methods will return (separation, normal) = (0, 0) if there is no overlap, otherwise sep and normal are always valid.

    [BurstCompile]
    public static (float separation, float2 normal) SeparateCircleFromShape(float2 center, float radius, PhysicsCoreHelper.ShapeProxyForJobs shape, PhysicsTransform transform)
    {
        switch (shape.ShapeType)
        {
            case PhysicsShape.ShapeType.Circle:
                return SeparateCircleFromCircle(center, radius, transform.TransformPoint(shape.Center1), shape.Radius);
            case PhysicsShape.ShapeType.Polygon:
                center = transform.InverseTransformPoint(center);//do calculation in shape's local space to avoid transforming all the vertices and normals
                var (separation, normal) = SeparateCircleFromPolygon(center, radius, shape.VertexArray, shape.NormalArray);
                return (separation, transform.rotation.RotateVector(normal));
            case PhysicsShape.ShapeType.Capsule:
                return SeparateCircleFromCapsule(center, radius, transform.TransformPoint(shape.Center1), transform.TransformPoint(shape.Center2), shape.Radius);
            default:
                return default;
        }
    }

    //separates circle1 from circle2 (i.e. normal points from center2 to center1)
    [BurstCompile]
    public static (float separation, float2 normal) SeparateCircleFromCircle(float2 center1, float radius1, float2 center2, float radius2)
    {
        var d = center1 - center2;
        var d2 = math.lengthsq(d);
        var radiusSum = radius1 + radius2;

        if (d2 > radiusSum * radiusSum)
        {
            return default;
        }

        if (d2 < MathTools.o91)
        {
            return (radiusSum, new float2(1, 0));
        }

        var dInv = math.rsqrt(d2);
        var normal = dInv * d;
        return (math.max(radiusSum - math.dot(d, normal), 0), normal);
    }

    [BurstCompile]
    public static (float separation, float2 normal) SeparateCircleFromCapsule(float2 circleCenter, float circleRadius, float2 capsuleCenter1, float2 capsuleCenter2, float capsuleRadius)
    {
        var h = capsuleCenter2 - capsuleCenter1;
        var h2 = math.lengthsq(h);
        var v = circleCenter - capsuleCenter1;

        var x = math.dot(v, h);

        if (x < 0)
        {
            return SeparateCircleFromCircle(circleCenter, circleRadius, capsuleCenter1, capsuleRadius);
        }

        if (x > h2)
        {
            return SeparateCircleFromCircle(circleCenter, circleRadius, capsuleCenter2, capsuleRadius);
        }

        var up = h.CCWPerp();
        var y = math.dot(v, up);
        var radiusSum = circleRadius + capsuleRadius;
        if (y * y > h2 * radiusSum * radiusSum)//no overlap
        {
            return default;
        }

        var a = math.rsqrt(h2);
        var normal = y > 0 ? a * up : -a * up;
        var yAbs = a * math.abs(y);
        return (math.max(radiusSum - yAbs, 0), normal);
    }

    [BurstCompile]
    public static (float separation, float2 normal) SeparateCircleFromPolygon (float2 center, float radius, ReadOnlySpan<float2> vertex, ReadOnlySpan<float2> normal)
    {

        float2 minNormal = normal[0];
        float minDist = math.dot(vertex[0] - center, minNormal);

        if (minDist < -radius)
        {
            return default;
        }

        for (int i = 1; i < vertex.Length; i++)
        {
            var n = normal[i];
            if (n.Equals(0))
            {
                //my shape proxy struct doesn't have a vertex count -- you have to just stop once the normals become zero
                break;
            }

            var dist = math.dot(vertex[i] - center, n);

            if (dist < -radius)
            {
                return default;
            }

            if (dist < minDist)
            {
                minDist = dist;
                minNormal = n;
            }
        }

        return (minDist + radius, minNormal);
    }

    [BurstCompile]
    public static (float2 escapeNormal, float escapeDistance) OverlapPoint(this in PhysicsCoreHelper.ShapeProxyForJobs shape, PhysicsTransform transform, float2 point)
    {
        point = transform.InverseTransformPoint(point);
        float2 escapeNormal;
        float escapeDistance;
        switch (shape.ShapeType)
        {
            case PhysicsShape.ShapeType.Circle:
                (escapeNormal, escapeDistance) = OverlapCircle(shape.Center1, shape.Radius, point);
                break;
            case PhysicsShape.ShapeType.Polygon:
                (escapeNormal, escapeDistance) = OverlapPolygon(shape.VertexArray, shape.NormalArray, point);
                break;
            case PhysicsShape.ShapeType.Capsule:
                (escapeNormal, escapeDistance) = OverlapCapsule(shape.Center1, shape.Center2, shape.Radius, point);
                break;
            default:
                return default;
        }

        escapeNormal = transform.rotation.RotateVector(escapeNormal);
        return (escapeNormal, escapeDistance);
    }

    [BurstCompile]
    public static (float2 escapeNormal, float escapeDistance) OverlapCircle(float2 center, float radius, float2 point)
    {
        var v = point - center;
        var d2 = math.lengthsq(v);

        if (d2 < MathTools.o91)
        {
            return (new float2(0, 1), radius);
        }

        var d = math.sqrt(d2);
        return (v / d, radius - d);
    }

    /// <summary> Returns normal and distance to closest edge (if starting point is inside polygon).</summary>
    [BurstCompile]
    public static unsafe (float2 escapeNormal, float escapeDistance) OverlapPolygon(ReadOnlySpan<float2> vertex, ReadOnlySpan<float2> normal, float2 point)
    {
        float2 escapeNormal = normal[0];
        float escapeDistance = math.dot(vertex[0] - point, escapeNormal);

        for (int i = 1; i < vertex.Length; i++)
        {
            float2 v = vertex[i];
            float2 n = normal[i];

            //vertices are ordered CCW, and normal[i] = outward normal to edge (vert[i], vert[i + 1])
            var dist = math.dot(v - point, n);

            if (dist < escapeDistance)
            {
                escapeDistance = dist;
                escapeNormal = n;
            }
        }

        return (escapeNormal, escapeDistance);
    }

    /// <summary> Like OverlapPolygon, but tries to find an edge where the escape point does not overlap any other shapes (not necessarily closest edge).</summary>
    [BurstCompile]
    public static (float2 escapeNormal, float escapeDistance) EscapePolygon(ReadOnlySpan<float2> vertex, ReadOnlySpan<float2> normal, float2 point,
        float2 worldPoint, PhysicsRotate worldRotation, PhysicsWorld world, PhysicsQuery.QueryFilter escapeFilter)
    {
        float2 escapeNormal = normal[0];
        float escapeDistance = math.dot(vertex[0] - point, escapeNormal);
        bool escapeFound = !TestOverlapEscapePoint(world, escapeFilter, worldPoint, worldRotation, escapeNormal, escapeDistance);

        for (int i = 1; i < vertex.Length; i++)
        {
            float2 v = vertex[i];
            float2 n = normal[i];

            //vertices are ordered CCW, and normal[i] = outward normal to edge (vert[i], vert[i + 1])
            var dist = math.dot(v - point, n);
            if (TestOverlapEscapePoint(world, escapeFilter, worldPoint, worldRotation, escapeNormal, escapeDistance))
            {
                if (!escapeFound && dist < escapeDistance)
                {
                    escapeNormal = n;
                    escapeDistance = dist;
                }
            }
            else
            {
                if (!escapeFound)
                {
                    escapeFound = true;
                    escapeNormal = n;
                    escapeDistance = dist;
                }
                else if (dist < escapeDistance)
                {
                    escapeNormal = n;
                    escapeDistance = dist;
                }
            }
        }

        return (escapeNormal, escapeDistance);

        static bool TestOverlapEscapePoint(in PhysicsWorld world, in PhysicsQuery.QueryFilter escapeFilter,
            in float2 worldPoint, in PhysicsRotate worldRotation, in float2 escapeNormal, in float escapeDistance)
        {
            var p = worldPoint + (escapeDistance + 0.01f) * (float2)worldRotation.RotateVector(escapeNormal);
            return world.TestOverlapPoint(p, escapeFilter);
        }
    }

    [BurstCompile]
    public static (float2 escapeNormal, float escapeDistance) OverlapCapsule(float2 center1, float2 center2, float radius, float2 point)
    {
        var h = center2 - center1;
        var h2 = math.lengthsq(h);
        var v = point - center1;

        var x = math.dot(v, h);

        if (x < 0)
        {
            return OverlapCircle(center1, radius, point);
        }

        if (x > h2)
        {
            return OverlapCircle(center2, radius, point);
        }

        var up = h.CCWPerp();
        var y = math.dot(v, up);

        var a = math.rsqrt(h2);
        var escapeNormal = y > 0 ? a * up : -a * up;
        var escapeDistance = radius - a * math.abs(y);
        return (escapeNormal, escapeDistance);
    }

    [BurstCompile]
    public static bool OverlapPoint(this PhysicsAABB box, float2 point, out float2 escapeNormal, out float escapeDistance)
    {
        if (point.x > box.upperBound.x || point.y > box.upperBound.y || point.x < box.lowerBound.x || point.y < box.lowerBound.y)
        {
            escapeNormal = default;
            escapeDistance = 0;
            return false;
        }

        var rlud = new float4(
            box.upperBound.x - point.x,
            point.x - box.lowerBound.x,
            box.upperBound.y - point.y,
            point.y - box.lowerBound.y
            );

        escapeDistance = math.cmin(rlud);

        escapeNormal = new float2(0, -1);
        escapeNormal = math.select(escapeNormal, new float2(1, 0), escapeDistance == rlud.x);
        escapeNormal = math.select(escapeNormal, new float2(-1, 0), escapeDistance == rlud.y);
        escapeNormal = math.select(escapeNormal, new float2(0, 1), escapeDistance == rlud.z);

        return true;
    }
}