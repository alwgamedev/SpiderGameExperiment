using System;
using UnityEngine;
using Unity.U2D.Physics;


[Serializable]
public struct SpiderPhysics
{
    public PhysicsBody abdomen;
    public PhysicsBody head;
    public PhysicsBody grappleArm;
    public PhysicsFixedJoint headJoint;
    public PhysicsFixedJoint grappleArmJoint;
    public PhysicsQuery.QueryFilter queryFilter;

    [SerializeField] PhysicsBodyDefinition bodyDef;
    [SerializeField] PhysicsShapeDefinition shapeDef;
    [SerializeField] PhysicsFixedJointDefinition headJointDef;
    [SerializeField] PhysicsFixedJointDefinition grappleArmJointDef;
    [SerializeField] Transform spiderTransform;
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

    public void CreatePhysicsBody()
    {
        if (abdomen.isValid)
        {
            abdomen.Destroy();
            headJoint.Destroy();
            grappleArmJoint.Destroy();
            head.Destroy();
            grappleArm.Destroy();
        }

        queryFilter = shapeDef.contactFilter.ToQueryFilter(queryFilter.ignoreFilter);

        var defaultWorld = PhysicsWorld.defaultWorld;

        var bodyDefCopy = bodyDef;
        bodyDefCopy.position = abdomenRoot.position;
        bodyDefCopy.rotation = new PhysicsRotate(abdomenRoot.rotation, PhysicsWorld.TransformPlane.XY);
        abdomen = PhysicsCoreHelper.CreateCapsuleBody(defaultWorld, bodyDefCopy, shapeDef, abdomenCapsuleSize, abdomenCapsuleOffset, abdomenBone.localToWorldMatrix);
        abdomen.transformObject = abdomenRoot;

        var com = abdomen.transform.InverseTransformPoint(heightReferencePoint.position);
        var mass = abdomen.massConfiguration;
        mass.center = com;
        abdomen.massConfiguration = mass;

        bodyDefCopy.position = headRoot.position;
        bodyDefCopy.rotation = new PhysicsRotate(headRoot.rotation, PhysicsWorld.TransformPlane.XY);
        head = PhysicsCoreHelper.CreateCapsuleBody(defaultWorld, bodyDefCopy, shapeDef, headCapsuleSize, headCapsuleOffset, headBone.localToWorldMatrix);
        head.transformObject = headRoot;

        bodyDefCopy.position = grappleArmRoot.position;
        bodyDefCopy.rotation = new PhysicsRotate(grappleArmRoot.rotation, PhysicsWorld.TransformPlane.XY);
        grappleArm = PhysicsCoreHelper.CreateBoxBody(defaultWorld, bodyDefCopy, shapeDef, Vector2.one, grappleArmBone.localToWorldMatrix);
        grappleArm.transformObject = grappleArmRoot;

        headJointDef.bodyA = abdomen;
        headJointDef.bodyB = head;
        headJointDef.localAnchorA = new(abdomen.transform.InverseTransformPoint(head.position));
        headJointDef.localAnchorB = PhysicsTransform.identity;
        headJoint = PhysicsFixedJoint.Create(defaultWorld, headJointDef);

        grappleArmJointDef.bodyA = abdomen;
        grappleArmJointDef.bodyB = grappleArm;
        grappleArmJointDef.localAnchorA = new(abdomen.transform.InverseTransformPoint(grappleArm.position));
        grappleArmJointDef.localAnchorB = PhysicsTransform.identity;
        grappleArmJoint = PhysicsFixedJoint.Create(defaultWorld, grappleArmJointDef);
    }
}