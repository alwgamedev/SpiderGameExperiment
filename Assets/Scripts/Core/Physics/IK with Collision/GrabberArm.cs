using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class GrabberArm : MonoBehaviour
{
    [SerializeField] PhysicsBasedIKArm arm;
    [SerializeField] ArmAnchor anchor;
    [SerializeField] GrabberClaw grabberClaw;
    [SerializeField] SpriteRenderer[] armSprites;
    [SerializeField] Transform grabber;
    [SerializeField] Transform grabberRayEndPt;
    [SerializeField] Transform upperGrabArm;//on positive half grabberRay
    [SerializeField] Transform lowerGrabArm;
    [SerializeField] float grabArmRotationSpeed;
    [SerializeField] float grabArmMaxRotationRad;
    [SerializeField] LayerMask grabMask;
    [SerializeField] Transform testTarget;
    [SerializeField] Transform depositTarget;
    [SerializeField] Transform offAnchorPosition;
    [SerializeField] Transform deployedAnchorPosition;

    Vector2 grabArmMaxDirection;
    Vector2 goalGrabArmDirection = Vector2.right;//in grabber's local frame

    bool actionInProgress;//will not process input while action in progress

    Collider2D grabTarget;

    Action onArmTargetReached;
    Action onAnchorTargetReached;
    Action onGrabberTargetReached;

    private void Deploy()
    {
        Debug.Log("deploying");
        actionInProgress = true;
        arm.SnapToFolded1();
        ShowSprites();
        anchor.BeginTargetingTransform(deployedAnchorPosition);

        onArmTargetReached = null;
        onAnchorTargetReached = () =>
        {
            arm.GoIdle();
            actionInProgress = false;
            Debug.Log("deployment complete");
        };
        onGrabberTargetReached = null;
        //let's set all 3 every time we start a new action (so we don't have any old unneeded callbacks coming in when they shouldn't)
    }

    private void PrepareToPark()
    {
        Debug.Log("preparing to park (folding up)");
        actionInProgress = true;
        arm.FoldUp1();
        grabberClaw.Close();

        onArmTargetReached = Park;
        onAnchorTargetReached = null;
        onGrabberTargetReached = null;
    }

    private void Park()
    {
        Debug.Log("parking");
        actionInProgress = true;
        arm.TurnOff();
        anchor.BeginTargetingTransform(offAnchorPosition);

        onArmTargetReached = null;
        onAnchorTargetReached = () =>
        {
            HideSprites();
            actionInProgress = false;
        };
        onGrabberTargetReached = null;
    }

    private bool TryBeginGrab()
    {
        if (actionInProgress || arm.IsOff)
        {
            return false;
        }

        Debug.Log("looking for grab target...");

        var o = arm.Anchor;
        var c = Physics2D.OverlapCircle(o.position, arm.TotalLength, grabMask);
        if (c)
        {
            //2DO: also enforce a time-out for reaching the target + trying to grab it with claw
            actionInProgress = true;
            grabTarget = c;
            grabberClaw.Open();
            arm.BeginTargetingCollider(c);

            Debug.Log("reaching for grab target");

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
        Debug.Log("grabbing target with claw");
        actionInProgress = true;
        arm.GoIdle();
        grabberClaw.Close();

        onArmTargetReached = null;
        onAnchorTargetReached = null;
        onGrabberTargetReached = Deposit;
    }

    //simple for now (in future we will want to first grip object with claw (closing to specific width),
    //anchor that object in so it looks like we're carrying it, and then begin deposit)
    private void Deposit()
    {
        Debug.Log("beginning deposit");
        actionInProgress = true;
        grabberClaw.Close();
        arm.BeginTargetingTransform(depositTarget);

        onArmTargetReached = PrepareToPark;
        onAnchorTargetReached = null;
        onGrabberTargetReached = null;
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
        arm.SnapToFolded();
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
        if (!actionInProgress && Keyboard.current.qKey.wasPressedThisFrame)
        {
            if (arm.IsOff)
            {
                Deploy();
            }
            else
            {
                TryBeginGrab();
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
}