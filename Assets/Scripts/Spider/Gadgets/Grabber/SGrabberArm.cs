using System;
using Unity.U2D.Physics;
using UnityEngine;

[Serializable]
public struct SGrabberArm
{
    const float TARGETING_TOLERANCE_SQUARED = MathTools.o41;//for a targeting tolerance of 0.01

    public JointedChain jointedChain;

    PhysicsBody targetBody;
    Vector2 targetPosition;//local to targetBody
    PhysicsRotate[] targetPose;

    [SerializeField] float targetingAccel;
    [SerializeField] float targetErrorCap;
    [SerializeField] float poseRotationTolerance;
    [SerializeField] float effectorDistance;

    Mode mode;
    bool targetReached;

    enum Mode
    {
        off, idle, trackBody, trackPose
    }

    public void OnValidate()
    {
        //update settings
    }

    public readonly void OnDrawGizmos(Transform[] bone, float[] width, JointedChainSettings settings,
        bool drawBodyGizmos, bool drawAngleLimitGizmos, bool reversed)
    {
        if (drawBodyGizmos)
        {
            JointedChain.DrawBodyGizmos(bone, width);
        }

        if (drawAngleLimitGizmos)
        {
            if (Application.isPlaying)
            {
                jointedChain.DrawAngleGizmos(reversed);
            }
            else
            {
                JointedChain.DrawAngleGizmos(bone, settings);
            }
        }
    }

    Vector2 EffectorPosition(bool reversed)
    {
        return jointedChain.EffectorPosition(reversed) + (reversed ? -effectorDistance : effectorDistance) * jointedChain.body[^1].rotation.direction;
    }
    public readonly bool Enabled() => jointedChain.Enabled();

    public void Initialize(Transform[] physTransform, Transform[] bone, PhysicsBody anchorBody,
        JointedChainDefinition def, JointedChainSettings settings)
    {
        jointedChain.Initialize(physTransform, bone, anchorBody, def, settings);
        targetPose = new PhysicsRotate[jointedChain.JointCount];
    }

    public readonly void Enable()
    {
        jointedChain.Enable();
    }

    public void Disable(bool forgetState)
    {
        if (forgetState)
        {
            mode = Mode.off;
        }

        jointedChain.Disable(forgetState);
    }

    public readonly void Destroy()
    {
        jointedChain.Destroy();
    }

    public bool Update(bool reversed)
    {
        return mode switch
        {
            Mode.trackBody => TrackBodyBehavior(reversed),
            Mode.trackPose => TrackPoseBehavior(reversed),
            _ => false
        };
    }

    public readonly void SyncTransforms()
    {
        jointedChain.SyncTransforms();
    }

    public readonly void EnableSprings(bool val)
    {
        for (int i = 0; i < jointedChain.JointCount; i++)
        {
            jointedChain.joint[i].enableSpring = val;
        }
    }

    public readonly void SetSpringTargets(float[] targetAngle)
    {
        for (int i = 0; i < jointedChain.JointCount; i++)
        {
            jointedChain.joint[i].springTargetAngle = targetAngle[i];
        }
    }

    public readonly void SnapToPose(float[] poseAngle)
    {
        for (int i = 0; i < jointedChain.JointCount; i++)
        {
            ref var joint = ref jointedChain.joint[i];
            var pos = jointedChain.JointPosition(i);//joint position is computed using bodyA (good, since previous body already in right place)
            var rot = joint.bodyA.rotation.MultiplyRotation(joint.localAnchorA.rotation).MultiplyRotation(PhysicsRotate.FromDegrees(poseAngle[i]));
            jointedChain.body[i].transform = new PhysicsTransform(pos, rot);
        }
    }

    public void BeginTargetingBody(PhysicsBody body, Vector2 bodyPos)
    {
        targetBody = body;
        targetPosition = bodyPos;
        mode = Mode.trackBody;
        targetReached = false;
    }

    public void BeginTargetingPose(float[] pose)
    {
        for (int i = 0; i < targetPose.Length; i++)
        {
            jointedChain.joint[i].enableSpring = true;
            jointedChain.joint[i].springTargetAngle = pose[i];
            targetPose[i] = PhysicsRotate.FromDegrees(pose[i]);
        }

        mode = Mode.trackPose;
        targetReached = false;
    }

    private bool TrackBodyBehavior(bool reversed)
    {
        if (!targetBody.isValid)
        {
            if (!targetReached)
            {
                targetReached = true;
                return true;
            }

            return false;
        }

        var targetPos = targetBody.transform.TransformPoint(targetPosition);
        var err = targetPos - EffectorPosition(reversed);
        var err2 = err.sqrMagnitude;

        if (err2 < TARGETING_TOLERANCE_SQUARED)
        {
            if (!targetReached)
            {
                targetReached = true;
                return true;
            }

            return false;
        }

        if (err2 > targetErrorCap * targetErrorCap)
        {
            err = targetErrorCap / Mathf.Sqrt(err2) * err;
        }

        ref var arm = ref jointedChain;
        var a = targetingAccel * err;
        for (int i = 0; i < jointedChain.body.Length; i++)
        {
            arm.body[i].ApplyForce(arm.body[i].mass * a, jointedChain.NextPosition(i, reversed));
        }

        return false;
    }

    private bool TrackPoseBehavior(bool reversed)
    {
        if (targetReached)
        {
            return false;
        }

        ref var arm = ref jointedChain;

        for (int i = 0; i < jointedChain.JointCount; i++)
        {
            ref var joint = ref arm.joint[i];
            var cur = joint.localAnchorA.rotation.InverseMultiplyRotation(joint.bodyA.rotation.InverseMultiplyRotation(joint.bodyB.rotation));
            var error = targetPose[i].InverseMultiplyRotation(cur).direction;
            if (error.x < 0 || Mathf.Abs(error.y) > poseRotationTolerance)
            {
                return false;
            }
        }

        targetReached = true;
        return true;
    }
}