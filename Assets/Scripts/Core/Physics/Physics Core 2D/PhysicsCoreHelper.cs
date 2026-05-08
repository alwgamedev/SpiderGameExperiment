using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Unity.U2D.Physics;
using UnityEditor;
using UnityEngine;

public static class PhysicsCoreHelper
{
    public static Dictionary<uint, PhysicsShape> ShapeLookup;

    public static Dictionary<uint, PhysicsBody> BodyLookup;

    //REFLECT AND ROTATE 

    public static PhysicsTransform RotateAroundPoint(this PhysicsTransform t, PhysicsRotate rot, Vector2 rotLocalCenter)
    {
        var rotWorldCenter = t.TransformPoint(rotLocalCenter);
        t.rotation = t.rotation.MultiplyRotation(rot);
        t.position += rotWorldCenter - t.TransformPoint(rotLocalCenter);
        return t;
    }

    /// <summary>
    /// Reflect transform A over transform B's y-axis, with transform B's position as the origin.
    /// </summary>
    public static PhysicsTransform Reflect(this PhysicsTransform tA, PhysicsTransform tB)
    {
        var origin = tB.position;
        //var hyperplaneNormal = tB.rotation.direction;
        //var rotHyperplaneNormal = changeDirection ? hyperplaneNormal.CCWPerp() : hyperplaneNormal;
        tA.position = origin + (tA.position - origin).ReflectAcrossHyperplane(tB.rotation.direction);
        tA.rotation = new PhysicsRotate(tA.rotation.direction.ReflectAcrossHyperplane(tB.rotation.direction));
        return tA;
    }

    /// <summary>
    /// Reflect transform A over transform B's y-axis, with transform B's position as the origin, 
    /// then rotate A an additional 180 degrees with respect to given center.
    /// </summary>
    public static PhysicsTransform ReflectAndFlip(this PhysicsTransform tA, PhysicsTransform tB, Vector2 flipLocalCenter)
    {
        return tA.Reflect(tB).RotateAroundPoint(PhysicsRotate.left, flipLocalCenter);
    }

    /// <summary>
    /// Use in conjunction with reflect methods if reflected physics bodies are connected by joints.
    /// </summary>
    public static void ReflectAndFlipAnchors(this PhysicsJoint joint)
    {
        joint.localAnchorA = joint.localAnchorA.ReflectAndFlip(PhysicsTransform.identity, Vector2.zero);
        joint.localAnchorB = joint.localAnchorB.ReflectAndFlip(PhysicsTransform.identity, Vector2.zero);
    }

    /// <summary>
    /// Use e.g. for a bone rig childed to a physics transform to make the rigged character change direction.
    /// </summary>
    public static void ReflectAndFlip(this Transform transform, PhysicsTransform reflection)
    {
        var s = transform.localScale;
        s.x *= -1;
        transform.localScale = s;
        var q = MathTools.QuaternionFrom2DUnitVector(reflection.rotation.direction);
        q = q * MathTools.InverseOfUnitQuaternion(transform.rotation) * q;
        var p = reflection.position + ((Vector2)transform.position - reflection.position).ReflectAcrossHyperplane(reflection.rotation.direction);
        transform.SetPositionAndRotation(p, q);

        //transform.rotation = q * MathTools.InverseOfUnitQuaternion(transform.rotation) * q;
        //transform.position = reflection.position + ((Vector2)transform.position - reflection.position).ReflectAcrossHyperplane(reflection.rotation.direction);
    }


    //QUERIES

    /// <summary> A ShapeProxy that can be used in Jobs (built-in ShapeProxy has potential to throw errors when you access geometry, which stalls jobs). 
    /// Use Vertex(0) for circle center, and use Vertex(0), Vertex(1) for capsule centers 1, 2.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct ShapeProxy
    {
        [FieldOffset(0)] fixed float vertex[16];
        [FieldOffset(64)] fixed float normal[16];//only for polygons
        [FieldOffset(128)] PhysicsShape.ShapeType shapeType;
        [FieldOffset(132)] int count;

        public readonly PhysicsShape.ShapeType ShapeType => shapeType;
        public readonly float Radius
        {
            get
            {
                fixed (float* normalPtr = normal)
                {
                    return *normalPtr;
                }
            }
        }
        public readonly int Count => count;

        public readonly Vector2 Vertex(int i)
        {
            fixed (float* vertexPtr = vertex)
            {
                return ((Vector2*)vertexPtr)[i];
            }
        }

        public readonly Vector2 Normal(int i)
        {
            fixed (float* normalPtr = normal)
            {
                return ((Vector2*)normalPtr)[i];
            }
        }

        public ShapeProxy(CircleGeometry circle)
        {
            shapeType = PhysicsShape.ShapeType.Circle;
            count = 1;

            fixed (float* vertexPtr = vertex, normalPtr = normal)
            {
                *normalPtr = circle.radius;
                *(Vector2*)vertexPtr = circle.center;
            }
        }

        public ShapeProxy(CapsuleGeometry capsule)
        {
            shapeType = PhysicsShape.ShapeType.Capsule;
            count = 2;

            fixed (float* vertexPtr = vertex, normalPtr = normal)
            {
                *normalPtr = capsule.radius;
                var c1 = capsule.center1;
                var c2 = capsule.center2;
                *(Vector4*)vertexPtr = new(c1.x, c1.y, c2.x, c2.y);
            }
        }

        public ShapeProxy(PolygonGeometry polygonGeometry)
        {
            shapeType = PhysicsShape.ShapeType.Polygon;
            count = polygonGeometry.count;
            fixed (float* vertexPtr = vertex, normalPtr = normal)
            {
                *(PhysicsShape.ShapeArray*)vertexPtr = polygonGeometry.vertices;
                *(PhysicsShape.ShapeArray*)normalPtr = polygonGeometry.normals;
            }
        }
    }

    public static PhysicsQuery.QueryFilter ToQueryFilter(this PhysicsShape.ContactFilter contactFilter,
        PhysicsWorld.IgnoreFilter ignoreFilter = PhysicsWorld.IgnoreFilter.None)
    {
        return new(contactFilter.categories, contactFilter.contacts, ignoreFilter);
    }

    public static NativeArray<PhysicsQuery.WorldCastResult> CastRay(this PhysicsWorld world, Vector2 origin, Vector2 translation, PhysicsQuery.QueryFilter filter,
        PhysicsQuery.WorldCastMode castMode = PhysicsQuery.WorldCastMode.Closest, Allocator allocator = Allocator.Temp)
    {
        return world.CastRay(new PhysicsQuery.CastRayInput(origin, translation), filter, castMode, allocator);
    }

    [BurstCompile]
    public static bool OverlapPoint(this in ShapeProxy shape, PhysicsTransform transform, Vector2 point, out Vector2 escapeNormal, out float escapeDistance)
    {
        point = transform.InverseTransformPoint(point);
        bool result;
        switch (shape.ShapeType)
        {
            case PhysicsShape.ShapeType.Circle:
                result = OverlapCircle(shape.Vertex(0), shape.Radius, point, out escapeNormal, out escapeDistance);
                break;
            case PhysicsShape.ShapeType.Polygon:
                result = OverlapPolygon(shape, point, out escapeNormal, out escapeDistance);
                break;
            case PhysicsShape.ShapeType.Capsule:
                result = OverlapCapsule(shape.Vertex(0), shape.Vertex(1), shape.Radius, point, out escapeNormal, out escapeDistance);
                break;
            default:
                escapeNormal = default;
                escapeDistance = default;
                return false;
        }

        escapeNormal = transform.rotation.RotateVector(escapeNormal);
        return result;
    }

    [BurstCompile]
    public static bool OverlapCircle(Vector2 center, float radius, Vector2 point, out Vector2 escapeNormal, out float escapeDistance)
    {
        var v = point - center;
        var d2 = v.sqrMagnitude;
        if (d2 > radius * radius)
        {
            escapeNormal = default;
            escapeDistance = 0;
            return false;
        }

        var d = math.min(math.sqrt(d2), radius);//just in case rounding errors make sqrt(d2) > radius
        escapeDistance = radius - d;
        escapeNormal = d < MathTools.o41 ? Vector2.up : v / d;
        return true;
    }

    [BurstCompile]
    public static bool OverlapPolygon(this in ShapeProxy polygon, Vector2 point, out Vector2 escapeNormal, out float escapeDistance)
    {
        escapeNormal = default;
        escapeDistance = -1;

        for (int i = 0; i < polygon.Count; i++)
        {
            var v = polygon.Vertex(i);
            var n = polygon.Normal(i);

            //vertices are ordered CCW, and normals[i] = outward normal to edge (vert[i], vert[i + 1])
            var dist = math.dot(v - point, n);
            if (dist < 0)
            {
                escapeNormal = default;
                return false;
            }

            if (escapeDistance < 0 || dist < escapeDistance)
            {
                escapeDistance = dist;
                escapeNormal = n;
            }
        }

        return true;
    }

    [BurstCompile]
    public static bool OverlapCapsule(Vector2 center1, Vector2 center2, float radius, Vector2 point, out Vector2 escapeNormal, out float escapeDistance)
    {
        var h = center2 - center1;
        var h2 = math.lengthsq(h);
        var v = point - center1;

        var up = h.CCWPerp();
        var y = math.dot(v, up);

        if (y * y > h2 * radius * radius)
        {
            escapeNormal = default;
            escapeDistance = 0;
            return false;
        }

        var x = math.dot(v, h);

        if (x < 0)
        {
            return OverlapCircle(center1, radius, point, out escapeNormal, out escapeDistance);
        }

        if (x > h2)
        {
            return OverlapCircle(center2, radius, point, out escapeNormal, out escapeDistance);
        }

        var a = math.rsqrt(h2);
        escapeNormal = y > 0 ? a * up : -a * up;
        escapeDistance = radius - a * math.abs(y);
        return true;
    }

    [BurstCompile]
    public static bool OverlapPoint(this PhysicsAABB box, Vector2 point, out Vector2 escapeNormal, out float escapeDistance)
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

        escapeNormal = Vector2.down;
        escapeNormal = math.select(escapeNormal, Vector2.right, escapeDistance == rlud.x);
        escapeNormal = math.select(escapeNormal, Vector2.left, escapeDistance == rlud.y);
        escapeNormal = math.select(escapeNormal, Vector2.up, escapeDistance == rlud.z);

        return true;
    }

    //SHAPES

    [BurstCompile]
    public static PhysicsAABB CalculateAABB(this PhysicsShape shape, PhysicsTransform transform)
    {
        switch (shape.shapeType)
        {
            case PhysicsShape.ShapeType.Circle:
                return shape.circleGeometry.CalculateAABB(transform);
            case PhysicsShape.ShapeType.Capsule:
                return shape.capsuleGeometry.CalculateAABB(transform);
            case PhysicsShape.ShapeType.Polygon:
                return shape.polygonGeometry.CalculateAABB(transform);
            case PhysicsShape.ShapeType.Segment:
                return shape.segmentGeometry.CalculateAABB(transform);
            case PhysicsShape.ShapeType.ChainSegment:
                return shape.chainSegmentGeometry.CalculateAABB(transform);
            default:
                return shape.aabb;

        }
    }

    //the built-in accessor for shape array throws an error when out of bounds, and including that in job code will cause stall (even when error is never reached)
    [BurstCompile]
    public static Vector2 GetVertexNoThrow(this PhysicsShape.ShapeArray shapeArray, int i)
    {
        return i switch
        {
            0 => shapeArray.vertex0,
            1 => shapeArray.vertex1,
            2 => shapeArray.vertex2,
            3 => shapeArray.vertex3,
            4 => shapeArray.vertex4,
            5 => shapeArray.vertex5,
            6 => shapeArray.vertex6,
            7 => shapeArray.vertex7,
            _ => shapeArray.vertex0
        };
    }

    //PHYSICS BODIES

    public static void SyncTransform(this PhysicsBody body)
    {
        body.GetPositionAndRotation3D(body.transformObject, body.world.transformWriteMode, body.world.transformPlane, out var p, out var q);
        body.transformObject.SetPositionAndRotation(p, q);
    }

    //don't set things like position, velocity, etc.
    public static void SetBodyDefLive(this PhysicsBody body, PhysicsBodyDefinition bodyDef)
    {
        body.type = bodyDef.type;
        body.constraints = bodyDef.constraints;
        body.transformWriteMode = bodyDef.transformWriteMode;
        body.linearDamping = bodyDef.linearDamping;
        body.angularDamping = bodyDef.angularDamping;
        body.gravityScale = bodyDef.gravityScale;
        body.sleepThreshold = bodyDef.sleepThreshold;
        body.fastRotationAllowed = bodyDef.fastRotationAllowed;
        body.fastRotationAllowed = bodyDef.fastCollisionsAllowed;
        body.sleepingAllowed = bodyDef.sleepingAllowed;
        body.worldDrawing = bodyDef.worldDrawing;
    }

    public static void SetShapeDef(this PhysicsBody body, PhysicsShapeDefinition shapeDef)
    {
        var shapes = body.GetShapes();
        for (int i = 0; i < shapes.Length; i++)
        {
            var s = shapes[i];
            s.definition = shapeDef;
            shapes[i] = s;
        }

        body.ApplyMassFromShapes();
    }

    public static PhysicsBody CreatePolygonBody(PhysicsWorld world, PhysicsBodyDefinition bodyDef, PhysicsShapeDefinition shapeDef,
        Matrix4x4 shapeInputSpace, Span<PolygonGeometry> subdividedPolygon)
    {
        var body = world.CreateBody(bodyDef);
        Span<PolygonGeometry> transformedPolygon = stackalloc PolygonGeometry[subdividedPolygon.Length];

        for (int i = 0; i < subdividedPolygon.Length; i++)
        {
            transformedPolygon[i] = subdividedPolygon[i].Transform(shapeInputSpace, true).InverseTransform(body.transform);
        }

        body.CreateShapeBatch(transformedPolygon, shapeDef);

        return body;
    }

    public static PhysicsBody CreateCircleBody(PhysicsWorld world, PhysicsBodyDefinition bodyDef, PhysicsShapeDefinition shapeDef,
        float radius, Matrix4x4 shapeInputSpace, out PhysicsShape shape)
    {
        var body = world.CreateBody(bodyDef);

        var circleGeom = CircleGeometry.Create(radius).Transform(shapeInputSpace, true).InverseTransform(body.transform);
        shape = body.CreateShape(circleGeom, shapeDef);

        return body;
    }

    public static PhysicsBody CreateCapsuleBody(PhysicsWorld world, PhysicsBodyDefinition bodyDef, PhysicsShapeDefinition shapeDef,
        Vector2 capCenter1, Vector2 capCenter2, float capRadius, Matrix4x4 shapeInputSpace, out PhysicsShape shape)
    {
        var body = world.CreateBody(bodyDef);

        var capsuleGeom = CapsuleGeometry.Create(capCenter1, capCenter2, capRadius).Transform(shapeInputSpace, true).InverseTransform(body.transform);
        shape = body.CreateShape(capsuleGeom, shapeDef);

        return body;
    }

    public static PhysicsBody CreateCapsuleBody(PhysicsWorld world, PhysicsBodyDefinition bodyDef, PhysicsShapeDefinition shapeDef,
        Vector2 capsuleSize, Vector2 capsuleOffset, Matrix4x4 shapeInputSpace, out PhysicsShape shape)
    {
        var body = world.CreateBody(bodyDef);
        var capsuleGeom = CreateCapsule(capsuleSize, capsuleOffset).Transform(shapeInputSpace, true).InverseTransform(body.transform);
        shape = body.CreateShape(capsuleGeom, shapeDef);

        return body;
    }

    public static PhysicsBody CreateBoxBody(PhysicsWorld world, PhysicsBodyDefinition bodyDef, PhysicsShapeDefinition shapeDef,
        Vector2 fullSize, Matrix4x4 shapeInputSpace, out PhysicsShape shape)
    {
        var body = world.CreateBody(bodyDef);

        PolygonGeometry boxGeom = PolygonGeometry.CreateBox(fullSize).Transform(shapeInputSpace, true).InverseTransform(body.transform);
        shape = body.CreateShape(boxGeom, shapeDef);

        return body;
    }

    /// <summary>
    /// capsuleSize is the full (width, height) of the capsule. Provide an inputTransform if capsuleSize and capsuleOffset are in the local space of some Transform.
    /// </summary>
    public static CapsuleGeometry CreateCapsule(Vector2 capsuleSize, Vector2 capsuleOffset)
    {
        var midPt = capsuleOffset;
        var c1 = midPt + new Vector2(-0.5f * (capsuleSize.x - capsuleSize.y), 0);
        var c2 = midPt + new Vector2(0.5f * (capsuleSize.x - capsuleSize.y), 0);

        return new CapsuleGeometry()
        {
            center1 = c1,
            center2 = c2,
            radius = 0.5f * capsuleSize.y
        };
    }


    public static void DrawCapsule(Color color, Vector2 capsuleSize, Vector2 capsuleOffset, Transform inputSpace = null)
    {
        var midPt = capsuleOffset;
        var leftEndpt = midPt + new Vector2(-0.5f * capsuleSize.x, 0);
        var c1 = leftEndpt + new Vector2(0.5f * capsuleSize.y, 0);
        var c2 = midPt + new Vector2(0.5f * (capsuleSize.x - capsuleSize.y), 0);
        var topLeft = c1 + new Vector2(0, 0.5f * capsuleSize.y);
        var topRight = c2 + new Vector2(0, 0.5f * capsuleSize.y);
        var bottomLeft = c1 + new Vector2(0, -0.5f * capsuleSize.y);
        var bottomRight = c2 + new Vector2(0, -0.5f * capsuleSize.y);
        var r = 0.5f * capsuleSize.y;

        var transformMat = inputSpace ? inputSpace.localToWorldMatrix : Matrix4x4.identity;

        using (new Handles.DrawingScope(color, transformMat))
        {
            Handles.DrawWireArc(c1, Vector3.forward, topLeft - c1, 180, r);
            Handles.DrawWireArc(c2, Vector3.forward, bottomRight - c2, 180, r);
            Handles.DrawLine(topLeft, topRight);
            Handles.DrawLine(bottomLeft, bottomRight);
        }
    }

    //JOINTS

    public static void UpdateSettings(this PhysicsFixedJoint joint, PhysicsFixedJointDefinition def)
    {
        joint.linearFrequency = def.linearFrequency;
        joint.linearDamping = def.linearDamping;
        joint.angularFrequency = def.angularFrequency;
        joint.angularDamping = def.angularDamping;
        joint.forceThreshold = def.forceThreshold;
        joint.torqueThreshold = def.torqueThreshold;
        joint.tuningFrequency = def.tuningFrequency;
        joint.tuningDamping = def.tuningDamping;
        joint.drawScale = def.drawScale;
        joint.worldDrawing = def.worldDrawing;
        joint.collideConnected = def.collideConnected;
    }

    public static void UpdateSettings(this PhysicsHingeJoint joint, PhysicsHingeJointDefinition def,
        bool keepEnableSpring, bool keepSpringTargetAngle)
    {
        if (!keepEnableSpring)
        {
            joint.enableSpring = def.enableSpring;
        }
        if (!keepSpringTargetAngle)
        {
            joint.springTargetAngle = def.springTargetAngle;
        }
        joint.springFrequency = def.springFrequency;
        joint.springDamping = def.springDamping;

        joint.enableMotor = def.enableMotor;
        joint.motorSpeed = def.motorSpeed;
        joint.maxMotorTorque = def.maxMotorTorque;

        joint.enableLimit = def.enableLimit;
        joint.lowerAngleLimit = def.lowerAngleLimit;
        joint.upperAngleLimit = def.upperAngleLimit;

        joint.forceThreshold = def.forceThreshold;
        joint.torqueThreshold = def.torqueThreshold;
        joint.tuningFrequency = def.tuningFrequency;
        joint.tuningDamping = def.tuningDamping;
        joint.drawScale = def.drawScale;
        joint.worldDrawing = def.worldDrawing;
        joint.collideConnected = def.collideConnected;
    }

    public static PhysicsRotate UpperLimitSeparation(this PhysicsHingeJoint joint)
    {
        var cur = joint.bodyB.rotation.MultiplyRotation(joint.localAnchorB.rotation);
        var upperLimit = joint.bodyA.rotation.MultiplyRotation(joint.localAnchorA.rotation.MultiplyRotation(PhysicsRotate.FromDegrees(joint.upperAngleLimit)));
        return upperLimit.InverseMultiplyRotation(cur);
    }

    public static PhysicsRotate LowerLimitSeparation(this PhysicsHingeJoint joint)
    {
        //joint world rotation
        var cur = joint.bodyB.rotation.MultiplyRotation(joint.localAnchorB.rotation);
        var lowerLimit = joint.bodyA.rotation.MultiplyRotation(joint.localAnchorA.rotation.MultiplyRotation(PhysicsRotate.FromDegrees(joint.lowerAngleLimit)));
        return lowerLimit.InverseMultiplyRotation(cur);
    }
}