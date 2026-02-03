using System;
using UnityEngine;

public class GrabberClaw : MonoBehaviour
{
    [SerializeField] Transform upperGrabArm;//on positive half of grabberRay
    [SerializeField] Transform lowerGrabArm;
    [SerializeField] Collider2D upperArmCollider;
    [SerializeField] Collider2D lowerArmCollider;
    [SerializeField] Quaternion upperArmOpen;
    [SerializeField] Quaternion upperArmClosed;
    [SerializeField] Quaternion lowerArmOpen;
    [SerializeField] Quaternion lowerArmClosed;
    [SerializeField] float grabArmRotationSpeed;
    [SerializeField] float rotationTolerance;//closer to 1 is stricter
    [SerializeField] float grabTolerance;
    [SerializeField] float dropTolerance;

    Quaternion upperGoal;
    Quaternion lowerGoal;
    Mode mode;

    Collider2D grabTarget;

    bool hasInvokedTargetReached;

    enum Mode
    {
        off, standard, grabbingTarget, holdingTarget
    }

    public event Action TargetReached;

    public void Open()
    {
        Debug.Log("opening");
        upperGoal = upperArmOpen;
        lowerGoal = lowerArmOpen;
        mode = Mode.standard;
        hasInvokedTargetReached = false;
    }

    public void SnapOpen()
    {
        upperGrabArm.localRotation = upperArmOpen;
        lowerGrabArm.localRotation = lowerArmOpen;
    }

    public void Close()
    {
        Debug.Log("closing");
        upperGoal = upperArmClosed;
        lowerGoal = lowerArmClosed;
        mode = Mode.standard;
        hasInvokedTargetReached = false;
    }

    public void SnapClosed()
    {
        upperGrabArm.localRotation = upperArmClosed;
        lowerGrabArm.localRotation = lowerArmClosed;
    }

    public void BeginGrab(Collider2D collider)
    {
        grabTarget = collider;
        mode = Mode.grabbingTarget;
        hasInvokedTargetReached = false;
    }

    public void BeginHold(Collider2D collider)
    {
        grabTarget = collider;
        mode = Mode.holdingTarget;
        hasInvokedTargetReached = false;
    }

    public void TurnOff()
    {
        mode = Mode.off;
        grabTarget = null;
        hasInvokedTargetReached = false;
    }

    private void FixedUpdate()
    {
        switch(mode)
        {
            case Mode.standard:
                RotateGrabArms(upperGoal, lowerGoal, grabArmRotationSpeed * Time.deltaTime, true);
                break;
            case Mode.grabbingTarget:
                GrabBehavior(Time.deltaTime);
                break;
            case Mode.holdingTarget:
                HoldBehavior(Time.deltaTime);
                break;
            //Mode.off => do nothing
        }
    }

    private void GrabBehavior(float dt)
    {
        if (grabTarget)
        {
            RotateGrabArms(upperArmClosed, lowerArmClosed, grabArmRotationSpeed * dt, true);
            if (hasInvokedTargetReached)
            {
                grabTarget = null;//we reached min direction
            }
            else
            {
                var d1 = grabTarget.Distance(upperArmCollider);
                var d2 = grabTarget.Distance(lowerArmCollider);
                if (Mathf.Max(d1.distance, d2.distance) < grabTolerance)
                {
                    grabTarget = null;
                    hasInvokedTargetReached = true;
                    TargetReached?.Invoke();
                }
            }
        }
        else if (grabTarget != null)
        {
            grabTarget = null;
            if (!hasInvokedTargetReached)
            {
                hasInvokedTargetReached = true;
                TargetReached?.Invoke();
            }
        }
    }

    //will tighten grip whenever distance from grab arms to target is > grabThreshold
    //and will invoke "TargetReached" if target is DROPPED
    private void HoldBehavior(float dt)
    {
        if (grabTarget)
        {
            var d1 = grabTarget.Distance(upperArmCollider);
            var d2 = grabTarget.Distance(lowerArmCollider);
            var max = Mathf.Max(d1.distance, d2.distance);
            if (max > dropTolerance)
            {
                grabTarget = null;
                hasInvokedTargetReached = true;
                TargetReached?.Invoke();
            }
            else if (max > grabTolerance)
            {
                RotateGrabArms(upperArmClosed, lowerArmClosed, grabArmRotationSpeed * dt, false);
            }
        }
        else if (grabTarget != null)
        {
            grabTarget = null;
            if (!hasInvokedTargetReached)
            {
                hasInvokedTargetReached = true;
                TargetReached?.Invoke();
            }
        }
    }

    private void RotateGrabArms(Quaternion upperGoal, Quaternion lowerGoal, float lerpAmount, bool invokeEvent = true)
    {
        //q & -q represent the same rotation so take abs of the dot!
        var min = Mathf.Min(Mathf.Abs(Quaternion.Dot(upperGoal, upperGrabArm.localRotation)), Mathf.Abs(Quaternion.Dot(lowerGoal, lowerGrabArm.localRotation)));
        if (min > rotationTolerance)
        {
            if (invokeEvent && !hasInvokedTargetReached)
            {
                hasInvokedTargetReached = true;
                TargetReached?.Invoke();
            }
            return;
        }

        upperGrabArm.localRotation = Quaternion.Lerp(upperGrabArm.localRotation, upperGoal, lerpAmount);
        lowerGrabArm.localRotation = Quaternion.Lerp(lowerGrabArm.localRotation, lowerGoal, lerpAmount);
    }
}