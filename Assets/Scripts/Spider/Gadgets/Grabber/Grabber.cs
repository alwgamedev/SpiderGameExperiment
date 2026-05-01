using System;
using Unity.U2D.Physics;
using UnityEngine;

[Serializable]
public class Grabber
{
    [Header("Arm")]
    [SerializeField] JointedChainDefinition armDef;
    [SerializeField] JointedChainSettings armSettings;//might want to have angle limits for joint 0
    [SerializeField] GrabberArm arm;
    [SerializeField] Transform[] armPhysTransforms;//centered between joint positions
    [SerializeField] Transform[] armNodes;//joint positions + effector
    [SerializeField] float[] foldedPose;//poses are spring target angles for the arm joints
    [SerializeField] float[] defaultPose;
#if UNITY_EDITOR
    [SerializeField] bool drawBodyGizmos;
    [SerializeField] bool drawAngleLimitGizmos;
#endif

    [Header("Claw")]
    [SerializeField] GrabberClawDefinition clawDef;
    [SerializeField] GrabberClaw claw;
    [SerializeField] Transform upperClawPhysTransform;
    [SerializeField] Transform[] upperClawBone;
    [SerializeField] Transform lowerClawPhysTransform;
    [SerializeField] Transform[] lowerClawBone;
#if UNITY_EDITOR
    [SerializeField] bool drawClawShapeGizmos;
#endif

    [Header("Auxiliary Parts")]
    [SerializeField] GrabberAnchor anchor;
    [SerializeField] DoubleDoor depositDoor;
    [SerializeField] DoubleDoor mouth;
    [SerializeField] SpriteRenderer[] sprite;
    [SerializeField] Transform depositTarget;
    [SerializeField] Transform offAnchor;
    [SerializeField] Transform deployedAnchor;
#if UNITY_EDITOR
    [SerializeField] bool drawAnchorGizmos;
#endif

    [Header("Grab")]
    [SerializeField] PhysicsQuery.QueryFilter grabFilter;
    [SerializeField] float grabAttemptRadius;
    [SerializeField] float grabTimeOut;

    SpiderInput input;

    bool reversed;
    bool taskInProgress;//will not process input while task in progress
    float grabTimer;

    PhysicsWorld world;
    PhysicsBody depositTargetBody;
    Vector2 depositTargetPosition;
    PhysicsBody grabTarget;

    Action onArmTargetReached;
    Action onAnchorTargetReached;
    Action onClawTargetReached;
    Action onClawTargetFailed;

    //2do:
    //1) direction change - will need to reverse spring target angles probably?
    //2) test grabbing & depositing - play with deposit pose and deposit target position
    //3) sprite mask to hide arm when parking (mask only active during anchor moving stage of deployment/parking so they never block while arm is out) 

#if UNITY_EDITOR
    public void OnValidate()
    {
        arm.OnValidate(armDef, armSettings, reversed);
        claw.OnValidate(clawDef);
    }

    public void OnDrawGizmos()
    {
        arm.OnDrawGizmos(armNodes, armDef.width, armSettings, drawBodyGizmos, drawAngleLimitGizmos, reversed);

        if (drawClawShapeGizmos)
        {
            GrabberClaw.DrawBodyGizmos(upperClawBone, clawDef.upperWidth);
            GrabberClaw.DrawBodyGizmos(lowerClawBone, clawDef.lowerWidth);
        }

        if (drawAnchorGizmos)
        {
            anchor.OnDrawGizmos();
        }
    }

    public void CenterArmTransforms()
    {
        JointedChain.CenterPhysicsTransforms(armPhysTransforms, armNodes);
    }
#endif


    //LIFETIME

    //anchor body will be head of spider
    public void Initialize(SpiderInput input, PhysicsBody anchorBody, PhysicsBody depositTargetBody)
    {
        this.input = input;
        world = anchorBody.world;
        this.depositTargetBody = depositTargetBody;
        depositTargetPosition = depositTargetBody.transform.InverseTransformPoint(depositTarget.position);

        arm.Initialize(armPhysTransforms, armNodes, anchorBody, armDef, armSettings);
        anchor.Initialize(arm.jointedChain.joint[0], offAnchor.position, deployedAnchor.position);
        claw.Initialize(arm.jointedChain.body[^1], clawDef,
            upperClawPhysTransform, upperClawBone,
            lowerClawPhysTransform, lowerClawBone);

        depositDoor.SnapClosed();
        mouth.SnapClosed();

        //initialize arm and claw

        //this was the old start method:
        //arm.Initialize();
        //arm.TurnOff();
        //arm.SnapToPose(foldedPose);
        //inventoryDoor.SnapClosed();
        //mouth.SnapClosed();
        //anchor.SetPosition(offAnchorPosition.position);
        //grabberClaw.SnapClosed();
        //HideSprites();
    }

    public void Destroy()
    {
        arm.Destroy();
        claw.Destroy();
    }


    //STATE MANAGEMENT

    public void Enable()
    {
        arm.Enable();
        claw.Enable();
    }

    public void Disable(bool forgetState)
    {
        if (forgetState)
        {
            anchor.Disable();
            taskInProgress = false;
            onAnchorTargetReached = null;
            onArmTargetReached = null;
            onClawTargetReached = null;
        }

        arm.Disable(forgetState);
        claw.Disable(forgetState);
    }

    public void ShowSprites()
    {
        for (int i = 0; i < sprite.Length; i++)
        {
            sprite[i].enabled = true;
        }
    }

    public void HideSprites()
    {
        for (int i = 0; i < sprite.Length; i++)
        {
            sprite[i].enabled = false;
        }
    }

    public void Update(float dt)
    {
        //handle input
        if (!taskInProgress)
        {
            if (input.QAction.WasPressedThisFrame())
            {
                if (!arm.Enabled())
                {
                    Deploy();
                }
                else if (input.ShiftAction.IsPressed())//shift+Q to close arm
                {
                    ParkPhase1();
                }
                else
                {
                    TryBeginGrab();
                }
            }
        }
        else
        {
            if (grabTimer > 0)
            {
                grabTimer -= dt;
                if (!(grabTimer > 0))
                {
                    OnGrabFailed();
                }
            }
        }
    }

    public void FixedUpdate(float dt)
    {
        mouth.FixedUpdate(dt);
        depositDoor.FixedUpdate(dt);

        if (anchor.Update(dt))
        {
            onAnchorTargetReached?.Invoke();
        }
        if (arm.Update(ref claw, reversed))
        {
            onArmTargetReached?.Invoke();
        }
        switch (claw.Update())
        {
            case GrabberClaw.TaskResult.complete:
                onClawTargetReached?.Invoke();
                break;
            case GrabberClaw.TaskResult.failed:
                onClawTargetFailed?.Invoke();
                break;
        }
    }

    private void Deploy()
    {
        taskInProgress = true;

        Enable();

        //1) snap to folded pose and set springs to maintain pose
        arm.SnapToPose(foldedPose);
        arm.SetSpringTargets(foldedPose);
        arm.EnableSprings(true);

        claw.SnapClosed();
        claw.Close();//set spring

        //2) sync transforms and show sprites
        arm.SyncTransforms();
        claw.SyncTransforms();
        ShowSprites();

        //3) deploy
        mouth.Open();
        anchor.BeginTargetingDeployedPosition();

        onArmTargetReached = null;
        onAnchorTargetReached = GoIdle;
        onClawTargetReached = null;
        onClawTargetFailed = null;
        //let's set all 4 actions every time we start a new task (so we don't have any old callbacks coming in when they shouldn't)
    }

    //move to default pose
    private void GoIdle()
    {
        grabTimer = 0;
        taskInProgress = true;
        claw.Close();
        depositDoor.Close();
        arm.BeginTargetingPose(defaultPose);

        onArmTargetReached = CompleteGoIdle;
        onAnchorTargetReached = null;
        onClawTargetReached = null;
        onClawTargetFailed = null;
    }

    private void CompleteGoIdle()
    {
        //we'll continue tracking default pose in case collision knocks the arm out of place
        taskInProgress = false;

        onArmTargetReached = null;
        onAnchorTargetReached = null;
        onClawTargetReached = null;
        onClawTargetFailed = null;
    }

    //return to default pose
    private void ParkPhase1()
    {
        taskInProgress = true;
        arm.BeginTargetingPose(defaultPose);

        onArmTargetReached = ParkPhase2;
        onAnchorTargetReached = null;
        onClawTargetReached = null;
        onClawTargetFailed = null;
    }

    //close grabber and fold up
    private void ParkPhase2()
    {
        taskInProgress = true;
        claw.Close();

        onArmTargetReached = null;
        onAnchorTargetReached = null;
        onClawTargetReached = ParkPhase3;
        onClawTargetFailed = null;
    }

    private void ParkPhase3()
    {
        taskInProgress = true;
        arm.BeginTargetingPose(foldedPose);

        onArmTargetReached = ParkPhase4;
        onAnchorTargetReached = null;
        onClawTargetReached = null;
        onClawTargetFailed = null;
    }

    //back into the garage
    private void ParkPhase4()
    {
        taskInProgress = true;
        anchor.BeginTargetingOffPosition();

        onArmTargetReached = null;
        onAnchorTargetReached = CompletePark;
        onClawTargetReached = null;
        onClawTargetFailed = null;
    }

    private void CompletePark()
    {
        mouth.Close();
        HideSprites();
        Disable(true);
    }

    private void TryBeginGrab()
    {
        var grabCircle = new CircleGeometry()
        {
            center = arm.jointedChain.JointPosition(0),
            radius = grabAttemptRadius
        };
        var overlaps = world.OverlapGeometry(grabCircle, grabFilter);

        if (overlaps.Length > 0)
        {
            taskInProgress = true;
            var hitBody = overlaps[0].shape.body;
            grabTarget = hitBody;
            claw.Open();
            arm.BeginTargetingBody(hitBody);
            arm.EnableSprings(false);
            grabTimer = grabTimeOut;

            onArmTargetReached = CompleteGrab;
            onAnchorTargetReached = null;
            onClawTargetReached = null;
            onClawTargetFailed = null;
        }
        else
        {
            Debug.Log("No grabbable target in range.");//replace with ui message in future
        }
    }

    private void CompleteGrab()
    {
        grabTimer = 0;
        taskInProgress = true;
        if (grabTarget.isValid)
        {
            claw.BeginGrab(grabTarget);

            onArmTargetReached = null;
            onAnchorTargetReached = null;
            onClawTargetReached = HeadToDeposit;
            onClawTargetFailed = OnGrabFailed;
        }
        else
        {
            OnGrabFailed();
        }
    }

    private void HeadToDeposit()
    {
        if (grabTarget.isValid)
        {
            taskInProgress = true;
            arm.BeginTargetingPositionOnBody(depositTargetBody, depositTargetPosition);
            arm.EnableSprings(false);
            claw.BeginHold(grabTarget);
            depositDoor.Open();

            onArmTargetReached = Deposit;
            onAnchorTargetReached = null;
            onClawTargetReached = OnGrabFailed;
            onClawTargetFailed = null;
        }
        else
        {
            OnGrabFailed();
        }
    }

    private void Deposit()
    {
        taskInProgress = true;
        claw.Open();

        onArmTargetReached = null;
        onAnchorTargetReached = null;
        onClawTargetReached = CompleteDeposit;
        onClawTargetFailed = null;
    }

    private void CompleteDeposit()
    {
        if (grabTarget.isValid)
        {
            grabTarget = default;
            GoIdle();
        }
        else
        {
            OnGrabFailed();
        }
    }

    private void OnGrabFailed()
    {
        //open claw to make sure we let go of target, then go idle
        grabTarget = default;
        claw.Open();
        arm.BeginTargetingPose(defaultPose);

        onArmTargetReached = null;
        onAnchorTargetReached = null;
        onClawTargetReached = GoIdle;
        onClawTargetFailed = null;
    }
}