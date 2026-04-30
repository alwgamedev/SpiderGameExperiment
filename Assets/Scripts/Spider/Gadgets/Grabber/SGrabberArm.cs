using System;
using Unity.U2D.Physics;
using UnityEngine;

[Serializable]
public struct SGrabberArm
{
    const float TARGETING_TOLERANCE = 0.01f;
    const float TARGETING_TOLERANCE_SQUARED = TARGETING_TOLERANCE * TARGETING_TOLERANCE;//for a targeting tolerance of 0.01

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
        off, idle, trackBody, trackPositionOnBody, trackPose
    }

    public void OnValidate(JointedChainDefinition def, JointedChainSettings settings, bool reversed)
    {
        jointedChain.UpdateDefAndSettings(def, settings, true, true);

        if (reversed)
        {
            jointedChain.FlipAngleLimits();
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

    public bool Update(bool reversed)
    {
        return mode switch
        {
            Mode.trackBody => TrackBodyBehavior(false, reversed),
            Mode.trackPositionOnBody => TrackBodyBehavior(true, reversed),
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

    public readonly void SetSpringTargetsToZero()
    {
        for (int i = 0; i < jointedChain.JointCount; i++)
        {
            jointedChain.joint[i].springTargetAngle = 0;
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

    private bool TrackBodyBehavior(bool trackLocalPos, bool reversed)
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
        Vector2 err;
        if (trackLocalPos)
        {
            var targetPos = targetBody.transform.TransformPoint(targetPosition);
            err = targetPos - effectorPos;
            Debug.DrawLine(effectorPos, targetPos, Color.red);
        }
        else
        {
            var targetShapes = targetBody.GetShapes();
            var minDist2 = Mathf.Infinity;
            err = Vector2.zero;
            
            for (int i = 0; i < targetShapes.Length; i++)
            {
                var p = targetShapes[i].ClosestPoint(effectorPos);
                var d = p - effectorPos;
                var dist2 = Vector2.SqrMagnitude(d);
                if (dist2 < minDist2)
                {
                    minDist2 = dist2;
                    err = d;
                    if (minDist2 < TARGETING_TOLERANCE_SQUARED)
                    {
                        break;
                    }
                }
            }
            
            Debug.DrawLine(effectorPos, effectorPos + err, Color.red);
        }

        return TrackBodyInternal(effectorPos, err, reversed);
    }

    private bool TrackBodyInternal(Vector2 effectorPos, Vector2 err, bool reversed)
    {
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

        var u = err.normalized;
        var a = targetingAccel * err;
        var last = jointedChain.JointCount - 1;
        jointedChain.body[last].ApplyForce(jointedChain.body[last].mass * a, effectorPos);

        //AccelerateWithDamping(jointedChain.body[last], effectorPos, a, u, trackDamping);
        //for (int i = 0; i < last; i++)
        //{
        //    AccelerateWithDamping(jointedChain.body[i], jointedChain.NextPosition(i, reversed), a, u, trackDamping);
        //}
        //arm.body[last].ApplyForce(arm.body[last].mass * a, effectorPos);
        //for (int i = 0; i < last; i++)
        //{
        //    arm.body[i].ApplyForce(arm.body[i].mass * a, jointedChain.NextPosition(i, reversed));
        //}

        return false;

        //static void AccelerateWithDamping(PhysicsBody body, Vector2 worldPos, Vector2 accel, Vector2 accelDirection, float damping)
        //{
        //    var v = Vector2.Dot(body.GetWorldPointVelocity(worldPos), accelDirection);
        //    accel -= damping * Mathf.Sign(v) * v * accelDirection;
        //    body.ApplyForce(body.mass * accel, worldPos);
        //}
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