using System;
using UnityEngine;

public class GrabberClaw : MonoBehaviour
{
    [SerializeField] Transform orientingTransform;
    [SerializeField] Transform grabberBase;
    [SerializeField] Transform grabberRayEndPt;
    [SerializeField] Transform upperGrabArm;//on positive half of grabberRay
    [SerializeField] Transform lowerGrabArm;
    [SerializeField] Collider2D upperArmCollider;
    [SerializeField] Collider2D lowerArmCollider;
    [SerializeField] float grabTolerance;
    [SerializeField] float dropTolerance;
    [SerializeField] float grabArmRotationSpeed;
    [SerializeField] float grabArmMaxRotationRad;
    [SerializeField] float grabArbMinRotationRad;
    [SerializeField] float rotationTolerance;//closer to 1 is stricter

    Vector2 grabArmMaxDirection;
    Vector2 grabArmMinDirection;
    Vector2 goalGrabArmDirection;//in grabber's local frame
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
        goalGrabArmDirection = grabArmMaxDirection;
        mode = Mode.standard;
        hasInvokedTargetReached = false;
    }

    public void Close()
    {
        Debug.Log("closing");
        goalGrabArmDirection = grabArmMinDirection;
        mode = Mode.standard;
        hasInvokedTargetReached = false;
    }

    public void BeginGrab(Collider2D collider)
    {
        //Close();
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

    private void OnValidate()
    {
        grabArmMaxDirection = new(Mathf.Cos(grabArmMaxRotationRad), Mathf.Sin(grabArmMaxRotationRad));
        grabArmMinDirection = new(Mathf.Cos(grabArbMinRotationRad), Mathf.Sin(grabArbMinRotationRad));
    }

    private void Awake()
    {
        grabArmMaxDirection = new(Mathf.Cos(grabArmMaxRotationRad), Mathf.Sin(grabArmMaxRotationRad));
    }

    private void FixedUpdate()
    {
        switch(mode)
        {
            case Mode.standard:
                RotateGrabArms(goalGrabArmDirection, Time.deltaTime);
                break;
            case Mode.grabbingTarget:
                GrabBehavior();
                break;
            case Mode.holdingTarget:
                HoldBehavior();
                break;
            //Mode.off => do nothing
        }
        //if (!hasInvokedTargetReached)
        //{
        //    RotateGrabArms(goalGrabArmDirection, Time.deltaTime);

        //    if (grabTarget && !hasInvokedTargetReached)
        //    {
        //        var d1 = grabTarget.Distance(upperArmCollider);
        //        var d2 = grabTarget.Distance(lowerArmCollider);
        //        if (Mathf.Max(d1.distance, d2.distance) < grabTolerance)
        //        {
        //            grabTarget = null;
        //            hasInvokedTargetReached = true;
        //            TargetReached?.Invoke();
        //        }
        //    }
        //}
    }

    private void GrabBehavior()
    {
        if (grabTarget)
        { 
            RotateGrabArms(grabArmMinDirection, Time.deltaTime);
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
    private void HoldBehavior()
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
                RotateGrabArms(grabArmMinDirection, Time.deltaTime, false);
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

    private void RotateGrabArms(Vector2 goalGrabArmDirection, float dt, bool invokeEvent = true)
    {
        //when parent (e.g. spider) is flipped horizontally, bones will be pointing in direction -boneTransform.right
        //but we have to rotate based on goal for bone right
        var goalUpper = goalGrabArmDirection.x * grabberBase.transform.right + Mathf.Sign(orientingTransform.localScale.x) * goalGrabArmDirection.y * grabberBase.transform.up;
        var goalLower = goalGrabArmDirection.x * grabberBase.transform.right - Mathf.Sign(orientingTransform.localScale.x) * goalGrabArmDirection.y * grabberBase.transform.up;

        var min = Mathf.Min(Vector2.Dot(goalUpper, upperGrabArm.right), Vector2.Dot(goalLower, lowerGrabArm.right));
        if (min > rotationTolerance)
        {
            if (invokeEvent && !hasInvokedTargetReached)
            {
                hasInvokedTargetReached = true;
                TargetReached?.Invoke();
            }
            return;
        }

        upperGrabArm.ApplyCheapRotationalLerpClamped(goalUpper, grabArmRotationSpeed * dt, out _);
        lowerGrabArm.ApplyCheapRotationalLerpClamped(goalLower, grabArmRotationSpeed * dt, out _);
    }
}