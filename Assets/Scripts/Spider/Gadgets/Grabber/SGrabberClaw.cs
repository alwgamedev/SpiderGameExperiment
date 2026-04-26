using System;
using UnityEngine;
using Unity.U2D.Physics;

[Serializable]
public struct GrabberClawDefinition
{
    public PhysicsHingeJointDefinition jointDef;
    public PhysicsBodyDefinition bodyDef;
    public PhysicsShapeDefinition shapeDef;
}

[Serializable]
public struct SGrabberClaw
{
    PhysicsBody upperArm;
    PhysicsHingeJoint upperArmJoint;
    PhysicsBody lowerArm;
    PhysicsHingeJoint lowerArmJoint;
    PhysicsBody grabTarget;

    PhysicsRotate upperTarget;
    PhysicsRotate lowerTarget;

    [SerializeField] float upperArmOpen;
    [SerializeField] float upperArmClosed;
    [SerializeField] float lowerArmOpen;
    [SerializeField] float lowerArmClosed;

    [SerializeField] float rotationTolerance;
    [SerializeField] float grabTolerance;
    [SerializeField] float dropTolerance;

    Mode mode;
    bool targetReached;

    public readonly bool Enabled => upperArm.enabled && lowerArm.enabled;

    enum Mode
    {
        off, standard, grabbingTarget, holdingTarget
    }

    public void Initialize(PhysicsBody anchorBody, Transform upperArmTransform, Transform lowerArmTransform)
    {

    }

    public void Enable()
    {
        upperArm.enabled = true;
        lowerArm.enabled = true;
    }

    public void Disable(bool forgetState)
    {
        if (forgetState)
        {
            mode = Mode.off;
            grabTarget = default;
            targetReached = false;
            upperArm.linearVelocity = Vector2.zero;
            upperArm.angularVelocity = 0;
            lowerArm.linearVelocity = Vector2.zero;
            lowerArm.angularVelocity = 0;
        }
        upperArm.enabled = false;
        lowerArm.enabled = false;
    }

    public readonly void Destroy()
    {
        upperArm.Destroy();
        lowerArm.Destroy();
    }

    public readonly void SyncTransforms()
    {
        upperArm.SyncTransform();
        lowerArm.SyncTransform();
    }

    public void SetSpringTarget(float upperTarget, float lowerTarget)
    {
        upperArmJoint.springTargetAngle = upperTarget;
        lowerArmJoint.springTargetAngle = lowerTarget;
        this.upperTarget = PhysicsRotate.FromDegrees(upperTarget);
        this.lowerTarget = PhysicsRotate.FromDegrees(lowerTarget);
    }

    /// <summary> upperTarget, lowerTarget are relative to joint bodyA rotation. This does not set spring targets. </summary>
    public readonly void SnapToPose(float upperTarget, float lowerTarget)
    {
        var upperPos = upperArmJoint.bodyA.transform.TransformPoint(upperArmJoint.localAnchorA.position);
        var upperRot = upperArmJoint.bodyA.rotation.MultiplyRotation(upperArmJoint.localAnchorA.rotation).MultiplyRotation(PhysicsRotate.FromDegrees(upperTarget));
        upperArm.transform = new PhysicsTransform(upperPos, upperRot);

        var lowerPos = lowerArmJoint.bodyA.transform.TransformPoint(lowerArmJoint.localAnchorA.position);
        var lowerRot = lowerArmJoint.bodyA.rotation.MultiplyRotation(lowerArmJoint.localAnchorA.rotation).MultiplyRotation(PhysicsRotate.FromDegrees(lowerTarget));
        lowerArm.transform = new PhysicsTransform(lowerPos, lowerRot);
    }

    public void Open()
    {
        SetSpringTarget(upperArmOpen, lowerArmOpen);
        mode = Mode.standard;
        targetReached = false;
    }

    public readonly void SnapOpen()
    {
        SnapToPose(upperArmOpen, lowerArmOpen);
    }

    public void Close()
    {
        SetSpringTarget(upperArmClosed, lowerArmClosed);
        mode = Mode.standard;
        targetReached = false;
    }

    public readonly void SnapClosed()
    {
        SnapToPose(upperArmClosed, lowerArmClosed);
    }

    public void BeginGrab(PhysicsBody grabTarget)
    {
        this.grabTarget = grabTarget;
        mode = Mode.grabbingTarget;
        targetReached = false;
    }

    public void BeginHold(PhysicsBody grabTarget)
    {
        this.grabTarget = grabTarget;
        mode = Mode.holdingTarget;
        targetReached = false;
    }

    public bool Update()
    {
        return mode switch
        {
            Mode.standard => StandardBehavior(),
            Mode.grabbingTarget => GrabBehavior(),
            Mode.holdingTarget => HoldBehavior(),
            _ => false
        };
    }

    //check whether spring target has been reached
    private bool StandardBehavior()
    {
        if (targetReached)
        {
            return false;
        }

        //we should also check angular speed is low to make sure joint is actually settling at target angle,
        //but joints/springs will be heavily damped so they won't really be flying past their target

        var err1 = upperTarget.InverseMultiplyRotation(upperArmJoint.bodyA.rotation.InverseMultiplyRotation(upperArm.rotation)).direction;
        if (err1.x < 0 || Mathf.Abs(err1.y) > rotationTolerance)
        {
            return false;
        }

        var err2 = lowerTarget.InverseMultiplyRotation(lowerArmJoint.bodyA.rotation.InverseMultiplyRotation(lowerArm.rotation)).direction;
        if (err2.x < 0 || Mathf.Abs(err2.y) > rotationTolerance)
        {
            return false;
        }

        targetReached = true;
        return true;

    }

    private bool GrabBehavior()
    {
        if (targetReached)
        {
            return false;
        }

        if (grabTarget.isValid)
        {
            if (MaxDistance(grabTarget) < grabTolerance)
            {
                targetReached = true;
                return true;
            }
            else
            {
                return false;
            }
        }
        else
        {
            targetReached = true;
            return true;
        }
    }

    //will tighten grip whenever distance from grab arms to target is > grabThreshold
    //and will invoke "TargetReached" if target is DROPPED
    private bool HoldBehavior()
    {
        if (targetReached)
        {
            return false;
        }

        if (grabTarget.isValid)
        {
            if (MaxDistance(grabTarget) > dropTolerance)
            {
                targetReached = true;
                return true;
            }
            else
            {
                return false;
            }
        }
        else
        {
            targetReached = true;
            return true;
        }
    }

    private readonly float MaxDistance(PhysicsBody body)
    {
        return Mathf.Max(body.Distance(upperArm.GetShapes()[0]).distance, body.Distance(lowerArm.GetShapes()[0]).distance);
    }
}