using System;
using UnityEngine;

public class GrabberClaw : MonoBehaviour
{
    [SerializeField] Transform grabberBase;
    [SerializeField] Transform grabberRayEndPt;
    [SerializeField] Transform upperGrabArm;//on positive half grabberRay
    [SerializeField] Transform lowerGrabArm;
    [SerializeField] float grabArmRotationSpeed;
    [SerializeField] float grabArmMaxRotationRad;
    [SerializeField] float rotationTolerance;//closer to 1 is stricter

    Vector2 grabArmMaxDirection;
    Vector2 goalGrabArmDirection = Vector2.right;//in grabber's local frame

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
        RotateGrabArms(goalGrabArmDirection, Time.deltaTime);
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
                TargetReached?.Invoke();
                hasInvokedTargetReached = true;
            }
            return;
        }

        upperGrabArm.ApplyCheapRotationalLerpClamped(goalUpper, grabArmRotationSpeed * dt, out _);
        lowerGrabArm.ApplyCheapRotationalLerpClamped(goalLower, grabArmRotationSpeed * dt, out _);
    }
}