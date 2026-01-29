using System;
using UnityEngine;

public class GrabberClaw : MonoBehaviour
{
    [SerializeField] Transform grabberBase;
    [SerializeField] Transform grabberRayEndPt;
    [SerializeField] Transform upperGrabArm;//on positive half grabberRay
    [SerializeField] Transform lowerGrabArm;
    [SerializeField] Collider2D upperArmCollider;
    [SerializeField] Collider2D lowerArmCollider;
    [SerializeField] float grabTolerance;
    [SerializeField] float grabArmRotationSpeed;
    [SerializeField] float grabArmMaxRotationRad;
    [SerializeField] float rotationTolerance;//closer to 1 is stricter

    Vector2 grabArmMaxDirection;
    Vector2 goalGrabArmDirection = Vector2.right;//in grabber's local frame

    Collider2D grabTarget;


    bool hasInvokedTargetReached;

    public event Action TargetReached;

    public void Open()
    {
        Debug.Log("opening");
        goalGrabArmDirection = grabArmMaxDirection;
        hasInvokedTargetReached = false;
    }

    public void Close()
    {
        Debug.Log("closing");
        goalGrabArmDirection = Vector2.right;
        hasInvokedTargetReached = false;
    }

    public void BeginGrab(Collider2D collider)
    {
        Close();
        grabTarget = collider;
    }

    private void OnValidate()
    {
        grabArmMaxDirection = new(Mathf.Cos(grabArmMaxRotationRad), Mathf.Sin(grabArmMaxRotationRad));
    }

    private void Awake()
    {
        grabArmMaxDirection = new(Mathf.Cos(grabArmMaxRotationRad), Mathf.Sin(grabArmMaxRotationRad));
    }

    private void FixedUpdate()
    {
        if (!hasInvokedTargetReached)
        {
            RotateGrabArms(goalGrabArmDirection, Time.deltaTime);

            if (grabTarget && !hasInvokedTargetReached)
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
    }

    private void RotateGrabArms(Vector2 goalGrabArmDirection, float dt)
    {
        var goalUpper = goalGrabArmDirection.x * grabberBase.right + goalGrabArmDirection.y * grabberBase.up;
        var goalLower = goalGrabArmDirection.x * grabberBase.right - goalGrabArmDirection.y * grabberBase.up;

        var min = Mathf.Min(Vector2.Dot(goalUpper, upperGrabArm.right), Vector2.Dot(goalLower, lowerGrabArm.right));
        if (min > rotationTolerance)
        {
            if (!hasInvokedTargetReached)
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