using UnityEngine;
using UnityEngine.Events;
using Unity.U2D.Physics;


public class SpiderMover : MonoBehaviour
{
    [SerializeField] float timeScale;
    [SerializeField] bool drawGizmos;

    [Header("Parts")]
    [SerializeField] Transform headBoneHeightRefPoint;
    [SerializeField] PhysicsLegSynchronizer legSynch;
    [SerializeField] SpiderPhysics spiderPhysics;
    [SerializeField] SpiderInput spiderInput;
    [SerializeField] GrappleCannon grapple;
    [SerializeField] Thruster thruster;
    [SerializeField] ThrusterFlame thrusterFlame;

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

    bool chargingJump;
    bool waitingToHandleJump;
    float jumpVerificationTimer;
    float jumpAngleFraction;//0 - 1 (for angle going from 0 to jumpAngleMin)
    PhysicsRotate jumpRotateMin;
    PhysicsRotate scurryRotateMin;
    float crouchProgress;//0 - 1

    bool grounded;
    float groundednessRating;
    Vector2 groundDirection;
    Vector2 groundAnchorPt;
    Vector2 balanceDirection;
    float settleTimer;

    Vector2[] legCastDirection;
    float cosFreeHangLegAngleMin;

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
    int Orientation => FacingRight ? 1 : -1;
    Vector2 Right => SpideyPhysics.LevelRight.direction;
    Vector2 Up => Right.CCWPerp();
    Vector2 OrientedRight => FacingRight ? Right : -Right;
    Vector2 OrientedGroundDirection => FacingRight ? groundDirection : -groundDirection;
    Vector2 HeightReferencePt => SpideyPhysics.HeightReferencePosition;
    float MaxSpeed => grounded ? maxSpeed : maxSpeedAirborne;
    float GrappleScurryResistance => Vector2.Dot(grapple.LastCarryForce, -OrientedGroundDirection);
    float GrappleScurryResistanceFraction => Mathf.Clamp(GrappleScurryResistance / grappleScurryResistanceMax, 0, 1);

    public bool FacingRight => SpideyPhysics.FacingRight;
    public float CrouchProgress => crouchProgress;
    public Thruster Thruster => thruster;
    public GrappleCannon Grapple => grapple;
    public ref SpiderPhysics SpideyPhysics => ref spiderPhysics;
    public ref PhysicsBody Abdomen => ref SpideyPhysics.abdomen;

    //mainly to hook up audio (later maybe also ui stuff)
    public UnityEvent jumpChargeBegan;
    public UnityEvent jumpChargeEnded;
    public UnityEvent jumped;
    public UnityEvent thrustersEngaged;
    public UnityEvent thrustersDisengaged;

    private void OnDrawGizmos()
    {
        if (drawGizmos)
        {
            if (Application.isPlaying)
            {
                groundMap.DrawGizmos();
                Gizmos.color = Color.magenta;
                Gizmos.DrawSphere(groundAnchorPt, 0.1f);
            }
            spiderPhysics.DrawGizmos();
        }

        grapple.OnDrawGizmos();
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            Time.timeScale = timeScale;

            spiderPhysics.OnValidate();
            thruster.Initialize();
            grapple.OnValidate();
        }
    }

    private void Awake()
    {
        legCastDirection = new Vector2[2];

        cosFreeHangLegAngleMin = Mathf.Cos(freeHangLegAngleMin);
        scurryRotateMin = new PhysicsRotate(new Vector2(Mathf.Cos(grappleScurryAngleMin), Mathf.Sin(grappleScurryAngleMin)));
        jumpRotateMin = new PhysicsRotate(new Vector2(Mathf.Cos(jumpAngleMin), Mathf.Sin(jumpAngleMin)));

        thruster.Initialize();
        thrusterFlame.Initialize();
    }

    private void Start()
    {
        spiderPhysics.CreatePhysicsBody(new PhysicsRotate(transform.right));
        InitializeGroundData();
        legSynch.Initialize();
        grapple.Initialize(spiderInput, Abdomen.world, SpideyPhysics.TotalMass, FacingRight);

        Time.timeScale = timeScale;
    }

    private void OnDestroy()
    {
        groundMap.Dispose();
        grapple.OnDestroy();
    }

    private void Update()
    {
        UpdateState();
        grapple.Update();
    }

    private void LateUpdate()
    {
        grapple.LateUpdate();
    }

    private void FixedUpdate()
    {
        if (VerifyingJump())
        {
            jumpVerificationTimer -= Time.deltaTime;
        }

        RotateAbdomen();
        RotateHead();
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
            if (crouchProgress < 1)
            {
                UpdateCrouch(Time.deltaTime);
            }
        }
        else if (!waitingToHandleJump)
        {
            if (crouchProgress > 0)
            {
                UpdateCrouch(crouchReleaseSpeedMultiplier * -Time.deltaTime);
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
            settleTimer -= Time.deltaTime;
        }

        grapple.FixedUpdate(SpideyPhysics.VirtualTransform, Abdomen);
    }


    //THRUSTER

    //do before you handle any move input
    private void UpdateThruster()
    {
        switch (thruster.FixedUpdate(ref Abdomen))
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

        if (MathTools.OppositeSigns(HorizontalMoveInput, SpideyPhysics.Orientation))
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
        if (!grapple.FreeHanging)
        {
            var reflection = SpideyPhysics.VirtualTransform;
            SpideyPhysics.ChangeDirection(reflection);
            legSynch.OnBodyChangedDirection(reflection.position, reflection.position, reflection.rotation.direction);
        }
        else
        {
            var p = SpideyPhysics.HeightReferencePosition;
            var u = SpideyPhysics.LevelRight.direction.CCWPerp();
            var reflection = new PhysicsTransform(grapple.FreeHangLeveragePoint, new PhysicsRotate(u));
            SpideyPhysics.ChangeDirection(reflection);
            legSynch.OnBodyChangedDirection(p, SpideyPhysics.HeightReferencePosition, u);
        }

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
                Abdomen.ApplyForce(SpideyPhysics.TotalMass * (accelFactorFreeHanging * FreeHangingMoveDirection()), grapple.FreeHangLeveragePoint);
            }
            else
            {
                Vector2 d = FlipInput && !grounded ? -OrientedGroundDirection : OrientedGroundDirection;
                var spd = Vector2.Dot(Abdomen.linearVelocity, d);
                var maxSpd = MaxSpeed;
                var accFactor = grounded ? accelFactor : (thruster.Engaged ? thrustingAccelFactor : deadThrusterAccelFactor * Mathf.Clamp(1 - d.y, 0, 1));

                var s = Mathf.Min(maxSpd - spd, accelCap * maxSpd);
                if (grounded || s > 0)
                {
                    Abdomen.ApplyForceToCenter(accFactor * s * SpideyPhysics.TotalMass * d);
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
            Abdomen.ApplyForceToCenter(SpideyPhysics.TotalMass * grip * d);//grip to steep slope
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
            var a = MathTools.PseudoAngle(SpideyPhysics.LevelRight, FlipInput && !grounded ? -balanceDirection : balanceDirection);
            f += a * (grounded ? balanceSpringForce : airborneBalanceSpringForce);
        }

        Abdomen.ApplyTorque(SpideyPhysics.TotalMass * f);
    }

    private void RotateAbdomen()
    {
        if (chargingJump)
        {
            SpideyPhysics.abdomenRotationFromBase = JumpAbdomenRotation();
        }
        else if (grappleScurrying)
        {
            SpideyPhysics.abdomenRotationFromBase = ScurryAbdomenRotation();
        }
        else if (SpideyPhysics.abdomenRotationFromBase.direction != Vector2.right)
        {
            SpideyPhysics.abdomenRotationFromBase = PhysicsRotate.identity;
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
            return PhysicsRotate.LerpRotation(PhysicsRotate.identity, scurryRotateMin, f);//MathTools.CheapRotationalLerpClamped(Vector2.right, scurryRotateMin.direction, f, out _);
        }
        return PhysicsRotate.identity;
    }

    private void RotateHead()
    {
        Vector2 g;
        if (grounded)
        {
            var p = groundMap.TrueClosestPoint((Vector2)headBoneHeightRefPoint.position, out var t, out _, out _);
            var n = FacingRight ?
                groundMap.AverageNormalFromCenter(t + headRotationMinPos, t + headRotationMaxPos)
                : groundMap.AverageNormalFromCenter(t - headRotationMaxPos, t - headRotationMinPos);
            g = n.CWPerp();
        }
        else
        {
            var right = Right;
            g = grapple.FreeHanging ? FreeHangingHeadRight(right) : right;
        }

        SpideyPhysics.SetHeadRotation(new PhysicsRotate(g));
    }

    private Vector2 FreeHangingHeadRight(Vector2 bodyRight)
    {
        var y =  bodyRight.y;
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
            jumpVerificationTimer = jumpVerificationTime;//needs to be first bc SetGround > OnTakeoff depends on VerifyingJump()
            groundednessRating = 0;
            SetGrounded(false);
            var jumpDir = JumpDirection();
            Abdomen.ApplyLinearImpulse(SpideyPhysics.TotalMass * JumpForce() * jumpDir, SpideyPhysics.HeightReferencePosition);
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
        return SpideyPhysics.FacingRight ?
            SpideyPhysics.AbdomenBaseRotationFromLevel.InverseMultiplyRotation(Abdomen.rotation).direction.CCWPerp()
            : SpideyPhysics.AbdomenBaseRotationFromLevel.MultiplyRotation(Abdomen.rotation).direction.CCWPerp();
        //return Up;//although you'll have to adjust when leaning
        //var u = AbdomenBoneUpInBaseLocalCoords();//quaternion version would compute a lot of extra trivial products
        //return u.x * Right + u.y * Up;
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

        Vector2 down = -Up;
        var v = Vector2.Dot(Abdomen.linearVelocity, down);
        var l = Vector2.Dot(p - HeightReferencePt, down) - EffectiveRideHeight;
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

        Abdomen.ApplyForceToCenter(SpideyPhysics.TotalMass * (f - heightSpringDamping * v - Vector2.Dot(Physics2D.gravity, down)) * down);
        //remove affect of gravity while height spring engaged, otherwise you will settle at a height which is off by -Vector2.Dot(Physics2D.gravity, down) / heightSpringForce
        //(meaning you will be under height when upright, and over height when upside down (which was causing feet to not reach ground while upside down))
        //(e.g. before ride height on flat ground was always off by +- 32/400 = 0.08)
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
                var castResult = Abdomen.world.CastRay(HeightReferencePt, -backupGroundPtRaycastLengthFactor * GroundMapRaycastLength * pt.normal, spiderPhysics.queryFilter);
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
            && groundednessRating > 0) /*|| spiderPhysics.HasContact()*/;
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
        groundMap.UpdateMap(Abdomen.world, spiderPhysics.queryFilter,
            HeightReferencePt,
            -Up,
            Right,
            GroundMapRaycastLength,
            FacingRight);
    }

    private void InitializeGroundMap()
    {
        groundMap.Initialize();
        UpdateGroundMap();
    }

    private void InitializeGroundData()
    {
        InitializeGroundMap();
        UpdateGroundednessRating();
        groundAnchorPt = groundMap.Center.point;
    }


    //LEGS

    private float FreeHangStrideMultiplier()
    {
        var y = Right.y;
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

        var groundVelocity = Vector2.Dot(Abdomen.linearVelocity, OrientedGroundDirection);
        legSynch.bodyGroundSpeedSign = (grounded && grapple.GrappleAnchored) || grapple.FreeHanging ? 1 : Mathf.Sign(groundVelocity);
        legSynch.absoluteBodyGroundSpeed = grounded ? Mathf.Abs(groundVelocity) : Mathf.Min(Abdomen.linearVelocity.magnitude, airborneLegSpeedMax);
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

        legCastDirection[0] = spiderPhysics.head.rotation.direction.CWPerp();
        legCastDirection[1] = spiderPhysics.LevelRight.direction.CWPerp();

        legSynch.UpdateAllLegs(Time.deltaTime, groundMap, grounded, legCastDirection,  simulateContactWeight);
    }
}