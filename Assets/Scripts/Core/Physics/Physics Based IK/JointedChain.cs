using System;
using Unity.U2D.Physics;
using UnityEditor;
using UnityEngine;
using Unity.Mathematics;

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
    public bool reversed;

    float effectorDistance;
    float mass;

    public readonly int JointCount => joint.Length;
    public readonly float Mass => mass;
    public readonly Vector2 EffectorPosition => 
        JointPosition(JointCount - 1) + math.select(effectorDistance, -effectorDistance, reversed) * body[^1].rotation.direction;
    public PhysicsBody AnchorBody => joint[0].bodyA;
    public readonly Vector2 JointPosition(int i) => body[i].transform.TransformPoint(joint[i].localAnchorB.position);
    public readonly Vector2 NextPosition(int i) => i == JointCount - 1 ? EffectorPosition : JointPosition(i + 1);
    public readonly float MassTail(int i)
    {
        var total = 0f;
        for (int j = i; j < body.Length; j++)
        {
            total += body[j].mass;
        }
        return total;
    }

    public static void DrawBodyGizmos(Transform[] transform, float[] width)
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

    public static void DrawAngleGizmos(Transform[] transform, JointedChainSettings settings)
    {
        if (transform != null && settings.lowerAngleLimit != null && settings.upperAngleLimit != null
            && settings.lowerAngleLimit.Length == transform.Length - 1 && settings.upperAngleLimit.Length == transform.Length - 1)
        {
            using (new Handles.DrawingScope(Color.red))
            {
                for (int i = 0; i < transform.Length - 1; i++)
                {
                    var p = transform[i].position;
                    var r = Vector2.Distance(transform[i].position, transform[i + 1].position);
                    var qMin = Quaternion.Euler(0, 0, settings.lowerAngleLimit[i]) * transform[i].rotation;
                    var qMax = Quaternion.Euler(0, 0, settings.upperAngleLimit[i]) * transform[i].rotation;
                    var uMin = qMin * Vector2.right;
                    var uMax = qMax * Vector2.right;
                    Handles.DrawLine(p, p + r * uMin);
                    Handles.DrawLine(p, p + r * uMax);
                    Handles.DrawWireArc(p, Vector3.forward, uMin, settings.upperAngleLimit[i] - settings.lowerAngleLimit[i], r);
                }
            }
        }
    }

    public void DrawAngleGizmos()
    {
        if (joint != null)
        {
            using (new Handles.DrawingScope(Color.red))
            {
                for (int i = 0; i < joint.Length; i++)
                {
                    ref var j = ref joint[i];

                    if (j.isValid)
                    {
                        var p = JointPosition(i);
                        var r = Vector2.Distance(p, NextPosition(i));
                        if (reversed)
                        {
                            r = -r;
                        }
                        var rot0 = j.bodyA.rotation.MultiplyRotation(j.localAnchorA.rotation);
                        var rotMin = rot0.MultiplyRotation(PhysicsRotate.FromDegrees(j.lowerAngleLimit));
                        var rotMax = rot0.MultiplyRotation(PhysicsRotate.FromDegrees(j.upperAngleLimit));
                        Handles.DrawLine(p, p + r * rotMin.direction);
                        Handles.DrawLine(p, p + r * rotMax.direction);
                        Handles.DrawWireArc(p, Vector3.forward, rotMin.direction, j.upperAngleLimit - j.lowerAngleLimit, r);
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

        RecomputeMass();
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

    /// <summary> Use if you want the base of the chain to be anchored to another body. 
    /// Bone array should list the joint positions and effector position, and
    /// physTransforms are expected to be centered between the bone positions.</summary>
    public void Initialize(Transform[] physTransform, Transform[] bone, PhysicsBody anchorBody, JointedChainDefinition def, JointedChainSettings settings)
    {
        Initialize(physTransform, bone, anchorBody.world, def, settings);

        var jointDef = def.jointDef;
        jointDef.enableLimit = settings.enableLimit[0];
        jointDef.lowerAngleLimit = settings.lowerAngleLimit[0];
        jointDef.upperAngleLimit = settings.upperAngleLimit[0];
        jointDef.bodyA = anchorBody;
        jointDef.bodyB = body[0];
        var worldAnchor = new PhysicsTransform(bone[0].position, body[0].rotation);
        jointDef.localAnchorA = anchorBody.transform.InverseMultiplyTransform(worldAnchor);
        jointDef.localAnchorB = body[0].transform.InverseMultiplyTransform(worldAnchor);
        joint[0] = PhysicsHingeJoint.Create(anchorBody.world, jointDef);

        reversed = false;
    }

    /// <summary> Does not create a joint 0. 
    /// Bone array should list the joint positions and effector position, and
    /// physTransforms are expected to be centered between the bone positions.</summary>
    public void Initialize(Transform[] physTransform, Transform[] bone, PhysicsWorld world, JointedChainDefinition def, JointedChainSettings settings)
    {
        body = new PhysicsBody[bone.Length - 1];
        joint = new PhysicsHingeJoint[bone.Length - 1];
        effectorDistance = Vector2.Distance(bone[^2].position, bone[^1].position);

        //create bodies
        var bodyDefCopy = def.bodyDef;
        for (int i = 0; i < body.Length; i++)
        {
            bodyDefCopy.position = physTransform[i].position;
            bodyDefCopy.rotation = new PhysicsRotate(physTransform[i].rotation, PhysicsWorld.TransformPlane.XY);
            var chainBody = world.CreateBody(bodyDefCopy);

            Vector2 v = 2 * (physTransform[i].position - bone[i].position);
            var l = v.magnitude;
            var boxGeometry = PolygonGeometry.CreateBox(new Vector2(l, def.width[i]));

            chainBody.CreateShape(boxGeometry, def.shapeDef);
            chainBody.transformObject = physTransform[i];
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
            var worldAnchor = new PhysicsTransform(bone[i].position, body[i].rotation);
            jointDefCopy.localAnchorA = body[i - 1].transform.InverseMultiplyTransform(worldAnchor);
            jointDefCopy.localAnchorB = body[i].transform.InverseMultiplyTransform(worldAnchor);

            joint[i] = PhysicsHingeJoint.Create(world, jointDefCopy);
        }

        RecomputeMass();
    }

    public void FlipAngleLimits(int i)
    {
        var temp = joint[i].upperAngleLimit;
        joint[i].upperAngleLimit = -joint[i].lowerAngleLimit;
        joint[i].lowerAngleLimit = -temp;
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