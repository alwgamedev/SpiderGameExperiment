using System;
using Unity.U2D.Physics;
using UnityEngine;

[Serializable]
public struct GrabberArm
{
    public JointedChain jointedChain;

    PhysicsBody targetBody;
    Vector2 targetPosition;//local to targetBody
    PhysicsRotate[] targetPose;//when facing right (these never get flipped)

    [SerializeField] float targetingLinearAccel;
    [SerializeField] float targetingRotationalAccel;
    [SerializeField] float poseRotationTolerance;
    [SerializeField] float effectorDistance;
    [SerializeField] float settleVelocity;
    [SerializeField] float targetingTolerance;

    Mode mode;
    bool targetReached;

    enum Mode
    {
        off, idle, trackBody, trackPositionOnBody, trackPose
    }

    public void OnValidate(JointedChainDefinition def, JointedChainSettings settings, bool reversed)
    {
        jointedChain.UpdateDefAndSettings(def, settings, true, true);

        if (reversed)
        {
            jointedChain.FlipAngleLimits();
            //jointedChain.FlipSpringTargets(); -- DON'T DO, because we aren't touching spring angles in UpdateDefAndSettings
        }
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
        jointedChain.Initialize(physTransform, bone, anchorBody, def, settings, true);
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

    public bool Update(ref GrabberClaw claw, bool reversed)
    {
        return mode switch
        {
            Mode.trackBody => TrackBodyBehavior(ref claw, reversed),
            Mode.trackPositionOnBody => TrackPositionInBodyBehavior(reversed),
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

    public readonly void SetSpringTargets(float[] targetAngle, bool reversed)
    {
        for (int i = 0; i < jointedChain.JointCount; i++)
        {
            jointedChain.joint[i].springTargetAngle = reversed ? -targetAngle[i] : targetAngle[i];
        }
    }

    public readonly void SnapToPose(float[] poseAngle, bool reversed)
    {
        for (int i = 0; i < jointedChain.JointCount; i++)
        {
            ref var joint = ref jointedChain.joint[i];
            var pos = jointedChain.JointPosition(i);//joint position is computed using bodyA (good, since previous body already in right place)
            var angle = reversed ? -poseAngle[i] : poseAngle[i];
            var rot = joint.bodyA.rotation.MultiplyRotation(joint.localAnchorA.rotation).MultiplyRotation(PhysicsRotate.FromDegrees(angle));
            jointedChain.body[i].transform = new PhysicsTransform(pos, rot);
        }
    }

    public void BeginTargetingBody(PhysicsBody body)
    {
        targetBody = body;
        mode = Mode.trackBody;
        targetReached = false;
    }

    public void BeginTargetingPositionOnBody(PhysicsBody body, Vector2 bodyLocalPos)
    {
        targetBody = body;
        targetPosition = bodyLocalPos;
        mode = Mode.trackPositionOnBody;
        targetReached = false;
    }

    public void BeginTargetingPose(float[] pose, bool reversed)
    {
        for (int i = 0; i < targetPose.Length; i++)
        {
            jointedChain.joint[i].enableSpring = true;
            jointedChain.joint[i].springTargetAngle = reversed ? -pose[i] : pose[i];
            targetPose[i] = PhysicsRotate.FromDegrees(pose[i]);
        }

        mode = Mode.trackPose;
        targetReached = false;
    }

    public void OnDirectionChanged(PhysicsTransform reflection, Vector2 postTranslation, Transform[] bone)
    {
        jointedChain.OnDirectionChanged(reflection, postTranslation, bone);
    }

    private bool TrackPositionInBodyBehavior(bool reversed)
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

        var effectorPos = EffectorPosition(reversed);
        var targetLocalPos = targetBody.userData.boolValue ? new(-targetPosition.x, targetPosition.y) : targetPosition;
        //^targetBody.userData.boolValue = whether target body is reversed (facing left)
        var targetWorldPos = targetBody.transform.TransformPoint(targetLocalPos);
        var err = targetWorldPos - effectorPos;

        if (err.sqrMagnitude < targetingTolerance * targetingTolerance)
        {
            if (!targetReached && jointedChain.body[^1].GetWorldPointVelocity(effectorPos).sqrMagnitude < settleVelocity * settleVelocity)
            {
                targetReached = true;
                return true;
            }

            return false;
        }

        ReachForPositionFromAbove(effectorPos, targetWorldPos, reversed);
        return false;
    }

    private bool TrackBodyBehavior(ref GrabberClaw claw, bool reversed)
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

        var com = targetBody.worldCenterOfMass;
        if (!targetReached && claw.EnclosesPoint(com))
        {
            targetReached = true;
            return true;
        }

        var effectorPos = EffectorPosition(reversed);

        //find closest point
        //var targetShapes = targetBody.GetShapes();
        //var minDist2 = Mathf.Infinity;
        //Vector2 closestPoint = default;

        //for (int i = 0; i < targetShapes.Length; i++)
        //{
        //    var p = targetShapes[i].ClosestPoint(effectorPos);
        //    var dist2 = Vector2.SqrMagnitude(p - effectorPos);
        //    if (dist2 < minDist2)
        //    {
        //        minDist2 = dist2;
        //        closestPoint = p;
        //        if (minDist2 < TARGETING_TOLERANCE_SQUARED)
        //        {
        //            if (!targetReached)
        //            {
        //                targetReached = true;
        //                return true;
        //            }

        //            return false;
        //        }
        //    }
        //}

        ReachForPositionFromAbove(effectorPos, com, reversed);

        return false;
    }

    private void ReachForPositionFromAbove(Vector2 effectorPos, Vector2 targetPos, bool reversed)
    {
        var effectorBody = jointedChain.body[^1];
        var linearAccel = targetingLinearAccel * (targetPos - effectorPos);
        effectorBody.ApplyForce(effectorBody.mass * linearAccel, effectorPos);

        var up = jointedChain.joint[0].bodyA.rotation.direction.CCWPerp();
        var effectorDir = reversed ? -effectorBody.rotation.direction : effectorBody.rotation.direction;
        var rotAccel = targetingRotationalAccel * MathTools.PseudoAngle(effectorDir, -up);
        effectorBody.ApplyTorque(effectorBody.rotationalInertia * rotAccel);
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
            var target = reversed ? targetPose[i].Inverse() : targetPose[i];
            var error = target.InverseMultiplyRotation(cur).direction;
            if (error.x < 0 || Mathf.Abs(error.y) > poseRotationTolerance)
            {
                return false;
            }
        }

        targetReached = true;
        return true;
    }
}