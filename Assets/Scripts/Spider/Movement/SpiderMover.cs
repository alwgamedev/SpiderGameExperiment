using UnityEngine;
using UnityEngine.Events;
using Unity.U2D.Physics;
using Unity.Collections;
using System;

[Serializable]
public class SpiderMover
{
    [SerializeField] bool drawGroundMapGizmos;
    [SerializeField] bool drawBodyGizmos;

    [Header("Parts")]
    [SerializeField] SpiderBody spiderBody;
    [SerializeField] SpiderBodyDefinition spiderBodyDef;
    [SerializeField] LegSynchronizer legSynch;
    [SerializeField] LegSynchSettings stdLegSettings;
    [SerializeField] LegSynchSettings freefallLegSettings;
    [SerializeField] LegSynchSettings thrustingLegSettings;
    [SerializeField] LegSynchSettings freeHangLegSettings;
    [SerializeField] Transform abdomenRoot;
    [SerializeField] Transform abdomenBone;
    [SerializeField] Transform headRoot;
    [SerializeField] Transform headBone;
    [SerializeField] Transform grappleArmTransform;
    [SerializeField] GrappleCannon grapple;
    [SerializeField] Thruster thruster;
    [SerializeField] ThrusterFlame thrusterFlame;

    [Header("Ground Data")]
    [SerializeField] float groundedExitToleranceFactor;
    [SerializeField] float groundedEntryToleranceFactor;
    [SerializeField] float groundDirectionSampleWidth;
    [SerializeField] float backupGroundPtRaycastLengthFactor;
    [SerializeField] GroundMap groundMap;
    [SerializeField] PhysicsQuery.QueryFilter shapeCaptureFilter;
    [SerializeField] ShapeCapture shapeCapture;//no trigger shapes

    [Header("Movement")]
    [SerializeField] float accelFactor;
    [SerializeField] float accelFactorFreeHanging;
    [SerializeField] float deadThrusterAccelFactor;
    [SerializeField] float thrustingAccelFactor;
    [SerializeField] float accelCap;
    [SerializeField] float maxSpeed;
    [SerializeField] float maxSpeedAirborne;
    [SerializeField] float settleTime;
    [SerializeField] float friction;
    [SerializeField] float frictionCap;

    [Header("Height Spring")]
    [SerializeField] float preferredRideHeight;
    [SerializeField] float heightSpringForce;
    [SerializeField] float heightSpringDamping;
    [SerializeField] float grappleSquatReduction;
    [SerializeField] float heightSampleMin;
    [SerializeField] float heightSampleMax;

    [Header("Balance & Rotation")]
    [SerializeField] float headRotationMinPos;
    [SerializeField] float headRotationMaxPos;
    [SerializeField] float balanceSpringForce;
    [SerializeField] float airborneBalanceSpringForce;
    [SerializeField] float balanceSpringDamping;
    [SerializeField] float airborneBalanceSpringDamping;
    [SerializeField] float grappleScurryResistanceMax;
    [SerializeField] float grappleScurryAngleMin;

    [Header("Jumping & Airborne")]
    [SerializeField] float jumpForce;
    [SerializeField] float jumpForceCrouchBoostRate;
    [SerializeField] float jumpAngleMin;
    [SerializeField] float jumpAngleLerpRate;
    [SerializeField] float jumpVerificationTime;
    [SerializeField] float tapJumpVerificationTime;
    [SerializeField] float crouchHeightFraction;
    [SerializeField] float crouchTime;
    [SerializeField] float crouchReleaseSpeedMultiplier;

    [Header("Free Hang")]
    [SerializeField] float freeHangHeadAngle;

    SpiderInput spiderInput;
    public Grabber grabber;

    bool chargingJump;
    bool waitingToHandleJump;
    float jumpVerificationTimer;
    float jumpAngleFraction;//0 - 1 (for angle going from 0 to jumpAngleMin)
    PhysicsRotate jumpRotateMin;
    PhysicsRotate scurryRotateMin;
    float crouchProgress;//0 - 1
    bool canFlip;
    bool canEngageThruster;

    bool grounded;
    Vector2 groundDirection;
    Vector2 balanceDirection;
    float settleTimer;

    Vector2[] legCastDirection;

    bool needChangeDirection;
    bool grappleScurrying;
    bool thrusterCooldownWarningSent;
    bool grappleFreeHangPrerequisites;

    bool FlipInput => spiderInput.FAction.IsPressed();
    bool Flipped => FlipInput && canFlip;
    bool JumpInput => spiderInput.SpaceAction.IsPressed();
    bool CancelJumpInput => spiderInput.ControlAction.IsPressed();
    float HorizontalMoveInput => spiderInput.MoveInput.x;
    float LeanInput => spiderInput.SecondaryInput.x;
    float EffectiveRideHeight => crouchProgress > 0 ? (1 - crouchProgress * crouchHeightFraction) * preferredRideHeight : preferredRideHeight;
    float GroundMapRaycastLength => (grounded ? groundedExitToleranceFactor : groundedEntryToleranceFactor) * EffectiveRideHeight;
    bool ForceFreeHang => grapple.GrappleStaticAnchored && spiderInput.ShiftAction.IsPressed();
    Vector2 Right => SpideyBody.LevelRight.direction;
    Vector2 Up => Right.CCWPerp();
    Vector2 OrientedGroundDirection => FacingRight ? groundDirection : -groundDirection;
    Vector2 OrientedRight => FacingRight ? Right : -Right;
    Vector2 OrientedHeadDirection => FacingRight ? Head.rotation.direction : -Head.rotation.direction;
    Vector2 HeightReferencePt => SpideyBody.HeightReferencePosition;
    float MaxSpeed => grounded ? maxSpeed : maxSpeedAirborne;
    float GrappleScurryResistance => Vector2.Dot(grapple.LastCarryForce, -OrientedGroundDirection);
    float GrappleScurryResistanceFraction => Mathf.Clamp(GrappleScurryResistance / grappleScurryResistanceMax, 0, 1);

    public bool FacingRight => SpideyBody.FacingRight;
    public bool ChargingJump => chargingJump;
    public float CrouchProgress => crouchProgress;
    public float TotalMass => SpideyBody.TotalMass + legSynch.settings.gravityScale * legSynch.TotalMass;
    public Thruster Thruster => thruster;
    public GrappleCannon Grapple => grapple;
    public ref SpiderBody SpideyBody => ref spiderBody;
    public PhysicsWorld World => SpideyBody.World;
    public ref PhysicsBody Abdomen => ref SpideyBody.abdomen;
    public ref PhysicsBody Head => ref SpideyBody.head;

    //mainly to hook up audio (later maybe also ui stuff)
    public UnityEvent jumpChargeBegan;
    public UnityEvent jumpChargeEnded;
    public UnityEvent jumped;
    public UnityEvent thrustersEngaged;
    public UnityEvent thrustersDisengaged;

#if UNITY_EDITOR
    public void CenterPhysicsBodies()
    {
        spiderBody.CenterRootTransforms(abdomenRoot, abdomenBone, headRoot, headBone, spiderBodyDef);
    }

    public void CreateLegPhysicsBodies(MonoBehaviour owner)
    {
        legSynch.CreatePhysicsTransforms(owner);
    }

    public void CenterLegPhysicsBodies()
    {
        legSynch.CenterPhysicsTransforms();
    }

    public void OnDrawGizmos()
    {
        if (Application.isPlaying && drawGroundMapGizmos)
        {
            groundMap.DrawGizmos();
        }

        if (drawBodyGizmos)
        {
            spiderBody.DrawGizmos(abdomenBone, headBone, grappleArmTransform, spiderBodyDef);
        }

        legSynch.OnDrawGizmos();
        grapple.OnDrawGizmos();
    }

    public void OnValidate()
    {
        if (Application.isPlaying)
        {
            spiderBody.OnValidate(spiderBodyDef);
            thruster.Initialize();
            grapple.OnValidate();
            if (legSynch.settings.stepStrength != null)
            {
                legSynch.UpdateSettings(in LegSettings());
            }
            legSynch.OnValidate();
        }
    }
#endif

    public void Initialize(Transform transform, SpiderInput spiderInput)
    {
        this.spiderInput = spiderInput;

        legCastDirection = new Vector2[2];
        scurryRotateMin = new PhysicsRotate() { direction = new Vector2(Mathf.Cos(grappleScurryAngleMin), Mathf.Sin(grappleScurryAngleMin)) };
        jumpRotateMin = new PhysicsRotate() { direction = new Vector2(Mathf.Cos(jumpAngleMin), Mathf.Sin(jumpAngleMin)) };

        shapeCapture.Initialize(2048);

        thruster.Initialize();
        thrusterFlame.Initialize();

        spiderBody.CreatePhysicsBody(new PhysicsRotate() { direction = transform.right }, abdomenRoot, headRoot, headBone,
            grappleArmTransform, spiderBodyDef);
        InitializeLegSynch();
        InitializeGroundMap();
        grapple.Initialize(spiderInput, World, SpideyBody.LevelRight, TotalMass, FacingRight);
    }

    public void Enable()
    {
        spiderBody.Enable();
        legSynch.Enable();
    }

    public void Disable()
    {
        spiderBody.Disable();
        legSynch.Disable();
    }

    public void OnDestroy()
    {
        groundMap.Dispose();
        grapple.OnDestroy();
        legSynch.Destroy();
        spiderBody.Destroy();
        shapeCapture.Dispose();
    }

    public void Update()
    {
        UpdateState();
        grapple.Update();
    }

    public void LateUpdate()
    {
        grapple.LateUpdate();
    }

    public void FixedUpdate()
    {
        float dt = Time.deltaTime;

        if (VerifyingJump())
        {
            jumpVerificationTimer -= dt;
        }

        //if jobs try to do something like apply force to a body,
        //and on main thread we access physics shape geometry, the game stalls out,
        //so complete jobs before updating the shape capture.
        groundMap.CompleteJobs();
        grapple.CompleteJobs();
        shapeCapture.Update(HeightReferencePt, World, shapeCaptureFilter);

        UpdateGroundData();
        RotateAbdomen();
        RotateHead();
        UpdateThruster();
        UpdateGrappleScurrying();

        if (needChangeDirection)
        {
            ChangeDirection();
            needChangeDirection = false;
        }

        var lyingOnBack = (Abdomen.GetContacts().Length > 0 || Head.GetContacts().Length > 0) && Up.y < 0;
        bool alignedWithGroundDir = Vector2.Dot(Right, groundDirection) > MathTools.sin30;
        if (canFlip && FlipInput && ((grounded && alignedWithGroundDir) || lyingOnBack))
        {
            //end flip if we come in contact with the ground
            canFlip = false;
            canEngageThruster = false;
        }
        else if (!canFlip && !FlipInput && !grounded && !lyingOnBack)
        {
            //re-enable flip once flip input released and we're eligible to flip (not in contact with ground)
            canFlip = true;
        }


        if (chargingJump)
        {
            jumpAngleFraction = Mathf.Clamp(jumpAngleFraction + LeanInput * jumpAngleLerpRate * dt, 0, 1);
            if (crouchProgress < 1)
            {
                UpdateCrouch(dt);
            }
        }
        else if (!waitingToHandleJump)
        {
            if (crouchProgress > 0)
            {
                UpdateCrouch(crouchReleaseSpeedMultiplier * -dt);
            }
            if (jumpAngleFraction != 0)
            {
                jumpAngleFraction = 0;
            }
        }

        HandleMoveInput();
        HandleJumpInput();

        if (grounded)
        {
            UpdateHeightSpring();
        }
        Balance();

        if (HorizontalMoveInput != 0 || !grounded)
        {
            settleTimer = settleTime;
        }
        else if (settleTimer > 0)
        {
            settleTimer -= dt;
        }

        grapple.FixedUpdate(dt, SpideyBody.LevelRight, Abdomen, shapeCapture.list.AsArray());
        UpdateLegSynch(dt);
    }


    //THRUSTER

    //do before you handle any move input
    private void UpdateThruster()
    {
        switch (thruster.FixedUpdate(Abdomen))
        {
            case Thruster.ThrustersUpdateResult.ChargeRanOut:
                OnThrusterRanOutOfCharge();
                if (!grounded && grapple.GrappleStaticAnchored)
                {
                    grappleFreeHangPrerequisites = true;
                    grapple.FreeHanging = true;
                }
                break;
            case Thruster.ThrustersUpdateResult.CooldownEnded:
                OnThrusterCooldownEnded();
                UpdateThrusterEngagement();
                break;
            case Thruster.ThrustersUpdateResult.None:
                UpdateThrusterEngagement();
                break;

        }

        thrusterFlame.Update(thruster.Engaged ? Abdomen.linearVelocity.magnitude : -1, Time.deltaTime);
    }

    private void UpdateThrusterEngagement()
    {
        bool engageThruster = canEngageThruster && HorizontalMoveInput != 0 && !ForceFreeHang;
        if (thruster.Engaged && !engageThruster)
        {
            DisengageThruster();
        }
        else if (engageThruster)
        {
            TryEngageThruster();
        }
    }

    private void TryEngageThruster()
    {
        if (thruster.Engage())
        {
            OnThrusterEngaged();
        }
        else
        {
            OnThrusterEngageFailed();
        }
    }

    private void DisengageThruster()
    {
        thruster.Disengage();
        OnThrusterDisengaged();
    }

    private void OnThrusterRanOutOfCharge()
    {
        //Debug.Log("thruster ran out of charge");
        OnThrusterDisengaged();
    }

    private void OnThrusterCooldownEnded()
    {
        //Debug.Log("thruster cooldown ended");
    }

    private void OnThrusterEngaged()
    {
        //rb.gravityScale = thrustingGravityScale;
        thrustersEngaged.Invoke();
    }

    private void OnThrusterEngageFailed()
    {
        if (!thrusterCooldownWarningSent)
        {
            //Debug.Log("thruster on cooldown...");
            thrusterCooldownWarningSent = true;
        }
    }

    private void OnThrusterDisengaged()
    {
        //Debug.Log("disengaging thruster.");
        thrustersDisengaged.Invoke();
    }


    //INPUT

    private void UpdateState()
    {
        if (chargingJump)
        {
            if (!grounded)
            {
                chargingJump = false;
                jumpChargeEnded.Invoke();
            }
            else if (!JumpInput)
            {
                chargingJump = false;
                waitingToHandleJump = !CancelJumpInput;
                jumpChargeEnded.Invoke();
            }
        }
        else if (!waitingToHandleJump)
        {
            if (grounded && JumpInput)
            {
                chargingJump = true;
                jumpChargeBegan.Invoke();
            }
        }

        grapple.aimInput = chargingJump ? 0 : LeanInput;
        UpdateGrappleScurrying();//needs to be updated before changing direction

        //update freeHanging (needs to be done before changing direction)
        if (grapple.GrappleStaticAnchored)
        {
            //hold shift to enter freehang/swing mode (if it's the default state, then repelling and direction change gets annoying)
            grappleFreeHangPrerequisites = HorizontalMoveInput == 0 || ForceFreeHang || thruster.Cooldown;
        }
        else if (grappleFreeHangPrerequisites)
        {
            grappleFreeHangPrerequisites = false;
        }

        if (MathTools.OppositeSigns(HorizontalMoveInput, SpideyBody.Orientation))
        {
            needChangeDirection = true;
        }

        if (thrusterCooldownWarningSent && HorizontalMoveInput == 0)
        {
            thrusterCooldownWarningSent = false;
            //so that you will only get the cooldown warning when you first press input or right after thrusters ran out of charge if you enter cooldown while holding input
        }
    }

    //pass negative dt when reversing crouch
    private void UpdateCrouch(float dt)
    {
        var progressDelta = dt / crouchTime;
        crouchProgress = Mathf.Clamp(crouchProgress + progressDelta, 0, 1);
    }

    private void UpdateGrappleScurrying()
    {
        grappleScurrying = grounded && HorizontalMoveInput != 0 && grapple.GrappleAnchored;
    }

    private void ChangeDirection()
    {
        PhysicsTransform reflection;
        if (!grapple.FreeHanging)
        {
            reflection = SpideyBody.VirtualTransform;
        }
        else
        {
            var u = SpideyBody.LevelRight.direction.CCWPerp();
            reflection = new PhysicsTransform(grapple.FreeHangLeveragePoint, new PhysicsRotate() { direction = u });
        }

        SpideyBody.ChangeDirection(reflection, abdomenBone, headBone, grappleArmTransform,
            spiderBodyDef.grappleArmBoxOffset, spiderBodyDef.grappleArmBoxSize);
        var overlapCorrection = SpideyBody.ResolveOverlaps();
        if (!grounded)
        {
            //if not grounded and changing direction will make you become grounded (e.g. when freehanging and change direction near an obstacle),
            //then make sure that you "become grounded" at an appropriate ride height -- helps prevent legs tunneling)
            var d = -EffectiveRideHeight * Up;
            var cast = World.CastRay(HeightReferencePt, d, groundMap.filter);
            if (cast.Length > 0)
            {
                var cor = cast[0].point - HeightReferencePt - d;
                SpideyBody.ApplyTranslation(cor);
                overlapCorrection += cor;
                overlapCorrection += SpideyBody.ResolveOverlaps();
            }
        }

        grabber.OnDirectionChanged(reflection, overlapCorrection, !FacingRight);
        legSynch.OnDirectionChanged(reflection, overlapCorrection, !FacingRight);
        grapple.SetOrientation(FacingRight);
    }

    private void HandleMoveInput()
    {
        if (HorizontalMoveInput != 0)
        {
            if (grapple.FreeHanging)
            {
                Abdomen.ApplyForce(TotalMass * accelFactorFreeHanging * FreeHangingMoveDirection(), grapple.FreeHangLeveragePoint);
            }
            else
            {
                if (grounded)
                {
                    var accFactor = TotalMass * accelFactor;
                    var headRel = SpideyBody.LevelRight.InverseMultiplyRotation(Head.rotation).direction;
                    var t = headRel.x < 0 ? 1 : Mathf.Abs(headRel.y);
                    MoveBody(Head, OrientedHeadDirection, MaxSpeed, t * accFactor, accelCap, 0);
                    MoveBody(Abdomen, OrientedRight, MaxSpeed, accFactor, accelCap, Mathf.NegativeInfinity);
                }
                else
                {
                    var dir = Flipped ? -OrientedGroundDirection : OrientedGroundDirection;
                    var accFactor = TotalMass * (thruster.Engaged ? thrustingAccelFactor : deadThrusterAccelFactor * Mathf.Clamp(1 - dir.y, 0, 1));
                    MoveBody(Abdomen, dir, MaxSpeed, accFactor, accelCap, 0);
                }
            }
        }

        //apply friction aka tofurction
        if (grounded)
        {
            var d = groundDirection;
            var vel = Vector2.Dot(Abdomen.linearVelocity, d);
            var c = Mathf.Min(friction, frictionCap * Mathf.Abs(vel) * MathTools.fixedDtInverse);
            var f = TotalMass * -Mathf.Sign(vel) * c * d;
            Abdomen.ApplyForceToCenter(f);
        }

        static void MoveBody(PhysicsBody body, Vector2 direction, float maxSpd, float accFactor, float accCap, float sMin)
        {
            var spd = Vector2.Dot(body.linearVelocity, direction);
            var s = Mathf.Min(maxSpd - spd, accCap);

            if (s > sMin)
            {
                body.ApplyForceToCenter(accFactor * s * direction);
            }
        }
    }

    private Vector2 FreeHangingMoveDirection()
    {
        return FacingRight ? grapple.GrappleExtent.normalized.CWPerp() : grapple.GrappleExtent.normalized.CCWPerp();
    }


    //BALANCE & ROTATION

    private void Balance()
    {
        var f = -(grounded ? balanceSpringDamping : airborneBalanceSpringDamping) * Abdomen.angularVelocity;

        if (!grapple.FreeHanging)
        {
            var a = Flipped ? FlippedBalanceAngle() : BalanceAngle();
            f += a * (grounded ? balanceSpringForce : airborneBalanceSpringForce);
        }

        Abdomen.ApplyTorque(TotalMass * f);
    }

    private float BalanceAngle()
    {
        return MathTools.PseudoAngle(SpideyBody.LevelRight, balanceDirection);
    }

    private float FlippedBalanceAngle()
    {
        //always flip "backwards," so it's predictable and you can use the flip to adjust rotation for landing on walls
        var angle = MathTools.PseudoAngle(SpideyBody.LevelRight, -balanceDirection);
        var dot = Vector2.Dot(SpideyBody.LevelRight, -balanceDirection);
        if (dot < 0)
        {
            return SpideyBody.Orientation * Mathf.Abs(angle);
        }

        return angle;
    }

    private void RotateAbdomen()
    {
        if (chargingJump)
        {
            SpideyBody.abdomenRotationFromBase = JumpAbdomenRotation();
        }
        else if (grappleScurrying)
        {
            SpideyBody.abdomenRotationFromBase = ScurryAbdomenRotation();
        }
        else if (SpideyBody.abdomenRotationFromBase.direction != Vector2.right)
        {
            SpideyBody.abdomenRotationFromBase = PhysicsRotate.identity;
        }
    }

    private PhysicsRotate JumpAbdomenRotation()
    {
        return jumpAngleFraction > 0 ?
            PhysicsRotate.LerpRotation(PhysicsRotate.identity, jumpRotateMin, jumpAngleFraction)
            : PhysicsRotate.identity;
    }

    private PhysicsRotate ScurryAbdomenRotation()
    {
        var f = GrappleScurryResistanceFraction;
        if (f > 0)
        {
            return PhysicsRotate.LerpRotation(PhysicsRotate.identity, scurryRotateMin, f);
        }
        return PhysicsRotate.identity;
    }

    private void RotateHead()
    {
        Vector2 g;
        if (grounded)
        {
            var p0 = Head.position;
            var headUp = Head.rotation.direction.CCWPerp();
            var (i, t) = groundMap.LineCastOrClosest(p0, headUp);
            Vector2 q = groundMap.PointFromReducedPosition(i, t);
            if (Vector2.Dot(q - p0, headUp) > 0)//happens when you change direction with steeply angled head
            {
                (i, t) = groundMap.LineCastOrClosest(p0, -Up);
            }

            var tMin = FacingRight ? t + headRotationMinPos : t - headRotationMaxPos;
            var tMax = tMin + headRotationMaxPos - headRotationMinPos;
            Vector2 n = groundMap.AverageNormal(i, tMin, tMax);
            g = n.CWPerp();
        }
        else
        {
            g = grapple.FreeHanging ? FreeHangingHeadRight(Right) : Right;
        }

        SpideyBody.SetHeadRotation(new PhysicsRotate() { direction = g });
    }

    private Vector2 FreeHangingHeadRight(Vector2 bodyRight)
    {
        var y = bodyRight.y;
        if (y < 0)
        {
            y = -y * freeHangHeadAngle;
            return Mathf.Cos(y) * bodyRight + Mathf.Sin(y) * (FacingRight ? bodyRight.CCWPerp() : bodyRight.CWPerp());
        }
        return bodyRight;
    }


    //JUMPING

    //not checking anything here, bc i have it set up so it only collects jump input when you are able to jump
    //(i.e. when grounded and not verifying jump)
    private void HandleJumpInput()
    {
        if (waitingToHandleJump)
        {
            waitingToHandleJump = false;
            jumpVerificationTimer = jumpVerificationTime;
            SetGrounded(false);
            var jumpDir = JumpDirection();
            Abdomen.ApplyLinearImpulse(TotalMass * JumpForce() * jumpDir, HeightReferencePt);
            jumped.Invoke();
        }
    }

    private float JumpForce()
    {
        if (crouchProgress * crouchTime < tapJumpVerificationTime)
        {
            return jumpForce;
        }
        return jumpForce + crouchProgress * jumpForceCrouchBoostRate;
    }

    private Vector2 JumpDirection()
    {
        return SpideyBody.FacingRight ?
            SpideyBody.AbdomenBaseRotationFromLevel.InverseMultiplyRotation(Abdomen.rotation).direction.CCWPerp()
            : SpideyBody.AbdomenBaseRotationFromLevel.MultiplyRotation(Abdomen.rotation).direction.CCWPerp();
    }

    private bool VerifyingJump()
    {
        return jumpVerificationTimer > 0;
    }


    //HEIGHT SPRING

    private void UpdateHeightSpring()
    {
        var p0 = HeightReferencePt;
        var (i, t) = groundMap.LineCastOrClosest(HeightReferencePt, -Up);
        var tMin = FacingRight ? t + heightSampleMin : t - heightSampleMax;
        var tMax = tMin + heightSampleMax - heightSampleMin;
        Vector2 p = groundMap.AveragePoint(i, tMin, tMax);

        var down = groundDirection.CWPerp();//not the right direction, but we don't want to compete with friction
        var v = Vector2.Dot(Abdomen.GetWorldPointVelocity(p0), down);
        var d = p - p0;
        var l = d.magnitude;
        var f = (l - EffectiveRideHeight) * heightSpringForce;
        if (grapple.GrappleAnchored)
        {
            var dot = Vector2.Dot(grapple.LastCarryForce, down);
            if (down.y < 0 && dot < 0 && l > EffectiveRideHeight && grapple.GrappleReleaseInput < 0)
            {
                //allow the grapple to pull you away from ground, except when you're clinging upside down 
                //(so you don't fall unintentionally from rope bobbling)
                return;
            }
            else if (dot > 0 && l < 0)
            {
                f -= grappleSquatReduction * dot;
                //fight grapple a little when it's pulling you into the ground 
                // (i.e. reduce grappleCarryForce in direction of height spring)
            }
        }

        Abdomen.ApplyForceToCenter(TotalMass * (f - heightSpringDamping * v) * down);
    }


    //GROUND DETECTION

    private void SetGrounded(bool val)
    {
        if (grounded == val) return;
        grounded = val;
        if (grounded)
        {
            OnLanding();
        }
        else
        {
            OnTakeOff();
        }
    }

    private void OnLanding()
    {
        SetGravityScale(0);
        canEngageThruster = false;
    }

    private void OnTakeOff()
    {
        if (chargingJump)
        {
            chargingJump = false;
            jumpChargeEnded.Invoke();
        }

        SetGravityScale(1);
        canEngageThruster = true;
    }

    private void SetGravityScale(float val)
    {
        Abdomen.gravityScale = val;
        Head.gravityScale = val;
    }

    private void UpdateGroundData()
    {

        UpdateGroundMap();

        if (!VerifyingJump())
        {
            SetGrounded(legSynch.AnyLegGrounded);
            grapple.FreeHanging = grappleFreeHangPrerequisites && !grounded;
        }

        var i = groundMap.FirstGroundHitFromCenter(FacingRight);
        if (i < 0 || !(i < groundMap.NumPoints))
        {
            i = groundMap.CentralIndex;
        }

        groundDirection = grounded ?
            groundMap.AverageNormal(i, -groundDirectionSampleWidth, groundDirectionSampleWidth).CWPerp()
            : Vector2.right;

        if (HorizontalMoveInput != 0 || !grounded || (grapple.GrappleAnchored && grapple.GrappleReleaseInput < 0))
        {
            balanceDirection = groundDirection;
        }
        else if (settleTimer > 0)//fix the balance direction once you come to a stop so you don't quiver
        {
            var t = settleTimer / settleTime;
            balanceDirection = MathTools.CheapRotationalLerpClamped(balanceDirection, groundDirection, 1 - 0.5f * t, out _);
        }
    }

    private void UpdateGroundMap()
    {
        groundMap.UpdateMap(World, HeightReferencePt, Up, GroundMapRaycastLength, shapeCapture.list.AsArray());
    }

    private void InitializeGroundMap()
    {
        groundMap.Initialize(HeightReferencePt, Right, GroundMapRaycastLength);
        UpdateGroundMap();
    }


    //LEGS

    LegState legState;

    enum LegState
    {
        std, freefall, thrusting, freeHang
    }

    ref LegSynchSettings LegSettings(LegState state)
    {
        switch (state)
        {
            case LegState.freefall:
                return ref freefallLegSettings;
            case LegState.thrusting:
                return ref thrustingLegSettings;
            case LegState.freeHang:
                return ref freeHangLegSettings;
            default:
                return ref stdLegSettings;
        }
    }

    ref LegSynchSettings LegSettings()
    {
        LegState state = grounded ? LegState.std
            : thruster.Engaged ? LegState.thrusting
            : grapple.FreeHanging ? LegState.freeHang
            : LegState.freefall;

        return ref LegSettings(state);
    }

    private void UpdateLegSynch(float dt)
    {
        LegState state = grounded ? LegState.std
            : thruster.Engaged ? LegState.thrusting
            : grapple.FreeHanging ? LegState.freeHang
            : LegState.freefall;

        ref var settings = ref LegSettings(state);

        if (state != legState)
        {
            legState = state;
            legSynch.UpdateSettings(in settings);
        }

        legSynch.settings.stepHeightFraction = settings.stepHeightFraction * (1 - crouchProgress * crouchHeightFraction);

        legCastDirection[0] = SpideyBody.head.rotation.direction.CWPerp();
        legCastDirection[1] = SpideyBody.LevelRight.direction.CWPerp();

        legSynch.UpdateAllLegs(dt, groundMap, legCastDirection, FacingRight);
    }

    private void InitializeLegSynch()
    {
        var anchorBody = new NativeArray<PhysicsBody>(8, Allocator.Temp);
        for (int i = 0; i < 4; i++)
        {
            anchorBody[i] = SpideyBody.head;
            anchorBody[4 + i] = SpideyBody.abdomen;
        }

        legSynch.Initialize(anchorBody);
        legSynch.settings = stdLegSettings.Clone();
        legState = LegState.std;
    }
}