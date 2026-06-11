using System;
using UnityEngine;
using Unity.U2D.Physics;
using UnityEditor;

[Serializable]
public struct SpiderBodyDefinition
{
    public PhysicsFixedJointDefinition headJointDef;
    public PhysicsBodyDefinition bodyDef;
    public PhysicsShapeDefinition shapeDef;
    public PBFDynamicObstacleSO fluidObstacle;

    public Vector2 abdomenCapsuleSize;//(width, height) -- full width and height
    public Vector2 abdomenCapsuleOffset;
    public Vector2 headCapsuleSize;
    public Vector2 headCapsuleOffset;
    public Vector2 grappleArmBoxOffset;
    public Vector2 grappleArmBoxSize;

}


[Serializable]
public struct SpiderBody
{
    public PhysicsBody abdomen;
    public PhysicsShape abdomenCapsule;
    public PhysicsShape grappleArm;
    public PhysicsBody head;
    public PhysicsFixedJoint headJoint;

    /// <summary> (when facing right) </summary>
    [NonSerialized] public PhysicsRotate abdomenRotationFromBase;
    PhysicsRotate abdomenBaseRotationFromLevel;

    float totalMass;
    bool facingRight;

    public readonly PhysicsWorld World => abdomen.world;
    public readonly bool FacingRight => facingRight;
    public readonly int Orientation => FacingRight ? 1 : -1;
    public readonly float TotalMass => totalMass;
    /// <summary> (accurate when facing right; when facing left it is the inverse of this) </summary>
    public readonly PhysicsRotate AbdomenBaseRotationFromLevel => abdomenBaseRotationFromLevel;
    //The heightRefPos and levelRight make up the spider's "true" transform, now that spider is made up of three separate bodies
    public readonly PhysicsTransform VirtualTransform => new(HeightReferencePosition, LevelRight);
    public readonly PhysicsRotate LevelRight
    {
        get
        {
            var abdomenAngleFromLevel = abdomenRotationFromBase.MultiplyRotation(abdomenBaseRotationFromLevel);
            //^(when facing right; inverse of this when facing left)
            return FacingRight ? abdomenAngleFromLevel.InverseMultiplyRotation(abdomen.rotation)
                : abdomenAngleFromLevel.MultiplyRotation(abdomen.rotation);
        }
    }
    public readonly Vector2 HeightReferencePosition => abdomen.transform.TransformPoint(headJoint.localAnchorA.position);

    public readonly bool HasContact() => abdomen.GetContacts().Length > 0 || head.GetContacts().Length > 0;

    public void OnValidate(SpiderBodyDefinition def)
    {
        if (Application.isPlaying && abdomen.isValid)
        {
            var gAbd = abdomen.gravityScale;
            abdomen.SetBodyDefLive(def.bodyDef);
            abdomenCapsule.definition = def.shapeDef;
            var grappleArmShapeDef = def.shapeDef;
            grappleArmShapeDef.density *= grappleArmDensityMultiplier;
            grappleArm.definition = grappleArmShapeDef;
            abdomen.ApplyMassFromShapes();
            abdomen.gravityScale = gAbd;

            var gHead = head.gravityScale;
            head.SetBodyDefLive(def.bodyDef);
            head.SetShapeDef(def.shapeDef);
            head.gravityScale = gHead;

            headJoint.UpdateSettings(def.headJointDef);

            totalMass = abdomen.mass + head.mass;
        }
    }

#if UNITY_EDITOR
    public void CenterRootTransforms(Transform abdomenRoot, Transform abdomenBone, Transform headRoot, Transform headBone,
        SpiderBodyDefinition def)
    {
        CenterRootInCapsule(abdomenRoot, abdomenBone, def.abdomenCapsuleSize, def.abdomenCapsuleOffset);
        CenterRootInCapsule(headRoot, headBone, def.headCapsuleSize, def.headCapsuleOffset);
    }

    public static void CenterRootInCapsule(Transform root, Transform bone, Vector2 capsuleSize, Vector2 capsuleOffset)
    {
        var recordTargets = new UnityEngine.Object[] { root, bone };
        Undo.RecordObjects(recordTargets, "Center root in capsule");

        var center = bone.transform.TransformPoint(capsuleOffset);
        var p = bone.position;
        root.position = center;
        bone.position = p;

        PrefabUtility.RecordPrefabInstancePropertyModifications(root);
        PrefabUtility.RecordPrefabInstancePropertyModifications(bone);

        EditorUtility.SetDirty(root);
        EditorUtility.SetDirty(bone);
    }
#endif

    public void DrawGizmos(Transform abdomenBone, Transform headBone, Transform grappleArmTransform,
        SpiderBodyDefinition def)
    {
        if (abdomenBone)
        {
            PhysicsCoreHelper.DrawCapsule(Color.orange, def.abdomenCapsuleSize, def.abdomenCapsuleOffset, abdomenBone);
        }

        if (headBone)
        {
            PhysicsCoreHelper.DrawCapsule(Color.orange, def.headCapsuleSize, def.headCapsuleOffset, headBone);
        }

        if (grappleArmTransform)
        {
            var m = Matrix4x4.TRS((Vector2)grappleArmTransform.position + def.grappleArmBoxOffset, grappleArmTransform.rotation,
                def.grappleArmBoxSize);
            using (new Handles.DrawingScope(Color.orange, m))
            {
                Handles.DrawLine(new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f));
                Handles.DrawLine(new Vector2(0.5f, -0.5f), new Vector2(0.5f, 0.5f));
                Handles.DrawLine(new Vector2(0.5f, 0.5f), new Vector2(-0.5f, 0.5f));
                Handles.DrawLine(new Vector2(-0.5f, 0.5f), new Vector2(-0.5f, -0.5f));
            }
        }
    }

    const float grappleArmDensityMultiplier = 0.001f;

    public void CreatePhysicsBody(PhysicsRotate levelDirection, Transform abdomenRoot, Transform headRoot, Transform headBone,
        Transform grappleArmTransform, SpiderBodyDefinition spiderDef)
    {
        var defaultWorld = PhysicsWorld.defaultWorld;

        var bodyDef = spiderDef.bodyDef;
        var shapeDef = spiderDef.shapeDef;
        //create abdomen
        bodyDef.position = abdomenRoot.position;
        bodyDef.rotation = new PhysicsRotate(abdomenRoot.rotation, PhysicsWorld.TransformPlane.XY);
        abdomen = PhysicsCoreHelper.CreateCapsuleBody(defaultWorld, bodyDef, shapeDef, spiderDef.abdomenCapsuleSize,
            Vector2.zero, abdomenRoot.localToWorldMatrix, out abdomenCapsule);
        //capsule will be centered at root position and won't use offset field, so need to properly position root and bone in editor before play
        //(have written an editor function to do this)
        abdomen.transformObject = abdomenRoot;

        var boxOffset = spiderDef.grappleArmBoxOffset;
        var boxSize = spiderDef.grappleArmBoxSize;
        var grappleArmShapeDef = shapeDef;
        grappleArmShapeDef.density *= grappleArmDensityMultiplier;//so it doesn't throw off balance
        var grappleArmBox = GrappleArmWorldBox(grappleArmTransform, boxOffset, boxSize).InverseTransform(abdomen.transform);
        grappleArm = abdomen.CreateShape(grappleArmBox, grappleArmShapeDef);

        //create head
        bodyDef.position = headRoot.position;
        bodyDef.rotation = new PhysicsRotate(headRoot.rotation, PhysicsWorld.TransformPlane.XY);
        head = PhysicsCoreHelper.CreateCapsuleBody(defaultWorld, bodyDef, shapeDef, spiderDef.headCapsuleSize,
            Vector2.zero, headRoot.localToWorldMatrix, out var headCapsule);
        head.transformObject = headRoot;

        //set user data
        var abdomenUserData = abdomenCapsule.userData;
        abdomenUserData.objectValue = spiderDef.fluidObstacle;
        abdomenCapsule.userData = abdomenUserData;

        var headUserData = headCapsule.userData;
        headUserData.objectValue = spiderDef.fluidObstacle;
        headCapsule.userData = headUserData;

        //fixed joints: anchorB on bodyB will be pulled towards anchorA on bodyA;
        //for rotation that means bodyB will rotate so that its anchor direction lines up with the anchor direction on bodyA
        var headJointDef = spiderDef.headJointDef;
        headJointDef.bodyA = abdomen;
        headJointDef.bodyB = head;
        headJointDef.localAnchorB = new(head.transform.InverseTransformPoint(headBone.position));
        headJointDef.localAnchorA = abdomen.transform.InverseMultiplyTransform(
            head.transform.MultiplyTransform(headJointDef.localAnchorB));
        headJoint = PhysicsFixedJoint.Create(defaultWorld, headJointDef);

        abdomenBaseRotationFromLevel = levelDirection.InverseMultiplyRotation(abdomen.rotation);
        abdomenRotationFromBase = PhysicsRotate.identity;

        abdomen.ApplyMassFromShapes();
        head.ApplyMassFromShapes();
        totalMass = abdomen.mass + head.mass;
        facingRight = true;

        PhysicsRegistry.RegisterBodyAndShapes(head);
        PhysicsRegistry.RegisterBodyAndShapes(abdomen);
    }

    public void Enable()
    {
        if (head.isValid)
        {
            head.enabled = true;
        }
        if (abdomen.isValid)
        {
            abdomen.enabled = true;
        }
    }

    public void Disable()
    {
        if (head.isValid)
        {
            head.enabled = false;
        }
        if (abdomen.isValid)
        {
            abdomen.enabled = false;
        }
    }

    public void Destroy()
    {
        if (abdomen.isValid)
        {
            abdomen.Destroy();
        }
        if (head.isValid)
        {
            head.Destroy();
        }
    }

    public void SetHeadRotation(PhysicsRotate worldRotation)
    {
        if (worldRotation.isValid)
        {
            var anchorA = headJoint.localAnchorA;
            anchorA.rotation = abdomen.rotation.InverseMultiplyRotation(worldRotation);
            headJoint.localAnchorA = anchorA;
        }
    }

    public void ChangeDirection(PhysicsTransform reflection, Transform abdomenBone, Transform headBone, Transform grappleArmTransform,
        Vector2 grappleArmBoxOffset, Vector2 grappleArmBoxSize)
    {
        abdomen.transform = abdomen.transform.ReflectAndFlip(reflection, Vector2.zero);
        head.transform = head.transform.ReflectAndFlip(reflection, Vector2.zero);

        abdomen.SyncTransform();
        head.SyncTransform();

        abdomenBone.ReflectAndFlip(abdomen.transform);//grapple arm is childed to abdomenBone
        headBone.ReflectAndFlip(head.transform);

        grappleArm.polygonGeometry = GrappleArmWorldBox(grappleArmTransform, grappleArmBoxOffset, grappleArmBoxSize)
            .InverseTransform(abdomen.transform);
        abdomen.ApplyMassFromShapes();

        ((PhysicsJoint)headJoint).ReflectAndFlipAnchors();

        facingRight = !facingRight;

        var abdomenUserData = abdomen.userData;
        abdomenUserData.boolValue = !facingRight;//bool value will track "reversed" ( = !facingRight)
        abdomen.userData = abdomenUserData;

        var headUserData = head.userData;
        headUserData.boolValue = !facingRight;
        head.userData = headUserData;
    }

    public readonly void ApplyTranslation(Vector2 t)
    {
        abdomen.position += t;
        head.position += t;
    }

    //returns total translation
    public readonly Vector2 ResolveOverlaps()
    {
        var totalTranslation = Vector2.zero;

        var world = abdomen.world;

        var headShape = head.GetShapes()[0];
        var queryFilter = headShape.contactFilter.ToQueryFilter(PhysicsWorld.IgnoreFilter.IgnoreTriggerShapes);
        if (HasOverlap(head.GetShapes()[0], world, queryFilter, out var c))
        {
            totalTranslation += c;
            ApplyTranslation(c);
        }

        var abdomenShapes = abdomen.GetShapes();
        if (HasOverlap(abdomenShapes[0], world, queryFilter, out c))
        {
            totalTranslation += c;
            ApplyTranslation(c);
        }
        if (HasOverlap(abdomenShapes[1], world, queryFilter, out c))
        {
            totalTranslation += c;
            ApplyTranslation(c);
        }


        return totalTranslation;

        static bool HasOverlap(PhysicsShape shape, PhysicsWorld world, PhysicsQuery.QueryFilter filter, out Vector2 correction)
        {
            var worldShape = shape.CreateShapeProxy(true);
            worldShape.radius = 0.2f;
            var overlapResults = world.OverlapShapeProxy(worldShape, filter);
            if (overlapResults.Length > 0)
            {
                var overlappedShape = overlapResults[0].shape;
                var contactManifold = shape.Intersect(shape.transform, overlappedShape, overlappedShape.transform);

                var minSep = 0f;
                for (int i = 0; i < contactManifold.pointCount; i++)
                {
                    if (contactManifold.points[i].separation < minSep)
                    {
                        minSep = contactManifold.points[i].separation;
                    }
                }

                if (minSep < 0)
                {
                    correction = minSep * contactManifold.normal;
                    return true;
                }
            }

            correction = Vector2.zero;
            return false;
        }
    }

    // public readonly bool TestOverlap(float scale)
    // {
    //     var s = Matrix4x4.Scale(new Vector3(scale, scale, scale));
    //     var headShape = head.GetShapes()[0];
    //     var filter = headShape.contactFilter.ToQueryFilter(PhysicsWorld.IgnoreFilter.IgnoreTriggerShapes);
    //     var headCapsule = headShape.capsuleGeometry.Transform(head.transform)
    //         .Transform(s, true);
    //     if (world.TestOverlapGeometry(headCapsule, filter))
    //     {
    //         return true;
    //     }

    //     var abdomenCapsule = this.abdomenCapsule.capsuleGeometry.Transform(abdomen.transform)
    //         .Transform(s, true);
    //     if (world.TestOverlapGeometry(abdomenCapsule, filter))
    //     {
    //         return true;
    //     }

    //     var grappleArm = this.grappleArm.polygonGeometry.Transform(abdomen.transform)
    //         .Transform(s, true);
    //     if (world.TestOverlapGeometry(grappleArm, filter))
    //     {
    //         return true;
    //     }

    //     return false;
    // }

    private readonly PolygonGeometry GrappleArmWorldBox(Transform grappleArmTransform, Vector2 offset, Vector2 size)
    {
        var m = Matrix4x4.TRS((Vector2)grappleArmTransform.position + offset, grappleArmTransform.rotation, size);
        return PolygonGeometry.CreateBox(Vector2.one).Transform(m, true);
    }
}