using System;
using Unity.U2D.Physics;
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

public struct JointedChain
{
    public PhysicsBody[] body;
    public PhysicsHingeJoint[] joint;

    PhysicsWorld world;
    float effectorDistance;
    float mass;
    
    //public bool reversed;

    public readonly int JointCount => joint.Length;
    public readonly float Mass => mass;
    public readonly float LastArmLength => effectorDistance;
    public readonly PhysicsBody AnchorBody => joint[0].bodyA;
    public readonly PhysicsWorld World => world;

    public readonly Vector2 EffectorPosition(bool reversed) =>
        JointPosition(JointCount - 1) + (reversed ? -effectorDistance : effectorDistance) * body[^1].rotation.direction;

    public readonly bool Enabled()
    {
        for (int i = 0; i < body.Length; i++)
        {
            if (!body[i].enabled)
            {
                return false;
            }
        }

        return true;
    }

    public readonly Vector2 JointPosition(int i) => joint[i].bodyA.transform.TransformPoint(joint[i].localAnchorA.position);//body[i].transform.TransformPoint(joint[i].localAnchorB.position);

    public readonly Vector2 NextPosition(int i, bool reversed) => i == JointCount - 1 ? EffectorPosition(reversed): JointPosition(i + 1);

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

    public readonly void DrawAngleGizmos(bool reversed)
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
                        var r = Vector2.Distance(p, NextPosition(i, reversed));
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

    public static void CenterPhysicsTransforms(Transform[] physicsTransform, Transform[] bone)
    {
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Center Leg Bodies");
        int groupId = Undo.GetCurrentGroup();

        for (int i = 0; i < bone.Length; i++)
        {
            for (int j = 0; j < bone.Length - 1; j++)
            {
                var p0 = bone[j].position;

                Undo.RecordObject(physicsTransform[j], "Set phys body position");
                Undo.RecordObject(bone[j], "Set bone position");
                physicsTransform[j].position = 0.5f * (bone[j].position + bone[j + 1].position);
                bone[j].position = p0;

                PrefabUtility.RecordPrefabInstancePropertyModifications(physicsTransform[j]);
                PrefabUtility.RecordPrefabInstancePropertyModifications(bone[j]);
            }
        }

        Undo.CollapseUndoOperations(groupId);
    }

    public void UpdateDefAndSettings(JointedChainDefinition def, JointedChainSettings settings, 
        bool keepEnableSpring, bool keepSpringTargetAngle)
    {
        if (body != null)
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
                    joint[i].UpdateSettings(jointDef, keepEnableSpring, keepSpringTargetAngle);
                }
            }

            RecomputeMass();
        }
    }

    public readonly void UpdateSettings(JointedChainSettings settings)
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
    public void Initialize(Transform[] physTransform, Transform[] bone, PhysicsBody anchorBody, JointedChainDefinition def, JointedChainSettings settings,
        bool alignArmRotations)
    {
        Initialize(physTransform, bone, anchorBody.world, def, settings, alignArmRotations);

        var jointDef = def.jointDef;
        if (settings.upperAngleLimit != null)
        {
            jointDef.enableLimit = settings.enableLimit[0];
            jointDef.lowerAngleLimit = settings.lowerAngleLimit[0];
            jointDef.upperAngleLimit = settings.upperAngleLimit[0];
        }
        jointDef.bodyA = anchorBody;
        jointDef.bodyB = body[0];
        if (alignArmRotations)
        {
            var worldPos = bone[0].position;
            var posA = anchorBody.transform.InverseTransformPoint(worldPos);
            var posB = body[0].transform.InverseTransformPoint(worldPos);
            var anchorA = new PhysicsTransform(posA, PhysicsRotate.identity);
            var anchorB = new PhysicsTransform(posB, PhysicsRotate.identity);
            jointDef.localAnchorA = anchorA;
            jointDef.localAnchorB = anchorB;
        }
        else
        {
            var worldAnchor = new PhysicsTransform(bone[0].position, body[0].rotation);
            jointDef.localAnchorA = anchorBody.transform.InverseMultiplyTransform(worldAnchor);
            jointDef.localAnchorB = body[0].transform.InverseMultiplyTransform(worldAnchor);
        }

        joint[0] = PhysicsHingeJoint.Create(anchorBody.world, jointDef);
    }

    /// <summary> Does not create a joint 0. 
    /// Bone array should list the joint positions and effector position, and
    /// physTransforms are expected to be centered between the bone positions.</summary>
    public void Initialize(Transform[] physTransform, Transform[] bone, PhysicsWorld world, JointedChainDefinition def, JointedChainSettings settings,
        bool alignArmRotations)
    {
        this.world = world;
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
            if (settings.upperAngleLimit != null)
            {
                jointDefCopy.enableLimit = settings.enableLimit[i];
                jointDefCopy.lowerAngleLimit = settings.lowerAngleLimit[i];
                jointDefCopy.upperAngleLimit = settings.upperAngleLimit[i];
            }
            jointDefCopy.bodyA = body[i - 1];
            jointDefCopy.bodyB = body[i];
            if (alignArmRotations)
            {
                var worldPos = bone[i].position;
                var posA = body[i - 1].transform.InverseTransformPoint(worldPos);
                var posB = body[i].transform.InverseTransformPoint(worldPos);
                var anchorA = new PhysicsTransform(posA, PhysicsRotate.identity);
                var anchorB = new PhysicsTransform(posB, PhysicsRotate.identity);
                jointDefCopy.localAnchorA = anchorA;
                jointDefCopy.localAnchorB = anchorB;
            }
            else
            {
                var worldAnchor = new PhysicsTransform(bone[i].position, body[i].rotation);
                jointDefCopy.localAnchorA = body[i - 1].transform.InverseMultiplyTransform(worldAnchor);
                jointDefCopy.localAnchorB = body[i].transform.InverseMultiplyTransform(worldAnchor);
            }

            joint[i] = PhysicsHingeJoint.Create(world, jointDefCopy);
        }

        RecomputeMass();
    }

    public readonly void Enable()
    {
        if (body != null)
        {
            for (int i = 0; i < body.Length; i++)
            {
                if (body[i].isValid)
                {
                    body[i].enabled = true;
                }
            }
        }
    }

    public readonly void Disable(bool forgetState)
    {
        if (body != null)
        {
            for (int i = 0; i < body.Length; i++)
            {
                if (body[i].isValid)
                {
                    if (forgetState)
                    {
                        body[i].linearVelocity = Vector2.zero;
                        body[i].angularVelocity = 0;
                    }
                    body[i].enabled = false;
                }
            }
        }
    }

    public readonly void Destroy()
    {
        if (body != null)
        {
            for (int i = 0; i < body.Length; i++)
            {
                if (body[i].isValid)
                {
                    body[i].Destroy();
                }
            }
        }
    }

    public readonly void OnDirectionChanged(PhysicsTransform reflection, Vector2 postTranslation, Transform[] bone)
    {
        for (int i = 0; i < body.Length; i++)
        {
            body[i].transform = body[i].transform.ReflectAndFlip(reflection, Vector2.zero);
            body[i].linearVelocity = body[i].linearVelocity.ReflectAcrossHyperplane(reflection.rotation.direction);
            body[i].angularVelocity = -body[i].angularVelocity;
            body[i].SyncTransform();
            bone[i].ReflectAndFlip(body[i].transform);
            ((PhysicsJoint)joint[i]).ReflectAndFlipAnchors();
        }

        if (postTranslation != Vector2.zero)
        {
            for (int i = 0; i < body.Length; i++)
            {
                body[i].position += postTranslation;
                body[i].SyncTransform();
            }
        }

        FlipAngleLimits();
        FlipSpringTargets();
    }

    public readonly void SyncTransforms()
    {
        for (int i = 0; i < body.Length; i++)
        {
            body[i].SyncTransform();
        }
    }

    public readonly void ApplyTranslation(Vector2 t)
    {
        for (int i = 0; i < body.Length; i++)
        {
            body[i].position += t;
        }
    }

    public readonly void FlipAngleLimits()
    {
        if (joint != null)
        {
            for (int i = 0; i < JointCount; i++)
            {
                if (joint[i].isValid)
                {
                    var temp = joint[i].upperAngleLimit;
                    joint[i].upperAngleLimit = -joint[i].lowerAngleLimit;
                    joint[i].lowerAngleLimit = -temp;
                }
            }
        }
    }

    public readonly void FlipSpringTargets()
    {
        if (joint != null)
        {
            for (int i = 0; i < JointCount; i++)
            {
                if (joint[i].isValid)
                {
                    joint[i].springTargetAngle = -joint[i].springTargetAngle;
                }
            }
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