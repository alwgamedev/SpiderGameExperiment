using System;
using UnityEngine;
using Unity.U2D.Physics;

[Serializable]
public struct JointedChain
{
    public PhysicsBody[] body;
    public PhysicsHingeJoint[] joint;

    [SerializeField] PhysicsHingeJointDefinition[] jointDef;
    [SerializeField] PhysicsShapeDefinition[] shapeDef;
    [SerializeField] PhysicsBodyDefinition[] bodyDef;
    [SerializeField] Transform[] chain;//transforms must not be nested, except for last transform which is the effector and should be childed to chain[^2]
    [SerializeField] float[] width;

    public readonly Vector2 EffectorPosition => chain[^1].position;

    public void OnValidate()
    {
        if (body != null)
        {
            for (int i = 0; i < body.Length; i++)
            {
                body[i].SetBodyDefLive(bodyDef[i]);
                body[i].SetShapeDef(shapeDef[i]);
                if (joint[i].isValid)
                {
                    joint[i].UpdateSettings(jointDef[i]);
                }
            }
        }
    }

    public void DrawGizmos()
    {
        //visualize collider shapes + angle bounds
    }

    public void Initialize(PhysicsBody anchorBody)
    {
        Initialize(anchorBody.world);
        AnchorToBody(anchorBody);
    }

    /// <summary> Does not create a joint 0. </summary>
    public void Initialize(PhysicsWorld world)
    {
        body = new PhysicsBody[chain.Length - 1];
        joint = new PhysicsHingeJoint[chain.Length - 1];

        //create bodies
        for (int i = 0; i < body.Length; i++)
        {
            var bodyDefCopy = bodyDef[i];
            bodyDefCopy.position = chain[i].position;
            bodyDefCopy.rotation = new PhysicsRotate(chain[i].rotation, PhysicsWorld.TransformPlane.XY);
            var chainBody = world.CreateBody(bodyDefCopy);

            Vector2 v = chain[i + 1].position - chain[i].position;
            var l = v.magnitude;
            Vector2 midpoint = 0.5f * (chain[i].position + chain[i + 1].position);
            var boxGeometry = PolygonGeometry.CreateBox(new Vector2(l, width[i]));
            var worldTransform = new PhysicsTransform(midpoint, chainBody.rotation);
            boxGeometry = boxGeometry.Transform(worldTransform).InverseTransform(chainBody.transform);

            chainBody.CreateShape(boxGeometry, shapeDef[i]);
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

            joint[i] = PhysicsHingeJoint.Create(world, jointDefCopy);
        }
    }
    public readonly void PullLink(int i, Vector2 force)
    {
        if (i == body.Length - 1)
        {
            body[i].SyncTransform();
            body[i].ApplyForce(force, chain[i + 1].position);
        }
        else
        {
            body[i].ApplyForce(force, body[i + 1].position);
        }
    }

    public readonly void PullEffector(Vector2 force)
    {
        PullLink(body.Length - 1, force);
    }

    public readonly void PullUniformly(Vector2 force)
    {
        for (int i = 0; i < body.Length; i++)
        {
            PullLink(i, force);
        }
    }

    //we'll see if this causes error when applied to an invalid joint
    //public void ApplyJointSettings(PhysicsHingeJointDefinition[] settings)
    //{
    //    for (int i = 0; i < joint.Length; i++)
    //    {
    //        joint[i].UpdateSettings(settings[i]);
    //    }
    //}

    private void AnchorToBody(PhysicsBody anchor)
    {
        var jointDef = this.jointDef[0];
        jointDef.bodyA = anchor;
        jointDef.bodyB = body[0];
        jointDef.localAnchorA = anchor.transform.InverseMultiplyTransform(body[0].transform);
        jointDef.localAnchorB = PhysicsTransform.identity;
        joint[0] = anchor.world.CreateJoint(jointDef);
    }
}