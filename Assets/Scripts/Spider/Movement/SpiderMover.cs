using UnityEngine;
using UnityEngine.Events;
using Unity.U2D.Physics;


public class SpiderMover : MonoBehaviour
{
    [SerializeField] float timeScale;
    [SerializeField] bool drawGizmos;

    [Header("Body Parts")]
    [SerializeField] Transform abdomenBone;
    [SerializeField] Transform abdomenBonePivot;
    [SerializeField] Transform headBone;
    [SerializeField] Transform headBoneHeightRefPoint;
    [SerializeField] GrappleCannon grapple;

    [Header("Ground Data")]
    [SerializeField] float groundedExitToleranceFactor;
    [SerializeField] float groundedEntryToleranceFactor;
    [SerializeField] float groundednessSmoothingRate;
    [SerializeField] float groundednessInitialContactValue;
    [SerializeField] float groundDirectionSampleWidth;
    [SerializeField] float backupGroundPtRaycastLengthFactor;
    [SerializeField] GroundMap groundMap;

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
    [SerializeField] float heightSpringBreakThreshold;
    [SerializeField] float grappleSquatReduction;
    [SerializeField] float heightSampleMin;
    [SerializeField] float heightSampleMax;

    [Header("Balance & Rotation")]
    [SerializeField] float abdomenRotationSpeed;
    [SerializeField] float headRotationSpeed;
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
    [SerializeField] float crouchBoostMinProgress;
    [SerializeField] float crouchReleaseSpeedMultiplier;
    [SerializeField] float airborneLegAnimationTimeScale;
    [SerializeField] float airborneLegSpeedMax;
    [SerializeField] float airborneStrideMultiplier;
    [SerializeField] float strideMultiplierSmoothingRate;

    [Header("Free Hang")]
    [SerializeField] float freeHangGroundedEntryLegExtensionThreshold;
    [SerializeField] float freeHangLegAngleMin;
    [SerializeField] float freeHangHeadAngle;
    [SerializeField] float freeHangStepHeightReductionMax;
    [SerializeField] float freeHangStrideMultiplier;
    [SerializeField] float freeHangSimulateContactMax;

    [Header("Thrusters")]
    [SerializeField] Thruster thruster;
    [SerializeField] ThrusterFlame thrusterFlame;

    [SerializeField] SpiderPhysics spiderPhysics;

    PhysicsLegSynchronizer legSynch;

    SpiderInput spiderInput;

    bool chargingJump;
    bool waitingToHandleJump;
    float jumpVerificationTimer;
    float jumpAngleFraction;//0 - 1 (for angle going from 0 to jumpAngleMin)
    float cosJumpAngleMin;
    float sinJumpAngleMin;

    Vector2 abdomenBoneBaseRight;
    Vector2 abdomenBoneBaseRightL;
    Vector2 abdomenBoneBaseUp;
    Vector2 abdomenBoneBaseUpL;
    Quaternion abdomenBoneBaseLocalRotation;
    Quaternion abdomenBoneBaseLocalRotationL;

    bool grounded;
    float groundednessRating;
    float groundmapRaycastLength;
    Vector2 groundDirection = Vector2.right;
    Vector2 groundAnchorPt;
    Vector2 balanceDirection;
    float settleTimer;

    float cosFreeHangLegAngleMin;
    float cosScurryAngleMin;
    float sinScurryAngleMin;

    bool needChangeDirection;
    float crouchProgress;//0-1
    bool grappleScurrying;
    bool thrusterCooldownWarningSent;
    bool grappleFreeHangPrerequisites;

    bool flipInput;

    float HorizontalMoveInput => spiderInput.MoveInput.x;
    float LeanInput => spiderInput.SecondaryInput.x;
    bool ForceFreeHang => grapple.GrappleAnchored && spiderInput.ShiftAction.IsPressed();
    int Orientation => FacingRight ? 1 : -1;
    Vector2 OrientedRight => FacingRight ? transform.right : -transform.right;
    Vector2 OrientedGroundDirection => FacingRight ? groundDirection : -groundDirection;
    Vector2 HeightReferencePt => PhysBody.worldCenterOfMass;
    float MaxSpeed => grounded ? maxSpeed : maxSpeedAirborne;
    float GrappleScurryResistance => Vector2.Dot(grapple.LastCarryForce, -OrientedGroundDirection);
    float GrappleScurryResistanceFraction => Mathf.Clamp(GrappleScurryResistance / grappleScurryResistanceMax, 0, 1);

    public bool FacingRight => transform.localScale.x > 0;
    public float CrouchProgress => crouchProgress;
    public Thruster Thruster => thruster;
    public GrappleCannon Grapple => grapple;
    public ref PhysicsBody PhysBody => ref spiderPhysics.physicsBody;

    //mainly to hook up audio (later maybe also ui stuff)
    public UnityEvent jumpChargeBegan;
    public UnityEvent jumpChargeEnded;
    public UnityEvent jumped;
    public UnityEvent thrustersEngaged;
    public UnityEvent thrustersDisengaged;

    private void OnDrawGizmos()
    {
        if (Application.isPlaying && drawGizmos)
        {
            groundMap.DrawGizmos();
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(groundAnchorPt, 0.1f);
        }
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            Time.timeScale = timeScale;

            spiderPhysics?.OnValidate();
        }
    }

    private void Awake()
    {
        legSynch = GetComponent<PhysicsLegSynchronizer>();

        abdomenBoneBaseRight = abdomenBone.right.InFrameV2(transform.right, transform.up);
        abdomenBoneBaseRightL = new(abdomenBoneBaseRight.x, -abdomenBoneBaseRight.y);
        abdomenBoneBaseUp = abdomenBoneBaseRight.CCWPerp();
        abdomenBoneBaseUpL = abdomenBoneBaseRightL.CCWPerp();
        abdomenBoneBaseLocalRotation = MathTools.QuaternionFrom2DUnitVector(abdomenBoneBaseRight);
        abdomenBoneBaseLocalRotationL = MathTools.QuaternionFrom2DUnitVector(abdomenBoneBaseRightL);
        //bc abdomenBone is not a direct child of this.transform (so abdomenBone.localRotation is not what we want)

        cosFreeHangLegAngleMin = Mathf.Cos(freeHangLegAngleMin);
        cosScurryAngleMin = Mathf.Cos(grappleScurryAngleMin);
        sinScurryAngleMin = Mathf.Sin(grappleScurryAngleMin);
        cosJumpAngleMin = Mathf.Cos(jumpAngleMin);
        sinJumpAngleMin = Mathf.Sin(jumpAngleMin);

        thruster.Initialize();
        thrusterFlame.Initialize();

        spiderInput = GetComponent<SpiderInput>();
    }

    private void Start()
    {
        spiderPhysics.CreatePhysicsBody();
        InitializeGroundData();
        legSynch.Initialize();
        grapple.Initialize(spiderInput, PhysBody, FacingRight);

        Time.timeScale = timeScale;
    }

    private void OnDestroy()
    {
        groundMap.Dispose();
    }

    private void Update()
    {
        UpdateState();
    }

    private void FixedUpdate()
    {
        if (VerifyingJump())
        {
            jumpVerificationTimer -= Time.deltaTime;
        }

        RotateAbdomen(Time.deltaTime);
        RotateHead(Time.deltaTime);
        UpdateLegSynch();
        UpdateGroundData();
        UpdateThruster();
        UpdateGrappleScurrying();

        if (needChangeDirection)
        {
            ChangeDirection();
            needChangeDirection = false;
        }

        if (chargingJump)
        {
            jumpAngleFraction = Mathf.Clamp(jumpAngleFraction + LeanInput * jumpAngleLerpRate * Time.deltaTime, 0, 1);
        }
        else if (jumpAngleFraction != 0 && !waitingToHandleJump)
        {
            jumpAngleFraction = 0;
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
            settleTimer -= Time.deltaTime;
        }
    }


    //THRUSTER

    //do before you handle any move input
    private void UpdateThruster()
    {
        switch (thruster.FixedUpdate(ref PhysBody))
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

        thrusterFlame.Update(thruster.Engaged ? legSynch.absoluteBodyGroundSpeed : -1, Time.deltaTime);
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
        //Debug.Log("thrusters ran out of charge");
        OnThrusterDisengaged();
    }

    private void OnThrusterCooldownEnded()
    {
        //Debug.Log("thrusters cooldown ended");
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
            //Debug.Log("thrusters on cooldown...");
            thrusterCooldownWarningSent = true;
        }
    }

    private void OnThrusterDisengaged()
    {
        //Debug.Log("disengaging thrusters.");
        thrustersDisengaged.Invoke();
    }

    //INPUT

    //2do: event based input (will save us from doing a bunch of redundant checks every frame)
    private void UpdateState()
    {
        if (chargingJump)
        {
            if (!grounded)
            {
                chargingJump = false;
                jumpChargeEnded.Invoke();
            }
            else if (!spiderInput.SpaceAction.IsPressed())
            {
                chargingJump = false;
                waitingToHandleJump = !spiderInput.ControlAction.IsPressed();
                jumpChargeEnded.Invoke();
            }
            else if (crouchProgress < 1)
            {
                UpdateCrouch(Time.deltaTime);
            }
        }
        else if (!waitingToHandleJump)
        {
            if (grounded && spiderInput.SpaceAction.IsPressed())
            {
                chargingJump = true;
                jumpChargeBegan.Invoke();
            }
            if (crouchProgress > 0)
            {
                UpdateCrouch(crouchReleaseSpeedMultiplier * -Time.deltaTime);
            }
        }

        grapple.aimInput = chargingJump ? 0 : LeanInput;
        UpdateGrappleScurrying();//needs to be updated before changing direction

        flipInput = spiderInput.FAction.IsPressed();// ? spiderInput.ShiftAction.IsPressed() ? FlipState.hold : FlipState.flip : FlipState.none;

        //make sure you update freeHanging before changing direction
        if (grapple.GrappleAnchored)
        {
            //hold shift to enter freehang/swing mode (if it's the default state, then repelling and direction change gets annoying)
            grappleFreeHangPrerequisites = HorizontalMoveInput == 0 || ForceFreeHang || thruster.Cooldown;
        }
        else if (grappleFreeHangPrerequisites)
        {
            grappleFreeHangPrerequisites = false;
        }

        if (MathTools.OppositeSigns(HorizontalMoveInput, Orientation))
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
        progressDelta = Mathf.Clamp(progressDelta, -crouchProgress, 1 - crouchProgress);
        crouchProgress += progressDelta;
        abdomenBone.position -= progressDelta * crouchHeightFraction * preferredRideHeight * transform.up;
    }

    private void UpdateGrappleScurrying()
    {
        grappleScurrying = grounded && HorizontalMoveInput != 0 && grapple.GrappleAnchored;
    }

    private void ChangeDirection()
    {
        if (!grapple.FreeHanging)
        {
            var s = transform.localScale;
            transform.localScale = new Vector3(-s.x, s.y, s.z);
            legSynch.OnBodyChangedDirection(transform.position, transform.position, transform.right);
        }
        else
        {
            PhysBody.SyncTransform();
            Vector2 o = grapple.FreeHangLeveragePoint;
            Vector2 p = PhysBody.position;
            Vector2 u = PhysBody.rotation.direction.CCWPerp();

            var s = transform.localScale;
            transform.localScale = new Vector3(-s.x, s.y, s.z);

            PhysBody.rotation = new PhysicsRotate(-PhysBody.rotation.direction);
            PhysBody.SyncTransform();
            PhysBody.position += o - grapple.FreeHangLeveragePoint;
            //SyncTransform();
            //translate so grapple.FreeHangeLeveragePoint stays in same place
            //(it's where move forces are applied while freeHanging, and we want to keep movement smooth)

            legSynch.OnBodyChangedDirection(p, PhysBody.position, u);

            //void SyncTransform()//so we have accurate position of grapple.FreeHangLeveragePoint -- inefficient, but reliable
            //{
            //    PhysBody.GetPositionAndRotation3D(transform, PhysicsWorld.defaultWorld.transformWriteMode, PhysicsWorld.TransformPlane.XY, out var pos, out var rot);
            //    transform.SetPositionAndRotation(pos, rot);
            //}
        }

        grapple.SetOrientation(FacingRight);
    }

    private void HandleMoveInput()
    {
        //accelCap bc otherwise if speed is highly negative, we get ungodly rates of acceleration
        //(and note that we are doing it in a way that scales with max speed)
        if (HorizontalMoveInput != 0)
        {
            if (grapple.FreeHanging)
            {
                PhysBody.ApplyForce(PhysBody.mass * (accelFactorFreeHanging * FreeHangingMoveDirection()), grapple.FreeHangLeveragePoint);
            }
            else
            {
                Vector2 d = flipInput && !grounded ? -OrientedGroundDirection : OrientedGroundDirection;
                var spd = Vector2.Dot(PhysBody.linearVelocity, d);
                var maxSpd = MaxSpeed;
                var accFactor = grounded ? accelFactor : (thruster.Engaged ? thrustingAccelFactor : deadThrusterAccelFactor * Mathf.Clamp(1 - d.y, 0, 1));

                var s = Mathf.Min(maxSpd - spd, accelCap * maxSpd);
                if (grounded || s > 0)
                {
                    PhysBody.ApplyForceToCenter(accFactor * s * PhysBody.mass * d);
                }
            }
        }
        ////apply grip and drag
        else if (grounded)
        {
            var d = OrientedRight;
            var vel = Vector2.Dot(PhysBody.linearVelocity, d);
            var l = Vector2.Dot(groundAnchorPt - HeightReferencePt, d);
            var grip = gripStrength * l - gripDamping * vel;
            PhysBody.ApplyForceToCenter(PhysBody.mass * grip * d);//grip to steep slope
        }
    }

    private Vector2 FreeHangingMoveDirection()
    {
        return FacingRight ? grapple.GrappleExtent.normalized.CWPerp() : grapple.GrappleExtent.normalized.CCWPerp();
    }


    //BALANCE & ROTATION

    private void Balance()
    {
        var f = -(grounded ? balanceSpringDamping : airborneBalanceSpringDamping) * PhysBody.angularVelocity;

        if (!grapple.FreeHanging)
        {
            var a = MathTools.PseudoAngle(transform.right, flipInput && !grounded ? -balanceDirection : balanceDirection);
            f += a * (grounded ? balanceSpringForce : airborneBalanceSpringForce);
        }

        PhysBody.ApplyTorque(PhysBody.mass * f);
    }

    private void RotateAbdomen(float dt)
    {
        var r = MathTools.CheapRotationalLerpClamped(AbdomenBoneRightInBaseLocalCoords(), AbdomenAngle(), abdomenRotationSpeed * dt, out bool changed);
        //^and this returns early when already at correct rotation.
        //We could do this all in terms of quaternions, but it would be slower because we need to do two square roots to write down a quaternion (two half angles)
        //instead of just the one square root (normalizing) involved in CheapRotationalLerp of unit vector.
        //We could use first order deformation of quaternion (and fact that (cos(t/2))' = -0.5 * sin(t/2) = -0.5 * q.z),
        //but we would still have to normalize the quaternion afterwards, so I think it's best as is.

        if (changed)
        {
            var p = abdomenBonePivot.position;
            abdomenBone.rotation = transform.rotation * (FacingRight ? abdomenBoneBaseLocalRotation : abdomenBoneBaseLocalRotationL)
                * MathTools.QuaternionFrom2DUnitVector(r);
            abdomenBone.position += p - abdomenBonePivot.position;
        }
    }

    private Vector2 AbdomenBoneRightInBaseLocalCoords()
    {
        return FacingRight ? abdomenBone.right.InFrameV2(transform.right, transform.up).InFrame(abdomenBoneBaseRight, abdomenBoneBaseUp) :
            abdomenBone.right.InFrameV2(transform.right, transform.up).InFrame(abdomenBoneBaseRightL, abdomenBoneBaseUpL);
    }

    private Vector2 AbdomenBoneUpInBaseLocalCoords()
    {
        return FacingRight ? abdomenBone.up.InFrameV2(transform.right, transform.up).InFrame(abdomenBoneBaseRight, abdomenBoneBaseUp)
            : abdomenBone.up.InFrameV2(transform.right, transform.up).InFrame(abdomenBoneBaseRightL, abdomenBoneBaseUpL);
    }

    //in abdomen bone's "base local coords" (i.e. right = abdomenBoneBaseRight)
    private Vector2 AbdomenAngle()
    {
        return chargingJump ? JumpAbdomenAngle() : grappleScurrying ? ScurryAbdomenAngle() : Vector2.right;
    }

    private Vector2 JumpAbdomenAngle()
    {
        return jumpAngleFraction > 0 ? MathTools.CheapRotationalLerpClamped(Vector2.right, JumpAbdomenAngleMin(), jumpAngleFraction, out _) : Vector2.right;
    }

    private Vector2 JumpAbdomenAngleMin()
    {
        return new(cosJumpAngleMin, FacingRight ? sinJumpAngleMin : -sinJumpAngleMin);
    }

    private Vector2 ScurryAbdomenAngle()
    {
        var f = GrappleScurryResistanceFraction;
        if (f > 0)
        {
            return MathTools.CheapRotationalLerpClamped(Vector2.right, ScurryAbdomenAngleMin(), f, out _);
        }
        return Vector2.right;
    }

    private Vector2 ScurryAbdomenAngleMin()
    {
        return new(cosScurryAngleMin, FacingRight ? sinScurryAngleMin : -sinScurryAngleMin);
    }

    private void RotateHead(float dt)
    {
        Vector2 g;
        if (grounded)
        {
            if (!(settleTimer > 0))
            {
                return;
            }
            var p = groundMap.TrueClosestPoint((Vector2)headBoneHeightRefPoint.position, out var t, out _, out _);
            var n = FacingRight ?
                groundMap.AverageNormalFromCenter(t + headRotationMinPos, t + headRotationMaxPos)
                : groundMap.AverageNormalFromCenter(t - headRotationMaxPos, t - headRotationMinPos);
            g = n.CWPerp();
        }
        else
        {
            g = grapple.FreeHanging ? FreeHangingHeadRight() : (Vector2)transform.right;
        }

        headBone.ApplyCheapRotationalLerpClamped(g, headRotationSpeed * dt, out _);//if rotate at constant speed, it starts to flicker when rotation is small
    }

    private Vector2 FreeHangingHeadRight()
    {
        var y = transform.right.y;
        if (y < 0)
        {
            y = -y * freeHangHeadAngle;
            return Mathf.Cos(y) * transform.right + Mathf.Sin(y) * (FacingRight ? transform.up : -transform.up);
        }
        return transform.right;
    }


    //JUMPING

    //not checking anything here, bc i have it set up so it only collects jump input when you are able to jump
    //(i.e. when grounded and not verifying jump)
    private void HandleJumpInput()
    {
        if (waitingToHandleJump)
        {
            waitingToHandleJump = false;
            jumpVerificationTimer = jumpVerificationTime;//needs to be first bc SetGround > OnTakeoff depends on VerifyingJump()
            groundednessRating = 0;
            SetGrounded(false);
            var jumpDir = JumpDirection();
            PhysBody.ApplyLinearImpulseToCenter(PhysBody.mass * JumpForce() * jumpDir);
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
        var u = AbdomenBoneUpInBaseLocalCoords();//quaternion version would compute a lot of extra trivial products
        return u.x * transform.right + u.y * transform.up;
    }

    private bool VerifyingJump()
    {
        return jumpVerificationTimer > 0;
    }


    //HEIGHT SPRING

    private void UpdateHeightSpring()
    {
        Vector2 p = groundMap.AveragePointFromCenter(heightSampleMin, heightSampleMax);
        //^average point so that body sinks a little as it rounds a sharp peak (keeping leg extension natural)
        //--note you only want to do this when strongly grounded

        Vector2 down = -transform.up;
        var v = Vector2.Dot(PhysBody.linearVelocity, down);
        var l = Vector2.Dot(p - HeightReferencePt, down) - preferredRideHeight;
        var f = l * heightSpringForce;
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

        PhysBody.ApplyForceToCenter(PhysBody.mass * (f - heightSpringDamping * v - Vector2.Dot(Physics2D.gravity, down)) * down);
        //remove affect of gravity while height spring engaged, otherwise you will settle at a height which is off by -Vector2.Dot(Physics2D.gravity, down) / heightSpringForce
        //(meaning you will be under height when upright, and over height when upside down (which was causing feet to not reach ground while upside down))
        //(e.g. before ride height on flat ground was always off by +- 32/400 = 0.08)
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

    private void RecomputeGroundMapRaycastLength()
    {
        groundmapRaycastLength = (grounded ? groundedExitToleranceFactor : groundedEntryToleranceFactor) * preferredRideHeight;
    }

    private void OnTakeOff()
    {
        if (chargingJump)
        {
            chargingJump = false;
            jumpChargeEnded.Invoke();
        }
        RecomputeGroundMapRaycastLength();
    }

    private void OnLanding()
    {
        RecomputeGroundMapRaycastLength();
    }

    private void UpdateGroundData()
    {
        UpdateGroundMap();
        var i = groundMap.IndexOfFirstGroundHitFromCenter;
        bool isCentralIndex = i == groundMap.CentralIndex;
        var pt = groundMap.MapPoint(i);
        var goalGroundDirection = pt.hitGround && isCentralIndex ?
            groundMap.AverageNormalFromCenter(-groundDirectionSampleWidth, groundDirectionSampleWidth).CWPerp()
            : pt.normal.CWPerp();

        groundDirection = grounded ? goalGroundDirection : Vector2.right;

        if (HorizontalMoveInput != 0 || !grounded || (grapple.GrappleAnchored && grapple.GrappleReleaseInput < 0))
        {
            groundAnchorPt = GetGroundAnchorPoint(ref pt, isCentralIndex);
            balanceDirection = groundDirection;
        }
        else if (settleTimer > 0)
        {
            var t = settleTimer / settleTime;
            balanceDirection = MathTools.CheapRotationalLerpClamped(balanceDirection, groundDirection, 1 - 0.5f * t, out _);
            var p = Vector2.Lerp(groundAnchorPt, GetGroundAnchorPoint(ref pt, isCentralIndex), t);
            groundAnchorPt = groundMap.TrueClosestPoint(p, out _, out _, out _);
        }

        if (!VerifyingJump())
        {
            UpdateGroundednessRating();
            SetGrounded(GroundedCondition());
            grapple.FreeHanging = grappleFreeHangPrerequisites && !grounded;
        }

        Vector2 GetGroundAnchorPoint(ref GroundMapPt pt, bool isCentralIndex)
        {
            if (pt.hitGround && !isCentralIndex)
            {
                var castResult = PhysBody.world.CastRay(HeightReferencePt, -backupGroundPtRaycastLengthFactor * groundmapRaycastLength * pt.normal, spiderPhysics.queryFilter);
                if (castResult.Length > 0)
                {
                    return castResult[0].point;
                }
            }

            return pt.point;
        }
    }

    private bool GroundedCondition()
    {
        return ((!grapple.FreeHanging || grounded || legSynch.AnyGroundedLegsUnderextended(freeHangGroundedEntryLegExtensionThreshold))
            && groundednessRating > 0) || spiderPhysics.HasContact();
        //keep the HasContact() out of parentheses; that on its own automatically qualifies you as grounded!
    }

    private void UpdateGroundednessRating()
    {
        groundednessRating = groundednessRating == 0 ? legSynch.FractionTouchingGround > 0 ? Mathf.Max(legSynch.FractionTouchingGround, groundednessInitialContactValue) : 0
            : grapple.GrappleAnchored && grapple.GrappleReleaseInput < 0 ? legSynch.FractionTouchingGround
            : MathTools.LerpAtConstantSpeed(groundednessRating, legSynch.FractionTouchingGround, groundednessSmoothingRate, Time.deltaTime);
    }

    private void UpdateGroundMap()
    {
        groundMap.UpdateMap(PhysBody.world, spiderPhysics.queryFilter,
            HeightReferencePt,
            -transform.up,
            transform.right,
            groundmapRaycastLength,
            FacingRight);
        //groundMap.UpdateMap(PhysBody.world, spiderPhysics.queryFilter,
        //    HeightReferencePt,
        //    -transform.up,
        //    transform.right,
        //    groundmapRaycastLength,
        //    FacingRight);
    }

    private void InitializeGroundMap()
    {
        groundMap.Initialize();
        UpdateGroundMap();
        //groundMap.UpdateMapImmediate(PhysBody.world, spiderPhysics.queryFilter,
        //    HeightReferencePt,
        //    -transform.up,
        //    transform.right,
        //    groundmapRaycastLength,
        //    FacingRight);
    }

    private void InitializeGroundData()
    {
        RecomputeGroundMapRaycastLength();
        InitializeGroundMap();
        UpdateGroundednessRating();
        groundAnchorPt = groundMap.Center.point;
    }


    //LEGS

    private float FreeHangStrideMultiplier()
    {
        var y = transform.right.y;
        //using y > cosLegAngleMax b/c we really want sin(90 - legAngleMax)
        return y > cosFreeHangLegAngleMin ? 1 :
            y > 0 ? Mathf.Lerp(airborneStrideMultiplier, 1, y / cosFreeHangLegAngleMin)
            : Mathf.Lerp(airborneStrideMultiplier, 1, -y);
    }

    private void UpdateLegSynch()
    {
        legSynch.State =
            grounded ? PhysicsLegSynchronizer.LegState.standard
            : thruster.Engaged ? PhysicsLegSynchronizer.LegState.jumping
            : grapple.FreeHanging ? PhysicsLegSynchronizer.LegState.limp
            : PhysicsLegSynchronizer.LegState.freefall;

        var groundVelocity = Vector2.Dot(PhysBody.linearVelocity, OrientedGroundDirection);
        legSynch.bodyGroundSpeedSign = (grounded && grapple.GrappleAnchored) || grapple.FreeHanging ? 1 : Mathf.Sign(groundVelocity);
        legSynch.absoluteBodyGroundSpeed = grounded ? Mathf.Abs(groundVelocity) : Mathf.Min(PhysBody.linearVelocity.magnitude, airborneLegSpeedMax);
        legSynch.stepHeightFraction = 1 - crouchProgress * crouchHeightFraction;
        legSynch.timeScale = grounded || thruster.Engaged ? 1 : airborneLegAnimationTimeScale;

        var simulateContactWeight = 0f;

        switch (legSynch.State)
        {
            case PhysicsLegSynchronizer.LegState.standard:
                legSynch.strideMultiplier = 1;
                break;
            case PhysicsLegSynchronizer.LegState.jumping:
                legSynch.strideMultiplier = MathTools.LerpAtConstantSpeed(legSynch.strideMultiplier, airborneStrideMultiplier,
                    strideMultiplierSmoothingRate, Time.deltaTime);
                break;
            case PhysicsLegSynchronizer.LegState.freefall:
                legSynch.strideMultiplier = MathTools.LerpAtConstantSpeed(legSynch.strideMultiplier, airborneStrideMultiplier,
                    strideMultiplierSmoothingRate, Time.deltaTime);
                break;
            case PhysicsLegSynchronizer.LegState.limp:
                var r = OrientedRight;
                var y = grounded ? 0 : MathTools.OppositeSigns(Orientation, r.x) ? 1 : r.y < -MathTools.sin15 ? (-r.y - MathTools.sin15) / (1 - MathTools.sin15) : 0;
                legSynch.stepHeightFraction *= 1 - freeHangStepHeightReductionMax * y;
                legSynch.strideMultiplier = MathTools.LerpAtConstantSpeed(legSynch.strideMultiplier, Mathf.Lerp(FreeHangStrideMultiplier(), freeHangStrideMultiplier, y),
                        strideMultiplierSmoothingRate, Time.deltaTime);
                simulateContactWeight = (1 - y);
                simulateContactWeight *= simulateContactWeight * freeHangSimulateContactMax;
                legSynch.absoluteBodyGroundSpeed *= simulateContactWeight;
                break;
        }

        legSynch.UpdateAllLegs(Time.deltaTime, groundMap, grounded, simulateContactWeight);
    }
}