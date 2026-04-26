using System;
using Unity.U2D.Physics;
using UnityEngine;

[Serializable]
public class SGrabber
{
    [Header("Arm")]
    [SerializeField] JointedChainDefinition armDef;
    [SerializeField] JointedChainSettings armSettings;//might want to have angle limits for joint 0
    [SerializeField] SGrabberArm arm;
    [SerializeField] Transform[] armPhysTransforms;//centered between joint positions
    [SerializeField] Transform[] armNodes;//joint positions + effector
    [SerializeField] float[] foldedPose;//poses are spring target angles for the arm joints
    [SerializeField] float[] defaultPose;
    [SerializeField] float[] depositPose;

    [Header("Claw")]
    [SerializeField] GrabberClawDefinition clawDef;
    [SerializeField] SGrabberClaw claw;
    [SerializeField] Transform upperClawPhysTransform;
    [SerializeField] Transform upperClawBone;
    [SerializeField] PolygonPhysicsShape upperClawShape;
    [SerializeField] Transform lowerClawPhysTransform;
    [SerializeField] Transform lowerClawBone;
    [SerializeField] PolygonPhysicsShape lowerClawShape;

    [Header("Auxiliary Parts")]
    [SerializeField] SGrabberAnchor anchor;
    [SerializeField] SDoubleDoor depositDoor;
    [SerializeField] SDoubleDoor mouth;
    [SerializeField] SpriteRenderer[] sprite;
    [SerializeField] Transform depositTarget;
    [SerializeField] Transform offAnchor;
    [SerializeField] Transform deployedAnchorPosition;

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

    //2do:
    //0) integrate into main spider script - DONE (?)
    //0.5f) initialize methods for this and all components - DONE (?)
    //0.75f) script extras:
        //editor scripts?
        //+validate and gizmo methods
    //1) set up and test
    //2) get direction change working (and components will need to know if reversed)
    //3) add a mask or masks to mask the sprites as they're retreating into body (only activate once arm has made it back to folded position, so they never block while arm is out)

    public void OnValidate()
    {

    }

    public void OnDrawGizmos()
    {

    }


    //LIFETIME

    //anchor body will be head of spider
    public void Initialize(SpiderInput input, PhysicsBody anchorBody, PhysicsBody depositTargetBody)
    {
        this.input = input;
        world = anchorBody.world;
        this.depositTargetBody = depositTargetBody;
        depositTargetPosition = depositTargetBody.transform.InverseTransformPoint(depositTarget.position);

        arm.Initialize(armPhysTransforms, armNodes, anchorBody, armDef, armSettings);
        anchor.Initialize(arm.jointedChain.joint[0], offAnchor.position, depositTarget.position);
        claw.Initialize(arm.jointedChain.body[^1], clawDef,
            upperClawPhysTransform, upperClawBone.position, upperClawShape.subdividedPolygon,
            lowerClawPhysTransform, lowerClawBone.position, lowerClawShape.subdividedPolygon);

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
        if (arm.Update(reversed))
        {
            onArmTargetReached?.Invoke();
        }
        if (claw.Update())
        {
            onClawTargetReached?.Invoke();
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
        //let's set all 3 every time we start a new action (so we don't have any old unneeded callbacks coming in when they shouldn't)
    }

    //move to default pose
    private void GoIdle()
    {
        taskInProgress = true;
        claw.Close();
        depositDoor.Close();
        arm.BeginTargetingPose(defaultPose);

        onArmTargetReached = CompleteGoIdle;
        onAnchorTargetReached = null;
        onClawTargetReached = null;
    }

    private void CompleteGoIdle()
    {
        //we'll continue tracking default pose in case collision knocks the arm out of place
        taskInProgress = false;

        onArmTargetReached = null;
        onAnchorTargetReached = null;
        onClawTargetReached = null;
    }

    //return to default pose
    private void ParkPhase1()
    {
        taskInProgress = true;
        arm.BeginTargetingPose(defaultPose);

        onArmTargetReached = ParkPhase2;
        onAnchorTargetReached = null;
        onClawTargetReached = null;
    }

    //close grabber and fold up
    private void ParkPhase2()
    {
        taskInProgress = true;
        claw.Close();

        onArmTargetReached = null;
        onAnchorTargetReached = null;
        onClawTargetReached = ParkPhase3;
    }

    private void ParkPhase3()
    {
        taskInProgress = true;
        arm.BeginTargetingPose(foldedPose);

        onArmTargetReached = ParkPhase4;
        onAnchorTargetReached = null;
        onClawTargetReached = null;
    }

    //back into the garage
    private void ParkPhase4()
    {
        taskInProgress = true;
        anchor.BeginTargetingOffPosition();

        onArmTargetReached = null;
        onAnchorTargetReached = CompletePark;
        onClawTargetReached = null;
    }

    private void CompletePark()
    {
        mouth.Close();
        HideSprites();
        Disable(true);
    }

    private void TryBeginGrab()
    {
        if (taskInProgress)
        {
            return;
        }

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
            arm.BeginTargetingBody(hitBody, hitBody.localCenterOfMass);
            arm.EnableSprings(false);
            grabTimer = grabTimeOut;

            onArmTargetReached = CompleteGrab;
            onAnchorTargetReached = null;
            onClawTargetReached = null;
        }
        else
        {
            Debug.Log("No grabbable target in range.");//replace with ui message in future
        }
    }

    private void CompleteGrab()
    {
        taskInProgress = true;
        grabTimer = 0;
        if (grabTarget.isValid)
        {
            claw.BeginGrab(grabTarget);

            onArmTargetReached = null;
            onAnchorTargetReached = null;
            onClawTargetReached = HeadToDeposit;
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
            arm.BeginTargetingBody(depositTargetBody, depositTargetPosition);
            arm.EnableSprings(true);
            arm.SetSpringTargets(depositPose);
            claw.BeginHold(grabTarget);
            depositDoor.Open();

            onArmTargetReached = Deposit;
            onAnchorTargetReached = null;
            onClawTargetReached = OnGrabFailed;
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
        grabTarget = default;
        GoIdle();
    }
}