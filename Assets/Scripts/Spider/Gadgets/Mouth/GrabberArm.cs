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
    [SerializeField] Transform testTarget;
    [SerializeField] Transform depositTarget;
    [SerializeField] Transform offAnchorPosition;
    [SerializeField] Transform deployedAnchorPosition;
    [SerializeField] Vector2[] foldedPose;
    [SerializeField] Vector2[] defaultPose;
    [SerializeField] Vector2[] depositPose;

    bool actionInProgress;//will not process input while action in progress

    Collider2D grabTarget;

    Action onArmTargetReached;
    Action onAnchorTargetReached;
    Action onGrabberTargetReached;

    //pull out of the garage
    private void DeployPhase1()
    {
        Debug.Log("deploying");
        actionInProgress = true;
        arm.SnapToPose(foldedPose);
        ShowSprites();
        anchor.BeginTargetingTransform(deployedAnchorPosition);

        onArmTargetReached = null;
        onAnchorTargetReached = DeployPhase2;
        onGrabberTargetReached = null;
        //let's set all 3 every time we start a new action (so we don't have any old unneeded callbacks coming in when they shouldn't)
    }

    //move to default pose
    private void DeployPhase2()
    {
        actionInProgress = true;
        arm.BeginTargetingPose(defaultPose);

        onArmTargetReached = CompleteDeployment;
        onAnchorTargetReached = null;
        onGrabberTargetReached = null;
    }

    private void CompleteDeployment()
    {
        Debug.Log("deployment complete");
        //arm.GoIdle();
        //^instead of going idle, we'll continue tracking default pose in case collision knocks the arm out of place
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

    private bool TryBeginGrab()
    {
        if (actionInProgress || arm.IsOff)
        {
            return false;
        }

        var o = arm.Anchor;
        var c = Physics2D.OverlapCircle(o.position, arm.TotalLength, grabMask);
        if (c)
        {
            //2DO: also enforce a time-out for reaching the target + trying to grab it with claw
            actionInProgress = true;
            grabTarget = c;
            grabberClaw.Open();
            arm.BeginTargetingCollider(c);

            onArmTargetReached = GrabTargetWithClaw;//grip the object with claw and then deposit
            onAnchorTargetReached = null;
            onGrabberTargetReached = null;
            return true;

        }
        else
        {
            Debug.Log("no grabbable target in range.");//replace with ui message in future
            return false;
        }
    }

    private void GrabTargetWithClaw()
    {
        actionInProgress = true;
        arm.GoIdle();
        grabberClaw.BeginGrab(grabTarget);

        onArmTargetReached = null;
        onAnchorTargetReached = null;
        onGrabberTargetReached = HeadToDeposit;
    }

    private void HeadToDeposit()
    {
        actionInProgress = true;
        arm.BeginTargetingTransform(depositTarget);
        //arm.BeginTargetingPose(depositPose);

        onArmTargetReached = Deposit;
        onAnchorTargetReached = null;
        onGrabberTargetReached = null;
    }

    private void Deposit()
    {
        actionInProgress = true;
        grabberClaw.Open();

        onArmTargetReached = null;
        onAnchorTargetReached = null;
        onGrabberTargetReached = ParkPhase1;
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
                    DeployPhase1();
                }
                else if (Keyboard.current.altKey.isPressed)//alt+Q to close arm
                {
                    ParkPhase1();
                }
                else
                {
                    TryBeginGrab();
                }
            }
            //else if (Keyboard.current.rKey.wasPressedThisFrame && !arm.IsOff)
            //{
            //    arm.BeginTargetingTransform(testTarget);
            //}
            //else if (Keyboard.current.pKey.wasPressedThisFrame && !arm.IsOff)
            //{
            //    ParkPhase1();
            //}
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
}