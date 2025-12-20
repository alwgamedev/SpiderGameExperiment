using System;
using UnityEngine;
using UnityEngine.Events;

public class SpiderMovementController : MonoBehaviour
{
    [Header("Body")]
    [SerializeField] Transform abdomenBone;
    [SerializeField] Transform abdomenBonePivot;
    [SerializeField] Transform headBone;
    [SerializeField] Transform heightReferencePoint;
    [SerializeField] GrappleCannon grapple;

    [Header("Ground Data")]
    [SerializeField] LayerMask groundLayer;
    [SerializeField] float groundedExitToleranceFactor;
    [SerializeField] float groundedEntryToleranceFactor;
    [SerializeField] float groundednessSmoothingRate;
    [SerializeField] float groundednessInitialContactValue;
    [SerializeField] float groundDirectionSampleWidth;
    [SerializeField] float backupGroundPtRaycastLengthFactor;
    [SerializeField] float groundPtSlipRate;
    [SerializeField] float groundPtSlipThreshold;
    [SerializeField] float upcomingGroundDirectionMinPos;
    [SerializeField] float upcomingGroundDirectionMaxPos;
    [SerializeField] float failedGroundDirectionSmoothingRate;
    [SerializeField] GroundMap groundMap;

    [Header("Movement")]
    [SerializeField] float accelFactor;
    [SerializeField] float accelFactorFreeHanging;
    [SerializeField] float deadThrusterAccelFactor;
    [SerializeField] float thrustingAccelFactor;
    [SerializeField] float accelCap;
    [SerializeField] float decelFactor;
    [SerializeField] float gripStrength;
    [SerializeField] float grapplePullGripReduction;
    [SerializeField] float maxSpeed;
    [SerializeField] float maxSpeedAirborne;

    [Header("Height Spring")]
    [SerializeField] float preferredRideHeight;
    [SerializeField] float heightSpringForce;
    [SerializeField] float heightSpringDamping;
    [SerializeField] float heightSpringBreakThreshold;
    [SerializeField] float grappleSquatReduction;
    [SerializeField] float heightSampleWidth;//2do: scale with spider size (and same for other fields)

    [Header("Balance & Rotation")]
    [SerializeField] float abdomenRotationSpeed;
    [SerializeField] float headRotationSpeed;
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
    [SerializeField] float airborneStrideMultiplier;
    [SerializeField] float strideMultiplierSmoothingRate;

    [Header("Free Hang")]
    [SerializeField] float freeHangGroundedEntryLegExtensionThreshold;
    [SerializeField] float freeHangLegAngleMin;
    [SerializeField] float freeHangLegAngleSkew;
    [SerializeField] float freeHangHeadAngle;
    [SerializeField] Vector2 baseFreeHangDriftWeight;
    [SerializeField] float freeHangStepHeightReductionMax;
    [SerializeField] float freeHangStrideMultiplier;

    [Header("Thrusters")]
    [SerializeField] Thruster thruster;
    [SerializeField] ThrusterFlame thrusterFlame;
    [SerializeField] float thrustingGravityScale;

    Rigidbody2D rb;
    Collider2D headCollider;
    Collider2D abdomenCollider;
    Collider2D[] bodyCollisionBuffer = new Collider2D[1];
    ContactFilter2D bodyCollisionFilter;
    LegSynchronizer legSynchronizer;

    int moveInput;
    int leanInput;//used to control either cannon fulcrum rotation or jump aim when jumpInput held

    bool jumpInputHeld;
    bool waitingToReleaseJump;
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

    bool needChangeDirection;

    bool grounded;
    float groundednessRating;
    float groundmapRaycastLength;
    Vector2 groundDirection = Vector2.right;
    Vector2 upcomingGroundDirection = Vector2.right;
    GroundMapPt groundPt;

    float cosFreeHangLegAngleMin;
    float sinFreeHangLegAngleMin;
    float cosScurryAngleMin;
    float sinScurryAngleMin;

    float crouchProgress;//0-1

    bool grappleScurrying;

    bool thrusterCooldownWarningSent;

    bool freeHangInput;
    bool grappleFreeHangPrerequisites;

    public bool FacingRight => transform.localScale.x > 0;
    int Orientation => FacingRight ? 1 : -1;
    Vector2 OrientedRight => FacingRight ? transform.right : -transform.right;
    Vector2 OrientedGroundDirection => FacingRight ? groundDirection : -groundDirection;
    float PreferredBodyPosGroundHeight => transform.position.y - heightReferencePoint.position.y + preferredRideHeight;
    float MaxSpeed => grounded ? maxSpeed : maxSpeedAirborne;
    float GroundVelocity => Vector2.Dot(rb.linearVelocity, OrientedGroundDirection);
    Vector2 GroundPtGroundDirection => groundPt.normal.CWPerp();
    float GrappleScurryResistance => Vector2.Dot(grapple.LastCarryForce, -OrientedGroundDirection);
    float GrappleScurryResistanceFraction => Mathf.Clamp(GrappleScurryResistance / grappleScurryResistanceMax, 0, 1);

    public float CrouchProgress => crouchProgress;
    public Thruster Thruster => thruster;
    public GrappleCannon Grapple => grapple;

    public static SpiderMovementController Player;

    public UnityEvent JumpChargeBegan;
    public UnityEvent JumpChargeEnded;
    public UnityEvent Jumped;
    public UnityEvent ThrustersEngaged;
    public UnityEvent ThrustersDisengaged;

    //private void OnDrawGizmos()
    //{
    //    if (Application.isPlaying)
    //    {
    //        groundMap.DrawGizmos();
    //        Gizmos.color = Color.magenta;
    //        Gizmos.DrawSphere(groundPt.point, 0.1f);
    //    }
    //}

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        legSynchronizer = GetComponent<LegSynchronizer>();

        abdomenBoneBaseRight = abdomenBone.right.InFrameV2(transform.right, transform.up);
        abdomenBoneBaseRightL = new(abdomenBoneBaseRight.x, -abdomenBoneBaseRight.y);
        abdomenBoneBaseUp = abdomenBoneBaseRight.CCWPerp();
        abdomenBoneBaseUpL = abdomenBoneBaseRightL.CCWPerp();
        abdomenBoneBaseLocalRotation = MathTools.QuaternionFrom2DUnitVector(abdomenBoneBaseRight);
        abdomenBoneBaseLocalRotationL = MathTools.QuaternionFrom2DUnitVector(abdomenBoneBaseRightL);
        //bc abdomenBone is not a direct child of this.transform (so abdomenBone.localRotation is not what we want)

        cosFreeHangLegAngleMin = Mathf.Cos(freeHangLegAngleMin);
        sinFreeHangLegAngleMin = Mathf.Sin(freeHangLegAngleMin);
        cosScurryAngleMin = Mathf.Cos(grappleScurryAngleMin);
        sinScurryAngleMin = Mathf.Sin(grappleScurryAngleMin);
        cosJumpAngleMin = Mathf.Cos(jumpAngleMin);
        sinJumpAngleMin = Mathf.Sin(jumpAngleMin);

        thruster.Initialize();
        thrusterFlame.Initialize();

        //Player = this;

        //Time.timeScale = 0.25f;//useful for spotting issues
    }

    private void Start()
    {
        headCollider = headBone.GetComponent<Collider2D>();
        abdomenCollider = abdomenBone.GetComponent<Collider2D>();

        rb.centerOfMass = heightReferencePoint.position - transform.position;

        InitializeGroundData();
        legSynchronizer.Initialize(PreferredBodyPosGroundHeight, FacingRight, groundMap);
        bodyCollisionFilter.NoFilter();
        bodyCollisionFilter.useTriggers = false;
        bodyCollisionFilter.SetLayerMask(groundLayer);
    }

    private void Update()
    {
        CaptureInput();
    }

    private void LateUpdate()
    {
        RotateAbdomen(Time.deltaTime);
        RotateHead(Time.deltaTime);
    }

    private void FixedUpdate()
    {
        if (VerifyingJump())
        {
            jumpVerificationTimer -= Time.deltaTime;
        }

        UpdateGroundData();
        UpdateThruster();
        UpdateGrappleScurrying();

        if (needChangeDirection)
        {
            ChangeDirection();
            needChangeDirection = false;
        }

        if (jumpInputHeld)
        {
            jumpAngleFraction = Mathf.Clamp(jumpAngleFraction - leanInput * jumpAngleLerpRate * Time.deltaTime, 0, 1);
        }
        else if (jumpAngleFraction != 0 && !waitingToReleaseJump)
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
    }


    //THRUSTER

    //do before you handle any move input
    private void UpdateThruster()
    {
        switch (thruster.Update(Time.deltaTime))
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

        thrusterFlame.Update(thruster.Engaged ? legSynchronizer.absoluteBodyGroundSpeed : -1, Time.deltaTime);
    }

    private void UpdateThrusterEngagement()
    {
        if (thruster.Engaged)
        {
            if (grounded || grapple.FreeHanging || moveInput == 0)
            {
                DisengageThruster();
            }
        }
        else if (!grounded && moveInput != 0 && !freeHangInput)
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
        rb.gravityScale = thrustingGravityScale;
        ThrustersEngaged.Invoke();
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
        rb.gravityScale = 1;
        ThrustersDisengaged.Invoke();
    }

    //INPUT

    //2do: event based input (will save us from doing a bunch of redundant checks every frame)
    private void CaptureInput()
    {
        if (jumpInputHeld)
        {
            if (!grounded)
            {
                jumpInputHeld = false;
                JumpChargeEnded.Invoke();
            }
            else if (Input.GetKeyUp(KeyCode.Space))
            {
                jumpInputHeld = false;
                waitingToReleaseJump = !Input.GetKey(KeyCode.LeftControl);
                JumpChargeEnded.Invoke();
            }
            else if (crouchProgress < 1)
            {
                UpdateCrouch(Time.deltaTime);
            }
        }
        else if (!waitingToReleaseJump)
        {
            if (grounded)
            {
                jumpInputHeld = Input.GetKey(KeyCode.Space);
                if (jumpInputHeld)
                {
                    JumpChargeBegan.Invoke();
                }
            }
            if (crouchProgress > 0)
            {
                UpdateCrouch(crouchReleaseSpeedMultiplier * -Time.deltaTime);
            }
        }

        moveInput = (Input.GetKey(KeyCode.RightArrow) ? 1 : 0) + (Input.GetKey(KeyCode.LeftArrow) ? -1 : 0);
        freeHangInput = grapple.GrappleAnchored && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
        leanInput = (Input.GetKey(KeyCode.A) ? 1 : 0) + (Input.GetKey(KeyCode.D) ? -1 : 0);
        grapple.aimInput = jumpInputHeld ? 0 : leanInput;
        UpdateGrappleScurrying();//needs to be updated before changing direction

        //make sure you update freeHanging before changing direction
        if (grapple.GrappleAnchored)
        {
            //hold shift to enter freehang/swing mode (if it's the default state, then repelling and direction change gets annoying)
            grappleFreeHangPrerequisites = moveInput == 0 || freeHangInput || thruster.Cooldown;
        }
        else if (grappleFreeHangPrerequisites)
        {
            grappleFreeHangPrerequisites = false;
        }

        if (MathTools.OppositeSigns(moveInput, Orientation))
        {
            needChangeDirection = true;
        }

        if (thrusterCooldownWarningSent && moveInput == 0)
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
        grappleScurrying = grounded && moveInput != 0 && grapple.GrappleAnchored;
    }

    private void ChangeDirection()
    {
        //2do: can we interpolate between these by free hang strength in a reasonable way (or is there no need to do that?)
        if (!grapple.FreeHanging/*!legSynchronizer.FreeHanging*/)
        {
            var s = transform.localScale;
            transform.localScale = new Vector3(-s.x, s.y, s.z);
        }
        else
        {
            Vector2 o = grapple.FreeHangLeveragePoint;
            var p = transform.position;
            var s = transform.localScale;
            transform.localScale = new Vector3(-s.x, s.y, s.z);
            var n = grapple.ShootDirection.CCWPerp();//normal to the hyperplane we're reflecting over
            transform.up = MathTools.ReflectAcrossHyperplane(transform.up, (Vector3)n);
            var d = (Vector3)(o - grapple.FreeHangLeveragePoint);
            transform.position += d;
            legSynchronizer.OnBodyChangedDirectionFreeHanging(p, transform.position, n);
        }
    }

    private void HandleMoveInput()
    {
        //accelCap bc otherwise if speed is highly negative, we get ungodly rates of acceleration
        //(and note that we are doing it in a way that scales with max speed
        //-- so you can limit maxSpd - spd to being e.g. double the maxSpeed or w/e)
        if (moveInput != 0)
        {
            if (grounded || !grapple.GrappleAnchored || (!freeHangInput && thruster.Engaged))
            {
                Vector2 d = OrientedGroundDirection;
                var spd = Vector2.Dot(rb.linearVelocity, d);
                var maxSpd = MaxSpeed;
                var accFactor = grounded ? accelFactor : (thruster.Engaged ? thrustingAccelFactor : deadThrusterAccelFactor);

                var s = Mathf.Min(maxSpd - spd, accelCap * maxSpd);
                if (grounded || s > 0)
                {
                    rb.AddForce(accFactor * s * rb.mass * d);
                }
            }
            else
            {
                //may add max speed and spring like acceleration?
                rb.AddForceAtPosition(rb.mass * (accelFactorFreeHanging * FreeHangingMoveDirection()), grapple.FreeHangLeveragePoint);
            }
        }
        //apply grip and drag
        else if (grounded)
        {
            Vector2 d = FacingRight ? GroundPtGroundDirection : -GroundPtGroundDirection;
            var spd = Vector2.Dot(rb.linearVelocity, d);
            var h = Vector2.Dot(groundPt.point - (Vector2)heightReferencePoint.position, d);
            var grip = gripStrength * h;
            if (grapple.GrappleAnchored)
            {
                //so grip doesn't fight against carry force
                if (grapple.GrappleReleaseInput < 0)
                {
                    var c = Vector2.Dot(grapple.LastCarryForce, d);
                    if (MathTools.OppositeSigns(grip, c))
                    {
                        grip += grapplePullGripReduction * c;
                    }
                }
            }
            var f = rb.mass * (grip - decelFactor * spd) * d;
            rb.AddForce(f);//grip to steep slope
        }
    }

    private Vector2 FreeHangingMoveDirection()
    {
        return FacingRight ? grapple.GrappleExtent.normalized.CWPerp() : grapple.GrappleExtent.normalized.CCWPerp();
    }


    //BALANCE & ROTATION

    private void Balance()
    {
        var f = -(grounded ? balanceSpringDamping : airborneBalanceSpringDamping) * rb.angularVelocity;

        if (!grapple.FreeHanging)
        {
            var a = MathTools.PseudoAngle(transform.right, groundDirection);
            f += a * (grounded ? balanceSpringForce : airborneBalanceSpringForce);
        }

        rb.AddTorque(rb.mass * f);
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
        return jumpInputHeld ? JumpAbdomenAngle() : grappleScurrying ? ScurryAbdomenAngle() : Vector2.right;
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
        var g = grounded ? upcomingGroundDirection : (grapple.FreeHanging ? FreeHangingHeadRight() : (Vector2)transform.right);
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
        if (waitingToReleaseJump)
        {
            waitingToReleaseJump = false;
            jumpVerificationTimer = jumpVerificationTime;//needs to be first bc SetGround > OnTakeoff depends on VerifyingJump()
            groundednessRating = 0;
            SetGrounded(false);
            var jumpDir = JumpDirection();
            rb.AddForce(rb.mass * JumpForce() * jumpDir, ForceMode2D.Impulse);
            Jumped.Invoke();
        }
    }

    private float JumpForce()
    {
        if (crouchProgress * crouchTime < tapJumpVerificationTime)
        {
            return rb.gravityScale * jumpForce;
        }
        return rb.gravityScale * (jumpForce + crouchProgress * jumpForceCrouchBoostRate);
    }

    private Vector2 JumpDirection()
    {
        var u = AbdomenBoneUpInBaseLocalCoords();//quaternion version would compute a lot of extra trivial products (0 * x)
        return u.x * transform.right + u.y * transform.up;
    }

    private bool VerifyingJump()
    {
        return jumpVerificationTimer > 0;
    }


    //HEIGHT SPRING

    private void UpdateHeightSpring()
    {
        var p = groundMap.AveragePointFromCenter(-heightSampleWidth, heightSampleWidth);
        //^average point so that body sinks a little as it rounds a sharp peak (keeping leg extension natural)
        //--note you only want to do this when strongly grounded

        Vector2 down = -transform.up;
        var v = Vector2.Dot(rb.linearVelocity, down);
        var l = Vector2.Dot(p - (Vector2)heightReferencePoint.position, down) - preferredRideHeight;
        var f = l * heightSpringForce;
        if (grapple.GrappleAnchored)
        {
            var dot = Vector2.Dot(grapple.LastCarryForce, down);
            if (dot < 0 && l > 0 && grapple.GrappleReleaseInput < 0)
            {
                return;
            }
            else if (dot > 0 && l < 0)
            {
                f -= grappleSquatReduction * dot;//fight grapple a little when it's pulling you into the ground (i.e. reduce grappleCarryForce in direction of height spring)
            }
        }

        rb.AddForce(rb.mass * (f - heightSpringDamping * v - Vector2.Dot(Physics2D.gravity, down)) * down);
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
        if (jumpInputHeld)
        {
            jumpInputHeld = false;
            JumpChargeEnded.Invoke();
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
        var i = groundMap.IndexOfFirstGroundHitFromCenter(FacingRight, out var isCentralIndex);
        ref var pt = ref groundMap.map[i];
        var goalGroundDirection = pt.HitGround && isCentralIndex ?
            groundMap.AverageNormalFromCenter(-groundDirectionSampleWidth, groundDirectionSampleWidth).CWPerp()
            : pt.normal.CWPerp();

        UpdateLegSynch();

        if (!VerifyingJump())
        {
            UpdateGroundednessRating();
            SetGrounded(GroundedCondition());
            grapple.FreeHanging = grappleFreeHangPrerequisites && !grounded;
        }

        if (moveInput != 0 || !grounded)
        {
            UpdateGroundPoint(ref pt);
        }
        else
        {
            var r = pt.normal.CWPerp();
            var l = Vector2.Dot(groundPt.point - pt.point, r);// l ~ position of current groundPt relative to pt on the newly updated groundMap
            if (l > groundPtSlipThreshold || l < -groundPtSlipThreshold)
            {
                //if groundPt is far enough away from new pt, ...

                var v = Vector2.Dot(rb.linearVelocity, r);
                if (MathTools.OppositeSigns(l, v))
                {

                    //... and our velocity is moving us towards the new point,
                    //then let groundPt slip towards new pt

                    var t = groundPtSlipRate * Mathf.Abs(l) * Time.deltaTime;
                    var p = Vector2.Lerp(groundPt.point, pt.point, t);
                    p = groundMap.ClosestPoint(p, out var n, out _);//<-- using ClosestPt works a lot better than previous attempts
                    groundPt.Set(p, n, n.CWPerp(), 0, 0, pt.groundCollider);
                }
            }
        }

        void UpdateGroundPoint(ref GroundMapPt pt)
        {
            if (pt.HitGround && !isCentralIndex)
            {
                var r = Physics2D.Raycast(heightReferencePoint.position, -pt.normal, backupGroundPtRaycastLengthFactor * groundmapRaycastLength, groundLayer);
                if (r)
                {
                    groundPt.Set(r.point, r.normal, r.normal.CWPerp(), 0, 0, r.collider);
                    return;
                }
            }

            groundPt = pt;
        }

        groundDirection = grounded ? goalGroundDirection : MathTools.CheapRotationBySpeed(groundDirection, Vector2.right, failedGroundDirectionSmoothingRate, Time.deltaTime, out _);
        upcomingGroundDirection = FacingRight ? groundMap.AverageNormalFromCenter(upcomingGroundDirectionMinPos, upcomingGroundDirectionMaxPos)
            : groundMap.AverageNormalFromCenter(-upcomingGroundDirectionMaxPos, -upcomingGroundDirectionMinPos);
        upcomingGroundDirection = upcomingGroundDirection.CWPerp();
    }

    private bool GroundedCondition()
    {
        return (!grapple.FreeHanging || grounded || legSynchronizer.AnyGroundedLegsUnderextended(freeHangGroundedEntryLegExtensionThreshold)) 
            && groundednessRating > 0 || IsTouchingGroundLayer(headCollider) || IsTouchingGroundLayer(abdomenCollider);
        //keep the IsTouchingGroundLayer(coll) out of parentheses; these on their own automatically qualify you as grounded!
    }

    private void UpdateGroundednessRating()
    {
        groundednessRating = groundednessRating == 0 ? legSynchronizer.FractionTouchingGround > 0 ? Mathf.Max(legSynchronizer.FractionTouchingGround, groundednessInitialContactValue) : 0
            : grapple.GrappleAnchored && grapple.GrappleReleaseInput < 0 ? legSynchronizer.FractionTouchingGround : MathTools.LerpAtConstantRate(groundednessRating, legSynchronizer.FractionTouchingGround, groundednessSmoothingRate, Time.deltaTime);
    }

    private void InitializeGroundMap()
    {
        if (grapple.FreeHanging)
        {
            groundMap.UpdateMap(heightReferencePoint.position,
                FreeHangGroundMapDown(),
                FreeHangGroundMapRight(),
                groundmapRaycastLength,
                groundMap.CentralIndex,
                groundLayer);
        }
        else
        {
            groundMap.UpdateMap(heightReferencePoint.position,
                -transform.up,
                transform.right,
                groundmapRaycastLength,
                groundMap.CentralIndex,
                groundLayer);
        }
    }

    private void UpdateGroundMap()
    {
        if (grapple.FreeHanging)
        {
            groundMap.UpdateMap(heightReferencePoint.position,
                FreeHangGroundMapDown(),
                FreeHangGroundMapRight(),
                groundmapRaycastLength,
                groundMap.CentralIndex,
                groundLayer);
        }
        else
        {
            groundMap.UpdateMap(heightReferencePoint.position,
                -transform.up,
                transform.right,
                groundmapRaycastLength,
                groundMap.CentralIndex,
                groundLayer);
        }
    }

    //private void LerpUpdateGroundMap()
    //{
    //    if (grapple.FreeHanging)
    //    {
    //        groundMap.LerpUpdateMap(heightReferencePoint.position,
    //            FreeHangGroundMapDown(),
    //            FreeHangGroundMapRight(),
    //            groundmapRaycastLength,
    //            groundMap.CentralIndex,
    //            groundLayer);
    //    }
    //    else
    //    {
    //        groundMap.LerpUpdateMap(heightReferencePoint.position,
    //            -transform.up,
    //            transform.right,
    //            groundmapRaycastLength,
    //            groundMap.CentralIndex,
    //            groundLayer);
    //    }
    //}

    private Vector2 FreeHangGroundMapDown()
    {
        var r = OrientedRight;
        if (r.y > 0)
        {
            return -transform.up;
        }

        if (MathTools.OppositeSigns(r.x, Orientation))//upside down
        {
            return r.y < -cosFreeHangLegAngleMin ? cosFreeHangLegAngleMin * OrientedRight - sinFreeHangLegAngleMin * (Vector2)transform.up :
            MathTools.ReflectAcrossHyperplane(Vector2.down, (Vector2)transform.up);
        }

        return r.y < -cosFreeHangLegAngleMin ? cosFreeHangLegAngleMin * OrientedRight - sinFreeHangLegAngleMin * (Vector2)transform.up : Vector2.down;
    }

    private Vector2 FreeHangGroundMapRight()
    {
        var y = transform.right.y;
        if (y > 0)
        {
            return transform.right;
        }

        y *= FacingRight ? -freeHangLegAngleSkew : freeHangLegAngleSkew;//to get -/+ tUp based on FacingRight
        return Mathf.Cos(y) * transform.right + Mathf.Sin(y) * transform.up;
    }

    //only used for releasing freeHang state, but we'll put it in this section I guess
    private bool IsTouchingGroundLayer(Collider2D c)
    {
        c.GetContacts(bodyCollisionFilter, bodyCollisionBuffer);
        var contact = bodyCollisionBuffer[0] && (1 << bodyCollisionBuffer[0].gameObject.layer & groundLayer) != 0;
        bodyCollisionBuffer[0] = null;
        return contact;
    }

    private void InitializeGroundPoint()
    {
        groundPt = groundMap.Center;
    }

    private void InitializeGroundData()
    {
        RecomputeGroundMapRaycastLength();
        groundMap.Initialize();
        InitializeGroundMap();
        UpdateGroundednessRating();
        InitializeGroundPoint();
    }


    //LEGS

    private float AirborneStrideMultiplier()
    {
        var y = transform.right.y;
        //using y > cosLegAngleMax b/c we really want sin(90 - legAngleMax)
        return y > cosFreeHangLegAngleMin ? 1 :
            y > 0 ? Mathf.Lerp(airborneStrideMultiplier, 1, y / cosFreeHangLegAngleMin)
            : Mathf.Lerp(airborneStrideMultiplier, 1, -y);
    }

    private void UpdateLegSynch()
    {
        legSynchronizer.FreeHanging = !grounded;// && !thruster.Engaged;//grapple.FreeHanging;

        var v = GroundVelocity;
        legSynchronizer.bodyGroundSpeedSign = (grounded && grapple.GrappleAnchored) || grapple.FreeHanging ? 1 : Mathf.Sign(v);
        legSynchronizer.absoluteBodyGroundSpeed = grounded || thruster.Engaged ? Mathf.Abs(v) : rb.linearVelocity.magnitude;
        legSynchronizer.stepHeightFraction = 1 - crouchProgress * crouchHeightFraction;
        legSynchronizer.timeScale = grounded || thruster.Engaged ? 1 : airborneLegAnimationTimeScale;

        if (legSynchronizer.FreeHanging)
        {
            var r = OrientedRight;
            var dX = grounded ? 0 : r.y < -MathTools.sin15 ? (-r.y - MathTools.sin15)/(1 - MathTools.sin15)  : 0;
            legSynchronizer.LerpDriftWeights(dX);
            legSynchronizer.stepHeightFraction *= 1 - freeHangStepHeightReductionMax * dX;
            legSynchronizer.strideMultiplier = MathTools.LerpAtConstantRate(legSynchronizer.strideMultiplier, Mathf.Lerp(AirborneStrideMultiplier(), freeHangStrideMultiplier, dX),
                    strideMultiplierSmoothingRate, Time.deltaTime);
        }
        else
        {
            if (!grounded)
            {
                legSynchronizer.strideMultiplier = MathTools.LerpAtConstantRate(legSynchronizer.strideMultiplier, AirborneStrideMultiplier(),
                    strideMultiplierSmoothingRate, Time.deltaTime);
            }
            else if (legSynchronizer.strideMultiplier != 1)
            {
                legSynchronizer.strideMultiplier = 1;
            }
        }

        legSynchronizer.UpdateAllLegs(Time.deltaTime, groundMap);
    }
}