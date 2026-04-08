using System;
using Unity.U2D.Physics;
using Unity.VisualScripting.YamlDotNet.Core.Tokens;
using UnityEditor;
using UnityEngine;

[Serializable] 
public struct JointedChainDefinition
{
    public PhysicsHingeJointDefinition jointDef;
    public PhysicsShapeDefinition shapeDef;
    public PhysicsBodyDefinition bodyDef;
    public float[] width;
}

[Serializable]
public struct JointedChainSettings
{
    public float[] lowerAngleLimit;
    public float[] upperAngleLimit;
    public bool[] enableLimit;
}

[Serializable]
public struct JointedChain
{
    public PhysicsBody[] body;
    public PhysicsHingeJoint[] joint;

    float effectorDistance;
    float mass;

    public readonly int JointCount => joint.Length;
    public readonly float Mass => mass;
    public readonly Vector2 EffectorPosition => body[^1].position + effectorDistance * body[^1].rotation.direction;
    public readonly Vector2 NextPosition(int i) => i == body.Length - 1 ? EffectorPosition : body[i + 1].position;
    public PhysicsBody AnchorBody => joint[0].bodyA;

    public static void DrawGizmos(Transform[] transform, float[] width)
    {
        if (transform != null && width != null && width.Length == transform.Length - 1)
        {
            using (new Handles.DrawingScope(Color.orange))
            {
                for (int i = 0; i < transform.Length - 1; i++)
                {
                    if (transform[i] && transform[i + 1])
                    {
                        Vector2 center = 0.5f * (transform[i].position + transform[i + 1].position);
                        Vector2 l = center - (Vector2)transform[i].position;
                        var w = 0.5f * width[i] * l.normalized.CCWPerp();

                        Handles.DrawLine(center - l + w, center + l + w);
                        Handles.DrawLine(center - l - w, center + l - w);
                        Handles.DrawLine(center - l - w, center - l + w);
                        Handles.DrawLine(center + l - w, center + l + w);
                    }
                }
            }
        }
    }

    public void UpdateDefAndSettings(JointedChainDefinition def, JointedChainSettings settings)
    {
        for (int i = 0; i < body.Length; i++)
        {
            if (body[i].isValid)
            {
                body[i].SetBodyDefLive(def.bodyDef);
                body[i].SetShapeDef(def.shapeDef);
            }

            if (joint[i].isValid)
            {
                var jointDef = def.jointDef;
                jointDef.enableLimit = settings.enableLimit[i];
                jointDef.lowerAngleLimit = settings.lowerAngleLimit[i];
                jointDef.upperAngleLimit = settings.upperAngleLimit[i];
                joint[i].UpdateSettings(jointDef);
            }
        }
    }

    public void UpdateSettings(JointedChainSettings settings)
    {
        for (int i = 0; i < body.Length; i++)
        {
            if (joint[i].isValid)
            {
                joint[i].enableLimit = settings.enableLimit[i];
                joint[i].lowerAngleLimit = settings.lowerAngleLimit[i];
                joint[i].upperAngleLimit = settings.upperAngleLimit[i];
            }
        }
    }

    /// <summary> Use if you want the base of the chain to be anchored to another body. </summary>
    public void Initialize(Transform[] transform, PhysicsBody anchorBody, JointedChainDefinition def, JointedChainSettings settings)
    {
        Initialize(transform, anchorBody.world, def, settings);

        var jointDef = def.jointDef;
        jointDef.enableLimit = settings.enableLimit[0];
        jointDef.lowerAngleLimit = settings.lowerAngleLimit[0];
        jointDef.upperAngleLimit = settings.upperAngleLimit[0];
        jointDef.bodyA = anchorBody;
        jointDef.bodyB = body[0];
        jointDef.localAnchorA = anchorBody.transform.InverseMultiplyTransform(body[0].transform);
        jointDef.localAnchorB = PhysicsTransform.identity;
        joint[0] = PhysicsHingeJoint.Create(anchorBody.world, jointDef);
    }

    /// <summary> Does not create a joint 0. </summary>
    public void Initialize(Transform[] transform, PhysicsWorld world, JointedChainDefinition def, JointedChainSettings settings)
    {
        body = new PhysicsBody[transform.Length - 1];
        joint = new PhysicsHingeJoint[transform.Length - 1];
        effectorDistance = Vector2.Distance(transform[^2].position, transform[^1].position);

        //create bodies
        var bodyDefCopy = def.bodyDef;
        for (int i = 0; i < body.Length; i++)
        {
            bodyDefCopy.position = transform[i].position;
            bodyDefCopy.rotation = new PhysicsRotate(transform[i].rotation, PhysicsWorld.TransformPlane.XY);
            var chainBody = world.CreateBody(bodyDefCopy);

            Vector2 v = transform[i + 1].position - transform[i].position;
            var l = v.magnitude;
            Vector2 midpoint = 0.5f * (transform[i].position + transform[i + 1].position);
            var boxGeometry = PolygonGeometry.CreateBox(new Vector2(l, def.width[i]));
            var worldTransform = new PhysicsTransform(midpoint, chainBody.rotation);
            boxGeometry = boxGeometry.Transform(worldTransform).InverseTransform(chainBody.transform);

            chainBody.CreateShape(boxGeometry, def.shapeDef);
            chainBody.transformObject = transform[i];
            body[i] = chainBody;
        }

        var jointDefCopy = def.jointDef;
        for (int i = 1; i < joint.Length; i++)
        {
            jointDefCopy.enableLimit = settings.enableLimit[i];
            jointDefCopy.lowerAngleLimit = settings.lowerAngleLimit[i];
            jointDefCopy.upperAngleLimit = settings.upperAngleLimit[i];
            jointDefCopy.bodyA = body[i - 1];
            jointDefCopy.bodyB = body[i];
            jointDefCopy.localAnchorA = body[i - 1].transform.InverseMultiplyTransform(body[i].transform);
            jointDefCopy.localAnchorB = PhysicsTransform.identity;

            joint[i] = PhysicsHingeJoint.Create(world, jointDefCopy);
        }

        RecomputeMass();
    }

    public readonly void PushLink(int i, Vector2 accel)
    {
        body[i].ApplyForce(body[i].mass * accel, body[i].position);
    }

    public readonly void PushUniformly(Vector2 accel)
    {
        for (int i = 0; i < body.Length; i++)
        {
            PushLink(i, accel);
        }
    }

    public readonly void AccelerateLink(int i, Vector2 accel)
    {
        body[i].ApplyForceToCenter(body[i].mass * accel);
    }

    public readonly void AccelerateUniformly(Vector2 accel)
    {
        for (int i = 0; i < body.Length; i++)
        {
            AccelerateLink(i, accel);
        }
    }

    public readonly void PullLink(int i, Vector2 accel)
    {
        body[i].ApplyForce(body[i].mass * accel, NextPosition(i));
    }

    public readonly void PullUniformly(Vector2 accel)
    {
        for (int i = 0; i < body.Length; i++)
        {
            PullLink(i, accel);
        }
    }

    private void RecomputeMass()
    {
        mass = 0;
        for (int i = 0; i < body.Length; i++)
        {
            mass += body[i].mass;
        }
    }
}