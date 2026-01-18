using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class GrabberArm : MonoBehaviour
{
    [SerializeField] IKExtendableArm arm;
    [SerializeField] Transform grabber;
    [SerializeField] Transform grabberRayEndPt;
    [SerializeField] Transform upperGrabArm;//on positive half grabberRay
    [SerializeField] Transform lowerGrabArm;
    [SerializeField] float grabArmRotationSpeed;
    [SerializeField] float grabArmMaxRotationRad;
    [SerializeField] LayerMask grabMask;
    [SerializeField] Transform testTarget;
    [SerializeField] Transform depositTarget;

    Vector2 grabArmMaxDirection;
    Vector2 goalGrabArmDirection = Vector2.right;//in grabber's local frame
    //bool grabArmRotationInProgress
    //Action onGrabArmRotationComplete;

    Collider2D grabTarget;

    State state;
    
    enum State
    {
        idle, grabbing, depositing
    }

    //note: when childed to spider that flips horizontally sometimes,
    //we may need to adjust how the arm bone rotations are set (see how you did it in spiderMC script, e.g. for head & abdomen)

    public void GrabNearestObject()
    {
        var o = arm.Chain[0];
        var c = Physics2D.OverlapCircle(o.position, arm.TotalLength, grabMask);
        if (c)
        {
            grabTarget = c;
            OpenGrabber();
            arm.BeginTargetingTransform(c.transform);
            state = State.grabbing;

        }
    }

    public void GoIdle()
    {
        arm.BeginTargetingTransform(testTarget);
        state = State.idle;
    }

    private void OnValidate()
    {
        grabArmMaxDirection = new(Mathf.Cos(grabArmMaxRotationRad), Mathf.Sin(grabArmMaxRotationRad));
    }

    private void Awake()
    {
        grabArmMaxDirection = new(Mathf.Cos(grabArmMaxRotationRad), Mathf.Sin(grabArmMaxRotationRad));
    }

    private void OnEnable()
    {
        state = State.idle;
        arm.TargetReached += OnTargetReached;
    }

    private void Start()
    {
        GoIdle();
    }

    private void OnDisable()
    {
        arm.TargetReached -= OnTargetReached;
    }

    //private void Start()
    //{
    //    state = State.idle;
    //    //arm.BeginTargetingTransform(testTarget);
    //}

    private void Update()
    {
        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            if (state == State.idle)
            {
                GrabNearestObject();
            }
            else
            {
                GoIdle();
            }
        }
    }

    private void FixedUpdate()
    {
        RotateGrabArms(goalGrabArmDirection, Time.deltaTime);
    }

    private void OnTargetReached()
    {
        switch (state)
        {
            case State.grabbing:
                CloseGrabber();
                arm.BeginTargetingTransform(depositTarget);
                state = State.depositing;
                Debug.Log("grab completed. depositing...");
                break;
            case State.depositing:
                OpenGrabber();
                arm.BeginTargetingTransform(testTarget);
                grabTarget = null;
                state = State.idle;
                Debug.Log("deposit completed. going back to idle.");
                break;
        }
    }

    private void OpenGrabber()
    {
        Debug.Log("opening");
        goalGrabArmDirection = grabArmMaxDirection;
    }

    private void CloseGrabber()
    {
        Debug.Log("closing");
        goalGrabArmDirection = Vector2.right;
    }

    private void RotateGrabArms(Vector2 goalGrabArmDirection, float dt)
    {
        var goalUpper = goalGrabArmDirection.x * grabber.right + goalGrabArmDirection.y * grabber.up;
        var goalLower = goalGrabArmDirection.x * grabber.right - goalGrabArmDirection.y * grabber.up;
        upperGrabArm.ApplyCheapRotationalLerpClamped(goalUpper, grabArmRotationSpeed * dt, out _);
        lowerGrabArm.ApplyCheapRotationalLerpClamped(goalLower, grabArmRotationSpeed * dt, out _);
    }
}