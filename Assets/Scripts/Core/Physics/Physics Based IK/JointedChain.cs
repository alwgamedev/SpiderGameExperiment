using System;
using UnityEngine;
using Unity.U2D.Physics;

[Serializable]
public struct JointedChain
{
    public PhysicsBody[] body;
    public PhysicsFixedJoint[] joint;

    [SerializeField] PhysicsFixedJointDefinition[] jointDef;
    [SerializeField] Transform[] chain;//transforms must not be nested; chain[^1] is the "effector" (no body)
    [SerializeField] float[] width;
    [SerializeField] PhysicsBodyDefinition bodyDef;
    [SerializeField] PhysicsShapeDefinition shapeDef;

    public void OnValidate()
    {
        if (joint != null)
        {
            ApplyJointSettings(jointDef);
        }
    }

    public void DrawGizmos()
    {
        //visualize collider shapes + angle bounds
    }

    /// <summary> Use if you want the chain to be anchored to another body. </summary>
    public void CreateChain(PhysicsBody anchor)
    {
        CreateChain(anchor.world);

        var jointDef = this.jointDef[0];
        jointDef.bodyA = anchor;
        jointDef.bodyB = body[0];
        jointDef.localAnchorA = anchor.transform.InverseMultiplyTransform(body[0].transform);
        jointDef.localAnchorB = PhysicsTransform.identity;
    }

    /// <summary> Use when chain does not need to be anchored to another body (does not create a joint 0). </summary>
    public void CreateChain(PhysicsWorld world)
    {
        body = new PhysicsBody[chain.Length - 1];
        joint = new PhysicsFixedJoint[chain.Length - 1];

        //create bodies
        var bodyDefCopy = bodyDef;
        for (int i = 0; i < body.Length; i++)
        {
            bodyDefCopy.position = chain[i].position;
            bodyDefCopy.rotation = new PhysicsRotate(chain[i].rotation, PhysicsWorld.TransformPlane.XY);
            var chainBody = world.CreateBody(bodyDef);

            Vector2 v = chain[i + 1].position - chain[i].position;
            var l = v.magnitude;
            Vector2 midpoint = 0.5f * (chain[i].position + chain[i + 1].position);
            var boxGeometry = PolygonGeometry.CreateBox(new(width[i], l));
            var worldTransform = new PhysicsTransform(midpoint, chainBody.rotation);
            boxGeometry = boxGeometry.Transform(worldTransform).InverseTransform(chainBody.transform);

            chainBody.CreateShape(boxGeometry, shapeDef);
            chainBody.transformObject = chain[i];
            body[i] = chainBody;
        }

        for (int i = 1; i < joint.Length; i++)
        {
            var jointDefCopy = jointDef[i];
            jointDefCopy.bodyA = body[i - 1];
            jointDefCopy.bodyB = body[i];
            jointDefCopy.localAnchorA = body[i - 1].transform.InverseMultiplyTransform(body[i].transform);
            jointDefCopy.localAnchorB = PhysicsTransform.identity;

            var chainJoint = world.CreateJoint(jointDef[i]);
        }
    }

    //we'll see if this causes error when applied to an invalid joint
    public void ApplyJointSettings(PhysicsFixedJointDefinition[] settings)
    {
        for (int i = 0; i < joint.Length; i++)
        {
            joint[i].UpdateSettings(settings[i]);
        }
    }

    public void PullLink(int i, Vector2 force)
    {
        body[i].ApplyForce(force, chain[i + 1].position);
    }
}