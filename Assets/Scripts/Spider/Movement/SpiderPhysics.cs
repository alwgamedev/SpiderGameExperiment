using System;
using UnityEngine;
using Unity.U2D.Physics;
using UnityEditor;


[Serializable]
public struct SpiderPhysics
{
    public PhysicsBody abdomen;
    public PhysicsBody head;
    public PhysicsBody grappleArm;
    public PhysicsFixedJoint headJoint;
    public PhysicsFixedJoint grappleArmJoint;
    public PhysicsQuery.QueryFilter queryFilter;
    public PhysicsRotate abdomenAngle;//angle from "level" (usually ground direction) when facing right

    [SerializeField] PhysicsBodyDefinition bodyDef;
    [SerializeField] PhysicsShapeDefinition shapeDef;
    [SerializeField] PhysicsFixedJointDefinition headJointDef;
    [SerializeField] PhysicsFixedJointDefinition grappleArmJointDef;
    //[SerializeField] Transform spiderTransform;
    [SerializeField] Transform grappleArmRoot;
    [SerializeField] Transform grappleArmBone;
    [SerializeField] Transform abdomenRoot;
    [SerializeField] Transform abdomenBone;
    [SerializeField] Transform headRoot;
    [SerializeField] Transform headBone;
    [SerializeField] Transform heightReferencePoint;//just for initialization; cache position in local space
    [SerializeField] Vector2 abdomenCapsuleSize;//(width, height) -- full width and height
    [SerializeField] Vector2 abdomenCapsuleOffset;
    [SerializeField] Vector2 headCapsuleSize;
    [SerializeField] Vector2 headCapsuleOffset;

    Vector2 heightReferenceLocalPos;

    bool facingRight;

    public readonly int Orientation => FacingRight ? 1 : -1;
    public readonly bool FacingRight => facingRight;
    public PhysicsRotate LevelRight => FacingRight ? abdomen.rotation.MultiplyRotation(abdomenAngle.Inverse()): abdomen.rotation.MultiplyRotation(abdomenAngle);
    public Vector2 HeightReferencePosition => head.transform.TransformPoint(heightReferenceLocalPos);

    public void OnValidate()
    {
        if (abdomen.isValid)
        {
            abdomen.SetBodyDefLive(bodyDef);
            abdomen.SetShapeDef(shapeDef);

            head.SetBodyDefLive(bodyDef);
            head.SetShapeDef(shapeDef);
            
            grappleArm.SetBodyDefLive(bodyDef);
            grappleArm.SetShapeDef(shapeDef);

            headJoint.UpdateSettings(headJointDef);
            grappleArmJoint.UpdateSettings(grappleArmJointDef);

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

        //var com = abdomen.transform.InverseTransformPoint(heightReferencePoint.position);
        //var mass = abdomen.massConfiguration;
        //mass.center = com;
        //abdomen.massConfiguration = mass;

        bodyDefCopy.position = headRoot.position;
        bodyDefCopy.rotation = new PhysicsRotate(headRoot.rotation, PhysicsWorld.TransformPlane.XY);
        head = PhysicsCoreHelper.CreateCapsuleBody(defaultWorld, bodyDefCopy, shapeDef, headCapsuleSize, Vector2.zero, headRoot.localToWorldMatrix);
        head.transformObject = headRoot;

        bodyDefCopy.position = grappleArmRoot.position;
        bodyDefCopy.rotation = new PhysicsRotate(grappleArmRoot.rotation, PhysicsWorld.TransformPlane.XY);
        grappleArm = PhysicsCoreHelper.CreateBoxBody(defaultWorld, bodyDefCopy, shapeDef, Vector2.one, grappleArmBone.localToWorldMatrix);
        grappleArm.transformObject = grappleArmRoot;

        //local anchors: localAnchorB on bodyB will hook up to localAnchorA on bodyA,
        //i.e. the anchor position on bodyB will be pulled towards the anchor position on bodyA,
        //and bodyB will rotate so that its anchor direction lines up with the anchor direction on bodyA
        headJointDef.bodyA = abdomen;
        headJointDef.bodyB = head;
        headJointDef.localAnchorB = new(head.transform.InverseTransformPoint(headBone.position));
        headJointDef.localAnchorA = abdomen.transform.InverseMultiplyTransform(head.transform.MultiplyTransform(headJointDef.localAnchorB));//the anchors are the same in world space
        headJoint = PhysicsFixedJoint.Create(defaultWorld, headJointDef);

        grappleArmJointDef.bodyA = abdomen;
        grappleArmJointDef.bodyB = grappleArm;
        grappleArmJointDef.localAnchorA = abdomen.transform.InverseMultiplyTransform(grappleArm.transform);
        grappleArmJointDef.localAnchorB = PhysicsTransform.identity;
        grappleArmJoint = PhysicsFixedJoint.Create(defaultWorld, grappleArmJointDef);

        abdomenAngle = abdomen.rotation.MultiplyRotation(levelDirection.Inverse());
        heightReferenceLocalPos = head.transform.InverseTransformPoint(heightReferencePoint.position);
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

    public void FlipHorizontally()
    {
        var reflection = new PhysicsTransform()
        {
            position = headJoint.bodyA.transform.TransformPoint(headJoint.localAnchorA.position),
            rotation = LevelRight
        };

        abdomen.transform = abdomen.transform.ReflectHorizontally(reflection);
        head.transform = head.transform.ReflectHorizontally(reflection);
        grappleArm.transform = grappleArm.transform.ReflectHorizontally(reflection);

        abdomen.SyncTransform();
        head.SyncTransform();
        grappleArm.SyncTransform();

        abdomenBone.ReflectHorizontally(abdomen.transform);
        headBone.ReflectHorizontally(head.transform);
        grappleArmBone.ReflectHorizontally(grappleArm.transform);

        ((PhysicsJoint)headJoint).SwitchAnchorSides();
        ((PhysicsJoint)grappleArmJoint).SwitchAnchorSides();

        facingRight = !facingRight;
    }
}