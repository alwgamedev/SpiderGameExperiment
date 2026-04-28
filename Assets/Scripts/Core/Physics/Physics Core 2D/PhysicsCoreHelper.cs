using Unity.Collections;
using Unity.U2D.Physics;
using UnityEditor;
using UnityEngine;
using System;

public static class PhysicsCoreHelper
{
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
        float radius, Matrix4x4 shapeInputSpace)
    {
        var body = world.CreateBody(bodyDef);

        var circleGeom = CircleGeometry.Create(radius).Transform(shapeInputSpace, true).InverseTransform(body.transform);
        body.CreateShape(circleGeom, shapeDef);

        return body;
    }

    public static PhysicsBody CreateCapsuleBody(PhysicsWorld world, PhysicsBodyDefinition bodyDef, PhysicsShapeDefinition shapeDef,
        Vector2 capCenter1, Vector2 capCenter2, float capRadius, Matrix4x4 shapeInputSpace)
    {
        var body = world.CreateBody(bodyDef);

        var capsuleGeom = CapsuleGeometry.Create(capCenter1, capCenter2, capRadius).Transform(shapeInputSpace, true).InverseTransform(body.transform);
        body.CreateShape(capsuleGeom, shapeDef);

        return body;
    }

    public static PhysicsBody CreateCapsuleBody(PhysicsWorld world, PhysicsBodyDefinition bodyDef, PhysicsShapeDefinition shapeDef,
        Vector2 capsuleSize, Vector2 capsuleOffset, Matrix4x4 shapeInputSpace)
    {
        var body = world.CreateBody(bodyDef);
        var capsuleGeom = CreateCapsule(capsuleSize, capsuleOffset).Transform(shapeInputSpace, true).InverseTransform(body.transform);
        body.CreateShape(capsuleGeom, shapeDef);

        return body;
    }

    public static PhysicsBody CreateBoxBody(PhysicsWorld world, PhysicsBodyDefinition bodyDef, PhysicsShapeDefinition shapeDef, 
        Vector2 fullSize, Matrix4x4 shapeInputSpace)
    {
        var body = world.CreateBody(bodyDef);

        PolygonGeometry boxGeom = PolygonGeometry.CreateBox(fullSize).Transform(shapeInputSpace, true).InverseTransform(body.transform);
        body.CreateShape(boxGeom, shapeDef);

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

    public static void UpdateSettings(this PhysicsHingeJoint joint, PhysicsHingeJointDefinition def)
    {
        joint.enableSpring = def.enableSpring;
        joint.springTargetAngle = def.springTargetAngle;
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
        joint.tuningDamping= def.tuningDamping;
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