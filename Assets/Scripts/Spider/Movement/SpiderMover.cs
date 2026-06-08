using UnityEngine;
using UnityEngine.Events;
using Unity.U2D.Physics;
using Unity.Collections;
using System;
using UnityEngine.UIElements;

[Serializable]
public class SpiderMover
{
    [SerializeField] bool drawGroundMapGizmos;
    [SerializeField] bool drawBodyGizmos;

    [Header("Parts")]
    [SerializeField] SpiderBody spiderBody;
    [SerializeField] LegSynchronizer legSynch;
    [SerializeField] LegSynchSettings stdLegSettings;
    [SerializeField] LegSynchSettings freefallLegSettings;
    [SerializeField] LegSynchSettings thrustingLegSettings;
    [SerializeField] LegSynchSettings freeHangLegSettings;
    // [SerializeField] Transform grappleArm;
    [SerializeField] Transform abdomenRoot;
    [SerializeField] Transform abdomenBone;
    [SerializeField] Transform headRoot;
    [SerializeField] Transform headBone;
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
    [SerializeField] float gripStrength;
    [SerializeField] float gripDamping;
    [SerializeField] float maxSpeed;
    [SerializeField] float maxSpeedAirborne;
    [SerializeField] float settleTime;

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

    bool grounded;
    Vector2 groundDirection;
    Vector2 groundAnchorPt;
    Vector2 balanceDirection;
    float settleTimer;

    Vector2[] legCastDirection;

    bool needChangeDirection;
    bool grappleScurrying;
    bool thrusterCooldownWarningSent;
    bool grappleFreeHangPrerequisites;

    bool FlipInput => spiderInput.FAction.IsPressed();
    bool JumpInput => spiderInput.SpaceAction.IsPressed();
    bool CancelJumpInput => spiderInput.ControlAction.IsPressed();
    float HorizontalMoveInput => spiderInput.MoveInput.x;
    float LeanInput => spiderInput.SecondaryInput.x;
    float EffectiveRideHeight => crouchProgress > 0 ? (1 - crouchProgress * crouchHeightFraction) * preferredRideHeight : preferredRideHeight;
    float GroundMapRaycastLength => (grounded ? groundedExitToleranceFactor : groundedEntryToleranceFactor) * EffectiveRideHeight;
    bool ForceFreeHang => grapple.GrappleAnchored && spiderInput.ShiftAction.IsPressed();
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
    public PhysicsWorld World => SpideyBody.world;
    public PhysicsBody Abdomen => SpideyBody.abdomen;
    public PhysicsBody Head => SpideyBody.head;

    //mainly to hook up audio (later maybe also ui stuff)
    public UnityEvent jumpChargeBegan;
    public UnityEvent jumpChargeEnded;
    public UnityEvent jumped;
    public UnityEvent thrustersEngaged;
    public UnityEvent thrustersDisengaged;

#if UNITY_EDITOR
    public void CenterPhysicsBodies()
    {
        spiderBody.CenterRootTransforms(abdomenRoot, abdomenBone, headRoot, headBone);
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
            //Gizmos.color = Color.yellow;
            //Gizmos.DrawSphere(groundAnchorPt, 0.1f);
            //Gizmos.DrawLine(HeightReferencePt, groundAnchorPt);
        }

        if (drawBodyGizmos)
        {
            spiderBody.DrawGizmos(abdomenBone, headBone);
        }

        legSynch.OnDrawGizmos();
        grapple.OnDrawGizmos();
    }

    public void OnValidate()
    {
        if (Application.isPlaying)
        {
            spiderBody.OnValidate();
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

        spiderBody.CreatePhysicsBody(new PhysicsRotate() { direction = transform.right }, abdomenRoot, headRoot, headBone);
        InitializeLegSynch();
        InitializeGroundData();
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
        if (grapple.JobsComplete())
        {
            grapple.CompleteJobs();
            shapeCapture.Update(HeightReferencePt, World, shapeCaptureFilter);
        }

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

        bool lyingOnBack = SpideyBody.HasContact() && Up.y < MathTools.sin30;
        bool alignedWithGroundDir = Mathf.Abs(MathTools.Cross2D(groundDirection, Right)) < MathTools.sin30;
        if (canFlip && FlipInput && ((grounded && alignedWithGroundDir) || lyingOnBack))//end flip
        {
            canFlip = false;
        }
        else if (!canFlip && !FlipInput && !grounded && !lyingOnBack)//re-enable flip once flip input released and we're eligible to flip (not in contact with ground)
        {
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
                if (!grounded && grapple.GrappleAnchored)
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
        if (thruster.Engaged)
        {
            if (grounded || grapple.FreeHanging || HorizontalMoveInput == 0)
            {
                DisengageThruster();
            }
        }
        else if (!grounded && HorizontalMoveInput != 0 && !ForceFreeHang)
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
        if (grapple.GrappleAnchored)
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
            var p = HeightReferencePt;
            var u = SpideyBody.LevelRight.direction.CCWPerp();
            reflection = new PhysicsTransform(grapple.FreeHangLeveragePoint, new PhysicsRotate() { direction = u });
        }

        SpideyBody.ChangeDirection(reflection, abdomenBone, headBone);
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
        //accelCap bc otherwise if velocity along movement direction is highly negative, we get ungodly rates of acceleration
        //(and note that we are doing it in a way that scales with max speed)
        if (HorizontalMoveInput != 0)
        {
            if (grapple.FreeHanging)
            {
                Abdomen.ApplyForce(TotalMass * (accelFactorFreeHanging * FreeHangingMoveDirection()), grapple.FreeHangLeveragePoint);
            }
            else
            {
                if (grounded)
                {
                    var accFactor = TotalMass * accelFactor;
                    var headRel = SpideyBody.LevelRight.InverseMultiplyRotation(Head.rotation).direction;
                    var t = headRel.x < 0 ? 1 : Mathf.Abs(headRel.y);
                    MoveBody(Head, OrientedHeadDirection, MaxSpeed, t * accFactor, accelCap, 0, out var sHead);
                    MoveBody(Abdomen, OrientedRight, MaxSpeed, /*(1 - t) **/ accFactor, accelCap, Mathf.NegativeInfinity, out var sAbdomen);
                }
                else
                {
                    var dir = FlipInput && canFlip ? -OrientedGroundDirection : OrientedGroundDirection;
                    var accFactor = TotalMass * (thruster.Engaged ? thrustingAccelFactor : deadThrusterAccelFactor * Mathf.Clamp(1 - dir.y, 0, 1));
                    MoveBody(Abdomen, dir, MaxSpeed, accFactor, accelCap, 0, out _);
                }
            }
        }
        //apply grip and drag
        else if (grounded)
        {
            var d = OrientedRight;
            var vel = Vector2.Dot(Abdomen.linearVelocity, d);
            var l = Vector2.Dot(groundAnchorPt - HeightReferencePt, d);
            var grip = gripStrength * l - gripDamping * vel;
            Abdomen.ApplyForceToCenter(TotalMass * grip * d);//grip to steep slope
        }

        static void MoveBody(PhysicsBody body, Vector2 direction, float maxSpd, float accFactor, float accCap, float sMin, out float s)
        {
            var spd = Vector2.Dot(body.linearVelocity, direction);
            s = Mathf.Min(maxSpd - spd, accCap * maxSpd);

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
            var a = MathTools.PseudoAngle(SpideyBody.LevelRight, FlipInput && canFlip ? -balanceDirection : balanceDirection);
            f += a * (grounded ? balanceSpringForce : airborneBalanceSpringForce);
        }

        Abdomen.ApplyTorque(TotalMass * f);
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
        return jumpAngleFraction > 0 ? PhysicsRotate.LerpRotation(PhysicsRotate.identity, jumpRotateMin, jumpAngleFraction) : PhysicsRotate.identity;
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
            var (i, t) = groundMap.LineCastOrClosest(p0, Head.rotation.direction.CCWPerp());
            var tMin = FacingRight ? t + headRotationMinPos : t - headRotationMaxPos;
            var tMax = tMin + headRotationMaxPos - headRotationMinPos;
            Vector2 n = groundMap.AverageNormal(i, tMin, tMax);
            g = n.CWPerp();

            //Vector2 p = groundMap.PointFromReducedPosition(i, t);
            //var d = p0 - p;
            //var headHeight = d.magnitude;
            //var minHeight = headHeightMinFrac * EffectiveRideHeight;
            //if (headHeight < minHeight)
            //{
            //    //may want to add damping
            //    Head.ApplyForceToCenter(headHeightSpringForce * (minHeight / headHeight - 1) * d);
            //}
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
        Vector2 down = -Up;
        var (i, t) = groundMap.LineCastOrClosest(HeightReferencePt, down);
        var tMin = FacingRight ? t + heightSampleMin : t - heightSampleMax;
        var tMax = tMin + heightSampleMax - heightSampleMin;
        Vector2 p = groundMap.AveragePoint(i, tMin, tMax);

        var v = Vector2.Dot(Abdomen.GetWorldPointVelocity(p0), down);
        var d = p - p0;
        var l = d.magnitude;
        down = d / l;
        var f = (l - EffectiveRideHeight) * heightSpringForce;
        if (grapple.GrappleAnchored)
        {
            var dot = Vector2.Dot(grapple.LastCarryForce, down);
            if (down.y < 0 && dot < 0 && l > 0 && grapple.GrappleReleaseInput < 0)
            //allow the grapple to pull you away from ground, except when you're clinging upside down (so you don't fall unintentionally from rope bobbling)
            {
                return;
            }
            else if (dot > 0 && l < 0)
            {
                f -= grappleSquatReduction * dot;//fight grapple a little when it's pulling you into the ground (i.e. reduce grappleCarryForce in direction of height spring)
            }
        }

        Abdomen.ApplyForce(TotalMass * (f - heightSpringDamping * v - Vector2.Dot(World.gravity, down)) * down, p0);
        //remove affect of gravity while height spring engaged, otherwise you will settle at a height which is off by -Vector2.Dot(gravity, down) / heightSpringForce
        //(meaning you will be under height when upright, and over height when upside down (which was causing feet to not reach ground while upside down))
        //(e.g. before ride height on flat ground was always off by around +- 32/400 = 0.08)
    }


    //GROUND DETECTION

    private void SetGrounded(bool val)
    {
        if (grounded == val) return;
        grounded = val;
        if (!grounded)
        {
            OnTakeOff();
        }
    }

    private void OnTakeOff()
    {
        if (chargingJump)
        {
            chargingJump = false;
            jumpChargeEnded.Invoke();
        }
    }

    private void UpdateGroundData()
    {
        UpdateGroundMap();
        var i = groundMap.FirstGroundHitFromCenter(FacingRight);
        if (i < groundMap.EndLeft || i > groundMap.EndRight)
        {
            i = groundMap.CentralIndex;
        }


        groundDirection = grounded ?
            groundMap.AverageNormal(i, -groundDirectionSampleWidth, groundDirectionSampleWidth).CWPerp()
            : Vector2.right;

        if (HorizontalMoveInput != 0 || !grounded || (grapple.GrappleAnchored && grapple.GrappleReleaseInput < 0))
        {
            groundAnchorPt = GetGroundAnchorPoint(i);
            balanceDirection = groundDirection;
        }
        else if (settleTimer > 0)//fixing the ground pt once "settled" stops us from sliding
        {
            var t = settleTimer / settleTime;
            balanceDirection = MathTools.CheapRotationalLerpClamped(balanceDirection, groundDirection, 1 - 0.5f * t, out _);
            var p = Vector2.Lerp(groundAnchorPt, GetGroundAnchorPoint(i), t);
            var (j, s) = groundMap.ClosestPoint(p);
            groundAnchorPt = groundMap.PointFromReducedPosition(j, s);
        }

        if (!VerifyingJump())
        {
            SetGrounded(legSynch.AnyLegGrounded());
            grapple.FreeHanging = grappleFreeHangPrerequisites && !grounded;
        }

        Vector2 GetGroundAnchorPoint(int i)
        {
            if (groundMap.HitGround(i) && i != groundMap.CentralIndex)
            {
                var (j, s) = groundMap.LineCastOrClosest(HeightReferencePt, -groundMap.Normal(i));
                return groundMap.PointFromReducedPosition(j, s);
            }

            return groundMap.Point(groundMap.CentralIndex);
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

    private void InitializeGroundData()
    {
        InitializeGroundMap();
        groundAnchorPt = groundMap.Point(groundMap.CentralIndex);
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