using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class GrabberArm : MonoBehaviour
{
    [SerializeField] PhysicsBasedIKArm arm;
    [SerializeField] ArmAnchor anchor;
    [SerializeField] GrabberClaw grabberClaw;
    [SerializeField] SpriteRenderer[] armSprites;
    [SerializeField] LayerMask grabMask;
    [SerializeField] float grabAttemptRadius;
    [SerializeField] Transform testTarget;
    [SerializeField] Transform depositTarget;
    [SerializeField] Transform offAnchorPosition;
    [SerializeField] Transform deployedAnchorPosition;
    [SerializeField] Vector2[] foldedPose;
    [SerializeField] Vector2[] defaultPose;
    [SerializeField] Vector2[] depositPose;
    [SerializeField] int depositPoseStartIndex;
    [SerializeField] float grabTimeOut;

    bool actionInProgress;//will not process input while action in progress
    float grabTimer;

    Collider2D grabTarget;

    Action onArmTargetReached;
    Action onAnchorTargetReached;
    Action onGrabberTargetReached;

    //pull out of the garage
    private void Deploy()
    {
        Debug.Log("deploying");
        actionInProgress = true;
        arm.SnapToPose(foldedPose);
        ShowSprites();
        anchor.BeginTargetingTransform(deployedAnchorPosition);

        onArmTargetReached = null;
        onAnchorTargetReached = GoIdle;
        onGrabberTargetReached = null;
        //let's set all 3 every time we start a new action (so we don't have any old unneeded callbacks coming in when they shouldn't)
    }

    //move to default pose
    private void GoIdle()
    {
        actionInProgress = true;
        grabberClaw.Close();
        arm.BeginTargetingPose(defaultPose);

        onArmTargetReached = CompleteGoIdle;
        onAnchorTargetReached = null;
        onGrabberTargetReached = null;
    }

    private void CompleteGoIdle()
    {
        //we'll continue tracking default pose in case collision knocks the arm out of place
        actionInProgress = false;

        onArmTargetReached = null;
        onAnchorTargetReached = null;
        onGrabberTargetReached = null;
    }

    //return to default pose
    private void ParkPhase1()
    {
        actionInProgress = true;
        arm.BeginTargetingPose(defaultPose);

        onArmTargetReached = ParkPhase2;
        onAnchorTargetReached = null;
        onGrabberTargetReached = null;
    }

    //close grabber and fold up
    private void ParkPhase2()
    {
        actionInProgress = true;
        grabberClaw.Close();

        onArmTargetReached = null;
        onAnchorTargetReached = null;
        onGrabberTargetReached = ParkPhase3;
    }

    private void ParkPhase3()
    {
        actionInProgress = true;
        arm.BeginTargetingPose(foldedPose);
        grabberClaw.TurnOff();

        onArmTargetReached = ParkPhase4;
        onAnchorTargetReached = null;
        onGrabberTargetReached = null;
    }

    //back into the garage
    private void ParkPhase4()
    {
        actionInProgress = true;
        arm.TurnOff();
        anchor.BeginTargetingTransform(offAnchorPosition);

        onArmTargetReached = null;
        onAnchorTargetReached = CompletePark;
        onGrabberTargetReached = null;
    }

    private void CompletePark()
    {
        HideSprites();
        actionInProgress = false;

        onArmTargetReached = null;
        onAnchorTargetReached = null;
        onGrabberTargetReached = null;
    }

    private void TryBeginGrab()
    {
        if (actionInProgress || arm.IsOff)
        {
            return;
        }

        var o = arm.Anchor;
        var c = Physics2D.OverlapCircle(o.position, grabAttemptRadius, grabMask);
        //^we'll use a manually set radius here rather than actual arm length, so we can still try when object is slightly out of reach
        //(maybe it's rolling and will become in reach, or maybe it has a large collider that will allow us to reach the edge of it)
        if (c)
        {
            //2DO: also enforce a time-out for reaching the target + trying to grab it with claw
            actionInProgress = true;
            grabTarget = c;
            grabberClaw.Open();
            arm.BeginTargetingCollider(c);
            grabTimer = grabTimeOut;

            onArmTargetReached = CompleteGrab;//grip the object with claw and then deposit
            onAnchorTargetReached = null;
            onGrabberTargetReached = null;
        }
        else
        {
            Debug.Log("no grabbable target in range.");//replace with ui message in future
        }
    }

    private void CompleteGrab()
    {
        actionInProgress = true;
        grabTimer = 0;
        if (grabTarget)
        {
            grabberClaw.BeginGrab(grabTarget);

            onArmTargetReached = null;
            onAnchorTargetReached = null;
            onGrabberTargetReached = HeadToDeposit;
        }
        else
        {
            OnGrabFailed();
        }
    }

    private void HeadToDeposit()
    {
        if (grabTarget)
        {
            arm.BeginTargetingTransform(depositTarget, depositPoseStartIndex, depositPose);
            grabberClaw.BeginHold(grabTarget);

            onArmTargetReached = Deposit;
            onAnchorTargetReached = null;
            onGrabberTargetReached = OnGrabFailed;
        }
        else
        {
            OnGrabFailed();
        }
    }

    private void Deposit()
    {
        actionInProgress = true;
        grabberClaw.Open();

        onArmTargetReached = null;
        onAnchorTargetReached = null;
        onGrabberTargetReached = CompleteDeposit;
    }

    private void CompleteDeposit()
    {
        if (grabTarget)
        {
            Debug.Log($"deposited {grabTarget.name}");
            grabTarget = null;
            GoIdle();
        }
        else
        {
            OnGrabFailed();
        }
    }

    private void OnEnable()
    {
        arm.TargetReached += OnArmTargetReached;
        anchor.TargetReached += OnAnchorTargetReached;
        grabberClaw.TargetReached += OnGrabberTargetReached;
    }

    private void Start()
    {
        arm.Initialize();
        arm.TurnOff();
        arm.SnapToPose(foldedPose);
        anchor.SetPosition(offAnchorPosition.position);
        HideSprites();
    }

    private void OnDisable()
    {
        arm.TargetReached -= OnArmTargetReached;
        anchor.TargetReached -= OnAnchorTargetReached;
        grabberClaw.TargetReached -= OnGrabberTargetReached;
    }

    private void Update()
    {
        //handle input
        if (!actionInProgress)
        {
            if (Keyboard.current.qKey.wasPressedThisFrame)
            {
                if (arm.IsOff)
                {
                    Deploy();
                }
                else if (Keyboard.current.shiftKey.isPressed)//shift+Q to close arm
                {
                    ParkPhase1();
                }
                else
                {
                    TryBeginGrab();
                }
            }
            else if (Keyboard.current.rKey.wasPressedThisFrame && !arm.IsOff)
            {
                arm.BeginTargetingTransform(testTarget);
            }
        }
        else if (grabTimer > 0)
        {
            grabTimer -= Time.deltaTime;
            if (!(grabTimer > 0))
            {
                OnGrabFailed();
            }
        }
    }

    private void OnArmTargetReached()
    {
        onArmTargetReached?.Invoke();
    }

    private void OnAnchorTargetReached()
    {
        onAnchorTargetReached?.Invoke();
    }

    private void OnGrabberTargetReached()
    {
        onGrabberTargetReached?.Invoke();
    }

    private void ShowSprites()
    {
        foreach (var r in armSprites)
        {
            r.enabled = true;
        }
    }

    private void HideSprites()
    {
        foreach (var r in armSprites)
        {
            r.enabled = false;
        }
    }

    private void OnGrabFailed()
    {
        Debug.Log("grab failed");
        grabTarget = null;
        GoIdle();
    }
}