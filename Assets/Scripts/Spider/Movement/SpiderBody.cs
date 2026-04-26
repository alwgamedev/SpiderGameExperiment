using System;
using UnityEngine;
using Unity.U2D.Physics;
using UnityEditor;


[Serializable]
public struct SpiderBody
{
    public PhysicsBody abdomen;
    public PhysicsBody head;
    public PhysicsShape grappleArmShape;
    public PhysicsFixedJoint headJoint;

    /// <summary> (when facing right) </summary>
    [NonSerialized] public PhysicsRotate abdomenRotationFromBase; 
    PhysicsRotate abdomenBaseRotationFromLevel;

    [SerializeField] Vector2 abdomenCapsuleSize;//(width, height) -- full width and height
    [SerializeField] Vector2 abdomenCapsuleOffset;
    [SerializeField] Vector2 headCapsuleSize;
    [SerializeField] Vector2 headCapsuleOffset;
    /// <summary> (when facing right) </summary>
    Vector2 heightReferenceLocalPos;

    [NonSerialized] public PhysicsQuery.QueryFilter queryFilter;
    float totalMass;

    [SerializeField] PhysicsWorld.IgnoreFilter queryIgnoreFilter;
    bool facingRight;

    [SerializeField] PhysicsShapeDefinition shapeDef;
    [SerializeField] PhysicsFixedJointDefinition headJointDef;
    [SerializeField] PhysicsBodyDefinition bodyDef;

    public readonly bool FacingRight => facingRight;
    public readonly int Orientation => FacingRight ? 1 : -1;
    public readonly float TotalMass => totalMass;
    /// <summary> (accurate when facing right; when facing left it is the inverse of this) </summary>
    public readonly PhysicsRotate AbdomenBaseRotationFromLevel => abdomenBaseRotationFromLevel;
    //The heightRefPos and levelRight make up the spider's "true" transform, now that spider is made up of three separate bodies
    public readonly PhysicsTransform VirtualTransform => new PhysicsTransform(HeightReferencePosition, LevelRight);
    public readonly PhysicsRotate LevelRight
    {
        get
        {
            var abdomenAngleFromLevel = abdomenRotationFromBase.MultiplyRotation(abdomenBaseRotationFromLevel);//(when facing right; inverse of this when facing left)
            return FacingRight? abdomenAngleFromLevel.InverseMultiplyRotation(abdomen.rotation) : abdomenAngleFromLevel.MultiplyRotation(abdomen.rotation);
        }
    }
    public readonly Vector2 HeightReferencePosition
    {
        get
        {
            var localPos = FacingRight ? heightReferenceLocalPos : new(-heightReferenceLocalPos.x, heightReferenceLocalPos.y);
            return abdomen.transform.TransformPoint(localPos);
        }
    }
    public readonly bool HasContact() => abdomen.GetContacts().Length > 0 || head.GetContacts().Length > 0;

    public void OnValidate()
    {
        if (abdomen.isValid)
        {
            abdomen.SetBodyDefLive(bodyDef);
            abdomen.SetShapeDef(shapeDef);

            head.SetBodyDefLive(bodyDef);
            head.SetShapeDef(shapeDef);

            headJoint.UpdateSettings(headJointDef);

            UpdateQueryFilter();
            totalMass = abdomen.mass + head.mass;
        }
    }

#if UNITY_EDITOR
    public void CenterRootTransforms(Transform abdomenRoot, Transform abdomenBone, Transform headRoot, Transform headBone)
    {
        CenterRootInCapsule(abdomenRoot, abdomenBone, abdomenCapsuleSize, abdomenCapsuleOffset);
        CenterRootInCapsule(headRoot, headBone, headCapsuleSize, headCapsuleOffset);
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

    public void DrawGizmos(Transform abdomenBone, Transform headBone)
    {
        if (abdomenBone)
        {
            PhysicsCoreHelper.DrawCapsule(Color.orange, abdomenCapsuleSize, abdomenCapsuleOffset, abdomenBone);
        }

        if (headBone)
        {
            PhysicsCoreHelper.DrawCapsule(Color.orange, headCapsuleSize, headCapsuleOffset, headBone);
        }
    }

    public void CreatePhysicsBody(PhysicsRotate levelDirection, Transform abdomenRoot, Transform headRoot, Transform headBone, 
        Transform grappleArmTransform, Transform heightReferencePoint)
    {
        UpdateQueryFilter();

        var defaultWorld = PhysicsWorld.defaultWorld;

        var bodyDefCopy = bodyDef;
        bodyDefCopy.position = abdomenRoot.position;
        bodyDefCopy.rotation = new PhysicsRotate(abdomenRoot.rotation, PhysicsWorld.TransformPlane.XY);
        abdomen = PhysicsCoreHelper.CreateCapsuleBody(defaultWorld, bodyDefCopy, shapeDef, abdomenCapsuleSize, Vector2.zero, abdomenRoot.localToWorldMatrix);
            //capsule will be centered at root position and won't use offset field, so need to properly position root and bone in editor before play
            //(have written an editor function to do this)
        abdomen.transformObject = abdomenRoot;

        bodyDefCopy.position = headRoot.position;
        bodyDefCopy.rotation = new PhysicsRotate(headRoot.rotation, PhysicsWorld.TransformPlane.XY);
        head = PhysicsCoreHelper.CreateCapsuleBody(defaultWorld, bodyDefCopy, shapeDef, headCapsuleSize, Vector2.zero, headRoot.localToWorldMatrix);
        head.transformObject = headRoot;

        var grappleArmBox = GrappleArmWorldBox(grappleArmTransform).InverseTransform(abdomen.transform);
        grappleArmShape = abdomen.CreateShape(grappleArmBox, shapeDef);

        //fixed joints: anchorB on bodyB will be pulled towards anchorA on bodyA;
        //for rotation that means bodyB will rotate so that its anchor direction lines up with the anchor direction on bodyA
        headJointDef.bodyA = abdomen;
        headJointDef.bodyB = head;
        headJointDef.localAnchorB = new(head.transform.InverseTransformPoint(headBone.position));
        headJointDef.localAnchorA = abdomen.transform.InverseMultiplyTransform(head.transform.MultiplyTransform(headJointDef.localAnchorB));//the anchors are the same in world space
        headJoint = PhysicsFixedJoint.Create(defaultWorld, headJointDef);

        abdomenBaseRotationFromLevel = levelDirection.InverseMultiplyRotation(abdomen.rotation);
        abdomenRotationFromBase = PhysicsRotate.identity;
        heightReferenceLocalPos = abdomen.transform.InverseTransformPoint(heightReferencePoint.position);

        totalMass = abdomen.mass + head.mass;
        facingRight = true;
    }

    public void Enable()
    {
        head.enabled = true;
        abdomen.enabled = true;
    }

    public void Disable()
    {
        head.enabled = false;
        abdomen.enabled = false;
    }

    public void Destroy()
    {
        abdomen.Destroy();
        head.Destroy();
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

    public void ChangeDirection(PhysicsTransform reflection, Transform abdomenBone, Transform headBone, Transform grappleArmTransform)
    {
        abdomen.transform = abdomen.transform.ReflectAndFlip(reflection, Vector2.zero);
        head.transform = head.transform.ReflectAndFlip(reflection, Vector2.zero);

        abdomen.SyncTransform();
        head.SyncTransform();

        abdomenBone.ReflectAndFlip(abdomen.transform);//grapple arm is childed to abdomenBone
        headBone.ReflectAndFlip(head.transform);

        grappleArmShape.polygonGeometry = GrappleArmWorldBox(grappleArmTransform).InverseTransform(abdomen.transform);
        abdomen.ApplyMassFromShapes();

        ((PhysicsJoint)headJoint).ReflectAndFlipAnchors();

        facingRight = !facingRight;
    }

    public void ApplyTranslation(Vector2 t)
    {
        abdomen.position += t;
        head.position += t;
    }

    //returns total translation
    public Vector2 ResolveOverlaps()
    {
        var totalTranslation = Vector2.zero;

        var world = abdomen.world;

        if (HasOverlap(head.GetShapes()[0], world, queryFilter, out var c))
        {
            totalTranslation += c;
            ApplyTranslation(c);
        }
        if (HasOverlap(abdomen.GetShapes()[0], world, queryFilter, out c))
        {
            totalTranslation += c;
            ApplyTranslation(c);
        }
        if (HasOverlap(abdomen.GetShapes()[1], world, queryFilter, out c))
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
            if (overlapResults.Length > 0 )
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

    private void UpdateQueryFilter()
    {
        queryFilter = shapeDef.contactFilter.ToQueryFilter(queryIgnoreFilter);
    }

    private readonly PolygonGeometry GrappleArmWorldBox(Transform grappleArmTransform)
    {
        return PolygonGeometry.CreateBox(Vector2.one).Transform(grappleArmTransform.localToWorldMatrix, true);
    }
}