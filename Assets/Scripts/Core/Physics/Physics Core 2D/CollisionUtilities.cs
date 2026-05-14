using System;
using Unity.Burst;
using Unity.Mathematics;
using Unity.U2D.Physics;

public static class CollisionUtilities
{
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

    public static void SeparateCapsuleFromCircle()
    {

    }
}