using System;
using Unity.Collections;
using Unity.U2D.Physics;
using UnityEngine;


public static class PhysicsCoreHelper
{
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
    }

    public static PhysicsBody CreatePolygonBody(PhysicsWorld world, PhysicsBodyDefinition bodyDef, PhysicsShapeDefinition shapeDef, 
        Vector2 scale, ReadOnlySpan<Vector2> polygon)
    {
        var body = world.CreateBody(bodyDef);

        var geoms = PolygonGeometry.CreatePolygons(polygon, PhysicsTransform.identity, scale);
        body.CreateShapeBatch(geoms, shapeDef);

        return body;
    }

    public static PhysicsBody CreatePolygonBody(PhysicsWorld world, PhysicsBodyDefinition bodyDef, PhysicsShapeDefinition shapeDef,
        Vector2 scale, PolygonGeometry[] subdividedPolygon)
    {
        var body = world.CreateBody(bodyDef);

        var scaleMatrix = Matrix4x4.Scale(new Vector3(scale.x, scale.y, 1));
        for (int i = 0; i < subdividedPolygon.Length; i++)
        {
            subdividedPolygon[i] = PolygonGeometry.Create(subdividedPolygon[i].AsReadOnlySpan(), 0f, scaleMatrix);
        }

        body.CreateShapeBatch(subdividedPolygon, shapeDef);

        return body;
    }

    public static PhysicsBody CreateCirceBody(PhysicsWorld world, PhysicsBodyDefinition bodyDef, PhysicsShapeDefinition shapeDef,
        float radius, out PhysicsShape shape)
    {
        var body = world.CreateBody(bodyDef);

        CircleGeometry circleGeom = new() { radius = radius };
        shape = body.CreateShape(circleGeom, shapeDef);

        return body;
    }

    public static PhysicsBody CreateCapsuleBody(PhysicsWorld world, PhysicsBodyDefinition bodyDef, PhysicsShapeDefinition shapeDef,
        Vector2 capCenter1, Vector2 capCenter2, float capRadius, out PhysicsShape shape)
    {
        var body = world.CreateBody(bodyDef);

        CapsuleGeometry capsuleGeom = new()
        {
            center1 = capCenter1,
            center2 = capCenter2,
            radius = capRadius
        };
        shape = body.CreateShape(capsuleGeom, shapeDef);

        return body;
    }

    public static PhysicsBody CreateBoxBody(PhysicsWorld world, PhysicsBodyDefinition bodyDef, PhysicsShapeDefinition shapeDef, Vector2 fullSize, out PhysicsShape shape)
    {
        var body = world.CreateBody(bodyDef);

        PolygonGeometry boxGeom = PolygonGeometry.CreateBox(fullSize);
        shape = body.CreateShape(boxGeom, shapeDef);

        return body;
    }
}