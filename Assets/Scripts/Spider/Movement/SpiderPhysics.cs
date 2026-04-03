using System;
using UnityEngine;
using Unity.U2D.Physics;
using UnityEditor;
using UnityEngine.UIElements;


[Serializable]
public struct SpiderPhysics
{
    public PhysicsBody abdomen;
    public PhysicsBody head;
    public PhysicsShape grappleArmShape;
    public PhysicsFixedJoint headJoint;
    public PhysicsQuery.QueryFilter queryFilter;
    /// <summary> (when facing right) </summary>
    public PhysicsRotate abdomenRotationFromBase;

    [SerializeField] PhysicsBodyDefinition bodyDef;
    [SerializeField] PhysicsShapeDefinition shapeDef;
    [SerializeField] PhysicsFixedJointDefinition headJointDef;
    [SerializeField] Transform grappleArm;
    [SerializeField] Transform abdomenRoot;
    [SerializeField] Transform abdomenBone;
    [SerializeField] Transform headRoot;
    [SerializeField] Transform headBone;
    [SerializeField] Transform heightReferencePoint;//just for initialization; cache position in local space
    [SerializeField] Vector2 abdomenCapsuleSize;//(width, height) -- full width and height
    [SerializeField] Vector2 abdomenCapsuleOffset;
    [SerializeField] Vector2 headCapsuleSize;
    [SerializeField] Vector2 headCapsuleOffset;

    /// <summary> (when facing right) </summary>
    PhysicsRotate abdomenBaseRotationFromLevel;
    Vector2 heightReferenceLocalPos;
    float totalMass;
    bool facingRight;

    public readonly bool FacingRight => facingRight;
    public readonly int Orientation => FacingRight ? 1 : -1;
    public readonly float TotalMass => totalMass;
    /// <summary> (accurate when facing right; when facing left it is the inverse of this) </summary>
    public readonly PhysicsRotate AbdomenBaseRotationFromLevel => abdomenBaseRotationFromLevel;
    //The heightRefPos and levelRight make up the spider's "true" transform, now that spider is made up of three separate bodies
    public readonly PhysicsRotate LevelRight
    {
        get
        {
            var abdomenAngleFromLevel = abdomenRotationFromBase.MultiplyRotation(abdomenBaseRotationFromLevel);//(when facing right; inverse of this when facing left)
            return FacingRight ? abdomenAngleFromLevel.InverseMultiplyRotation(abdomen.rotation) : abdomenAngleFromLevel.MultiplyRotation(abdomen.rotation);
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

    public void OnValidate()
    {
        if (abdomen.isValid)
        {
            abdomen.SetBodyDefLive(bodyDef);
            abdomen.SetShapeDef(shapeDef);

            head.SetBodyDefLive(bodyDef);
            head.SetShapeDef(shapeDef);

            headJoint.UpdateSettings(headJointDef);

            queryFilter = shapeDef.contactFilter.ToQueryFilter(queryFilter.ignoreFilter);
        }
    }

#if UNITY_EDITOR
    public void CenterRootTransforms()
    {
        CenterRootInCapsule(abdomenRoot, abdomenBone, abdomenCapsuleSize, abdomenCapsuleOffset);
        CenterRootInCapsule(headRoot, headBone, headCapsuleSize, headCapsuleOffset);
    }

    private static void CenterRootInCapsule(Transform root, Transform bone, Vector2 capsuleSize, Vector2 capsuleOffset)
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

    public void DrawGizmos()
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

    public void CreatePhysicsBody(PhysicsRotate levelDirection)
    {
        queryFilter = shapeDef.contactFilter.ToQueryFilter(queryFilter.ignoreFilter);

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

        var grappleArmBox = GrappleArmWorldBox().InverseTransform(abdomen.transform);
        grappleArmShape = abdomen.CreateShape(grappleArmBox, shapeDef);

        //fixed joints: anchorB on bodyB will be pulled towards anchorA on bodyA;
        //for rotation that means bodyB will rotate so that its anchor direction lines up with the anchor direction on bodyA
        headJointDef.bodyA = abdomen;
        headJointDef.bodyB = head;
        headJointDef.localAnchorB = new(head.transform.InverseTransformPoint(headBone.position));
        headJointDef.localAnchorA = abdomen.transform.InverseMultiplyTransform(head.transform.MultiplyTransform(headJointDef.localAnchorB));//the anchors are the same in world space
        headJoint = PhysicsFixedJoint.Create(defaultWorld, headJointDef);

        abdomenBaseRotationFromLevel = levelDirection.InverseMultiplyRotation(abdomen.rotation);
        heightReferenceLocalPos = abdomen.transform.InverseTransformPoint(heightReferencePoint.position);
        abdomen.ApplyMassFromShapes();
        head.ApplyMassFromShapes();
        totalMass = abdomen.mass + head.mass;// + grappleArm.mass;
        facingRight = true;
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

    public void FlipHorizontally(out PhysicsTransform reflection)
    {
        reflection = new PhysicsTransform()
        {
            position = headJoint.bodyA.transform.TransformPoint(headJoint.localAnchorA.position),
            rotation = LevelRight
        };

        abdomen.transform = abdomen.transform.ReflectHorizontally(reflection);
        head.transform = head.transform.ReflectHorizontally(reflection);

        abdomen.SyncTransform();
        head.SyncTransform();

        abdomenBone.ReflectHorizontally(abdomen.transform);//grapple arm is childed to abdomenBone, so that gets covered here
        headBone.ReflectHorizontally(head.transform);

        grappleArmShape.polygonGeometry = GrappleArmWorldBox().InverseTransform(abdomen.transform);
        abdomen.ApplyMassFromShapes();

        ((PhysicsJoint)headJoint).SwitchAnchorSides();

        facingRight = !facingRight;
    }

    private readonly PolygonGeometry GrappleArmWorldBox()
    {
        return PolygonGeometry.CreateBox(Vector2.one).Transform(grappleArm.transform.localToWorldMatrix, true);
    }
}