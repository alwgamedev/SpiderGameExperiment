using UnityEngine;

public class SpiderMovementController : MonoBehaviour
{
    [Header("Body")]
    [SerializeField] Transform abdomenBone;
    [SerializeField] Transform headBone;
    [SerializeField] Transform heightReferencePoint;
    [SerializeField] GrappleCannon grapple;

    [Header("Ground Data")]
    [SerializeField] LayerMask groundLayer;
    [SerializeField] float groundedExitToleranceFactor;
    [SerializeField] float groundedEntryToleranceFactor;
    [SerializeField] float groundDirectionSampleWidth;
    [SerializeField] float backupGroundPtRaycastLengthFactor;
    [SerializeField] float groundPtSlipRate;
    [SerializeField] float groundPtSlipThreshold;
    [SerializeField] float upcomingGroundDirectionMinPos;
    [SerializeField] float upcomingGroundDirectionMaxPos;
    [SerializeField] float failedGroundRaycastSmoothingRate;
    //[SerializeField] float grappleLandingVerificationTime;
    [SerializeField] GroundMap groundMap;

    [Header("Movement")]
    [SerializeField] float accelFactor;
    [SerializeField] float accelFactorFreeHanging;
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
    [SerializeField] float headRotationSpeed;
    [SerializeField] float groundedRotationSpeed;
    [SerializeField] float airborneRotationSpeed;
    [SerializeField] float balanceSpringForce;
    [SerializeField] float airborneBalanceSpringForce;
    [SerializeField] float balanceSpringDamping;
    [SerializeField] float airborneBalanceSpringDamping;
    [SerializeField] float grappleScurryResistanceMax;
    [SerializeField] float grappleScurryAngleMax;

    [Header("Jumping & Airborne")]
    [SerializeField] float jumpForce;
    [SerializeField] float jumpForceCrouchBoostRate;
    [SerializeField] float uphillJumpDirectionRotationRate;
    [SerializeField] float uphillJumpTakeoffRotationFraction;
    [SerializeField] float jumpVerificationTime;
    [SerializeField] float tapJumpVerificationTime; 
    [SerializeField] float crouchHeightFraction;
    [SerializeField] float crouchTime;
    [SerializeField] float crouchBoostMinProgress;
    [SerializeField] float crouchReleaseSpeedMultiplier;
    [SerializeField] float airborneLegAnimationTimeScale;
    [SerializeField] float airborneReverseLegAnimationTimeScale;
    [SerializeField] float airborneStrideMultiplier;
    [SerializeField] float strideMultiplierSmoothingRate;

    [Header("Free Hang")]
    [SerializeField] float freeHangEntryThreshold;
    [SerializeField] float weakFreeHangEntryThreshold;
    [SerializeField] float freeHangGroundedToleranceMultiplier;
    [SerializeField] float freeHangLegAngleMin;
    [SerializeField] float freeHangLegAngleSkew;
    [SerializeField] float freeHangHeadAngle;

    [Header("Thrusters")]
    [SerializeField] Thrusters thrusters;
    //[SerializeField] float thrustingGravityScale;

    Rigidbody2D rb;
    Collider2D headCollider;
    Collider2D abdomenCollider;
    Collider2D[] bodyCollisionBuffer = new Collider2D[1];
    ContactFilter2D bodyCollisionFilter;
    LegSynchronizer legSynchronizer;

    int moveInput;

    bool jumpInput;
    float jumpVerificationTimer;

    int orientation = 1;

    bool grounded;
    bool allGroundMapPtsHitGround;
    float groundednessTolerance;
    Vector2 groundDirection = Vector2.right;
    Vector2 upcomingGroundDirection = Vector2.right;
    GroundMapPt groundPt = new GroundMapPt(new(Mathf.Infinity, Mathf.Infinity), Vector2.up, Vector2.right, 0, Mathf.Infinity, null);
    //float grappleLandingVerificationTimer;

    float cosFreeHangLegAngleMin;
    float sinFreeHangLegAngleMin;
    float cosScurryAngleMax;
    float sinScurryAngleMax;

    float crouchProgress;//0-1

    bool grappleScurrying;

    bool thrustersCooldownWarningSent;

    bool freeHangInput;

    bool FacingRight => transform.localScale.x > 0;
    Vector2 OrientedRight => FacingRight ? transform.right : -transform.right;
    Vector2 OrientedGroundDirection => FacingRight ? groundDirection : -groundDirection;
    float PreferredBodyPosGroundHeight => transform.position.y - heightReferencePoint.position.y + preferredRideHeight;
    float MaxSpeed => grounded ? maxSpeed : maxSpeedAirborne;
    float GroundVelocity => Vector2.Dot(rb.linearVelocity, OrientedGroundDirection);
    bool StronglyGrounded => grounded && allGroundMapPtsHitGround;
    Vector2 GroundPtGroundDirection => groundPt.normal.CWPerp();
    float GrappleScurryResistance => Vector2.Dot(grapple.LastCarryForce, -OrientedGroundDirection);
    float GrappleScurryResistanceFraction => GrappleScurryResistance / grappleScurryResistanceMax;
    //float GroundedGroundednessTolerance => groundedExitToleranceFactor * preferredRideHeight;
    //float AirborneGroundednessTolerance => groundedEntryToleranceFactor * preferredRideHeight;
    //float ThrustingGroundednessTolerance => thrustingGroundedToleranceFactor * preferredRideHeight;

    public Thrusters Thrusters => thrusters;

    private void OnDrawGizmos()
    {
        if (Application.isPlaying)
        {
            groundMap.DrawGizmos();
        }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        legSynchronizer = GetComponent<LegSynchronizer>();

        cosFreeHangLegAngleMin = Mathf.Cos(freeHangLegAngleMin);
        sinFreeHangLegAngleMin = Mathf.Sin(freeHangLegAngleMin);
        cosScurryAngleMax = Mathf.Cos(grappleScurryAngleMax);
        sinScurryAngleMax = Mathf.Sin(grappleScurryAngleMax);

        thrusters.Initialize();

        //Time.timeScale = 0.25f;//useful for spotting issues
    }

    private void Start()
    {
        headCollider = headBone.GetComponent<Collider2D>();
        abdomenCollider = abdomenBone.GetComponent<Collider2D>();

        //rb.centerOfMass = heightReferencePoint.position - transform.position;

        legSynchronizer.Initialize(PreferredBodyPosGroundHeight, FacingRight);
        bodyCollisionFilter.NoFilter();
        bodyCollisionFilter.layerMask = groundLayer;
        InitializeGroundData();
    }

    private void Update()
    {
        CaptureInput();
    }

    private void LateUpdate()
    {
        RotateHead(Time.deltaTime);
        legSynchronizer.UpdateAllLegs(Time.deltaTime, groundMap);
    }

    private void FixedUpdate()
    {
        if (VerifyingJump())
        {
            jumpVerificationTimer -= Time.deltaTime;
        }
        //if (VerifyingGrappleLanding())
        //{
        //    if (!grapple.GrappleAnchored || moveInput == 0)
        //    {
        //        grappleLandingVerificationTimer = 0;
        //    }
        //    else
        //    {
        //        grappleLandingVerificationTimer -= Time.deltaTime;
        //    }
        //}

        UpdateGroundData();
        UpdateThrusters();
        grappleScurrying = StronglyGrounded && moveInput != 0 && grapple.GrappleAnchored;

        HandleMoveInput();
        HandleJumpInput();

        if (StronglyGrounded)//was strongly grdd
        {  
            UpdateHeightSpring();
        }
        Balance();

        //we don't need to do all of these in fixed update (e.g. step height fraction only needs to be update where crouchProgress is updated)
        //but for now it's just easier have them all here.
        //also may want to clearly identify the main states that affect this and just call appropriate methods from switch statement instead of checking the same bools multiple times per frame
        //(for now this is more flexible)
        var v = GroundVelocity;
        legSynchronizer.bodyGroundSpeedSign = grounded && grapple.GrappleAnchored ? 1 : Mathf.Sign(v);
        legSynchronizer.absoluteBodyGroundSpeed = grounded || thrusters.Engaged ? Mathf.Abs(v) : rb.linearVelocity.magnitude;
        legSynchronizer.preferredBodyPosGroundHeight = PreferredBodyPosGroundHeight;
        legSynchronizer.stepHeightFraction = 1 - crouchProgress * crouchHeightFraction;
        legSynchronizer.timeScale = grounded || thrusters.Engaged ? 1 : legSynchronizer.bodyGroundSpeedSign < 0 ? airborneReverseLegAnimationTimeScale : airborneLegAnimationTimeScale;
        if (!grounded)
        {
            legSynchronizer.strideMultiplier = MathTools.LerpAtConstantRate(legSynchronizer.strideMultiplier, AirborneStrideMultiplier(),
                strideMultiplierSmoothingRate, Time.deltaTime);
        }
        else if (legSynchronizer.strideMultiplier != 1)
        {
            legSynchronizer.strideMultiplier = 1;
        }

        legSynchronizer.driftWeight = (grapple.FreeHanging ? Mathf.Max(-OrientedRight.y, 0) : 0);
    }

    private float AirborneStrideMultiplier()
    {
        var y = transform.right.y;
        //using y > cosLegAngleMax b/c we really want sin(90 - legAngleMax)
        return y > cosFreeHangLegAngleMin ? 1 :
            y > 0 ? Mathf.Lerp(airborneStrideMultiplier, 1, y / cosFreeHangLegAngleMin)
            : Mathf.Lerp(airborneStrideMultiplier, 1, -y);
    }


    //THRUSTERS

    //do before you handle any move input
    //i want to do in fixed update, because 
    private void UpdateThrusters()
    {
        switch(thrusters.Update(Time.deltaTime))
        {
            case Thrusters.ThrustersUpdateResult.ChargeRanOut:
                OnThrustersRanOutOfCharge();
                if (!StronglyGrounded && grapple.GrappleAnchored)
                {
                    grapple.FreeHanging = true;
                }
                break;
            case Thrusters.ThrustersUpdateResult.CooldownEnded:
                OnThrustersCooldownEnded();
                UpdateThrustersEngagement();
                break;
            case Thrusters.ThrustersUpdateResult.None:
                UpdateThrustersEngagement();
                break;

        }
    }

    private void UpdateThrustersEngagement()
    {
        if (thrusters.Engaged)
        {
            if (grounded || grapple.FreeHanging || moveInput == 0)
            {
                DisengageThrusters();
            }
        }
        else if (!grounded && moveInput != 0 && !freeHangInput)
        {
            TryEngageThrusters();
        }
    }

    private void TryEngageThrusters()
    {
        if (thrusters.Engage())
        {
            OnThrustersEngaged();
        }
        else
        {
            OnThrustersEngageFailed();
        }
    }

    private void DisengageThrusters()
    {
        thrusters.Disengage();
        OnThrustersDisengaged();
    }

    private void OnThrustersRanOutOfCharge()
    {
        //Debug.Log("thrusters ran out of charge");
        OnThrustersDisengaged();
        //if (grapple.GrappleAnchored && !StronglyGrounded)
        //{
        //    grapple.FreeHanging = true;
        //}
    }

    private void OnThrustersCooldownEnded()
    {
        //Debug.Log("thrusters cooldown ended");
    }

    private void OnThrustersEngaged()
    {
        //Debug.Log("engaging thrusters!");
        //rb.gravityScale = thrustingGravityScale;
    }

    private void OnThrustersEngageFailed()
    {
        if (!thrustersCooldownWarningSent)
        {
            //Debug.Log("thrusters on cooldown...");
            thrustersCooldownWarningSent = true;
        }
    }

    private void OnThrustersDisengaged()
    {
        //Debug.Log("disengaging thrusters.");
        //rb.gravityScale = 1;
    }

    //INPUT

    private void CaptureInput()
    {
        //2do: could UpdateCrouch in fixed update (so that crouch is accurate every physics update)
        //which means we would just record a state here (crouching, releasingCrouch, or notCrouching)
        //or we could set the crouchSpeed instead (1, -crouchReleaseSpeedMultiplier, or 0)
        if (grounded && Input.GetKey(KeyCode.Space))
        {
            if (crouchProgress < 1)
            {
                UpdateCrouch(Time.deltaTime);//could move this to fixed update
            }
        }
        else
        {
            if (!jumpInput && crouchProgress > 0)//don't start releasing crouch until jump input gets handled! (bc crouch progress affects jump force)
            {
                UpdateCrouch(crouchReleaseSpeedMultiplier * -Time.deltaTime);
            }
            if (!jumpInput && grounded)
            {
                jumpInput = Input.GetKeyUp(KeyCode.Space);//handle jump input on next fixed update
            }
        }

        moveInput = (Input.GetKey(KeyCode.RightArrow) ? 1 : 0) + (Input.GetKey(KeyCode.LeftArrow) ? -1 : 0);

        freeHangInput = grapple.GrappleAnchored && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));

        //make sure you update freeHanging before changing direction
        if (grapple.GrappleAnchored)
        {
            //hold shift to enter freehang/swing mode (if it's the default state, then repelling and direction change gets annoying)
            grapple.FreeHanging = (moveInput == 0 || freeHangInput || thrusters.Cooldown)
                && !(StronglyGrounded || IsTouchingGroundPtCollider(headCollider) || IsTouchingGroundPtCollider(abdomenCollider));
        }

        if (MathTools.OppositeSigns(moveInput, orientation))
        {
            ChangeDirection();
        }

        if (thrustersCooldownWarningSent && moveInput == 0)
        {
            thrustersCooldownWarningSent = false;
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

    private void ChangeDirection()
    {
        //2do: can we interpolate between these by free hang strength in a reasonable way (or is there no need to do that)
        if (grounded || !grapple.GrappleAnchored)
        {
            var s = transform.localScale;
            transform.localScale = new Vector3(-s.x, s.y, s.z);
        }
        else
        {
            Vector2 o = grapple.FreeHangLeveragePoint;
            var s = transform.localScale;
            transform.localScale = new Vector3(-s.x, s.y, s.z);
            var n = grapple.FreeHanging ? grapple.LastCarryForceDirection.CWPerp() : Vector2.right;//normal to the hyperplane we're reflecting over
            transform.up = MathTools.ReflectAcrossHyperplane((Vector2)transform.up, n);
            transform.position += (Vector3)(o - grapple.FreeHangLeveragePoint);
        }

        orientation = FacingRight ? 1 : -1;
    }

    private void HandleMoveInput()
    {
        //accelCap bc otherwise if speed is highly negative, we get ungodly rates of acceleration
        //(and note that we are doing it in a way that scales with max speed
        //-- so you can limit maxSpd - spd to being e.g. double the maxSpeed or w/e)
        if (moveInput != 0)
        {
            if (grounded || thrusters.Engaged)
            {
                Vector2 d = OrientedGroundDirection;
                var spd = Vector2.Dot(rb.linearVelocity, d);
                var maxSpd = MaxSpeed;
                var accFactor = grounded ? accelFactor : thrustingAccelFactor;

                var s = Mathf.Min(maxSpd - spd, accelCap * maxSpd);
                if (grounded || s > 0)
                {
                    rb.AddForce(accFactor * s * rb.mass * d);
                }
            }
            else if (freeHangInput)
            {
                rb.AddForceAtPosition(rb.mass * (accelFactorFreeHanging * FreeHangingMoveDirection()), grapple.FreeHangLeveragePoint);
            }
        }
        else if (StronglyGrounded)
        {
            Vector2 d = FacingRight ? GroundPtGroundDirection : -GroundPtGroundDirection;
            var spd = Vector2.Dot(rb.linearVelocity, d);
            var h = Vector2.Dot(groundPt.point - (Vector2)heightReferencePoint.position, d);
            var grip = gripStrength * h;
            if (grapple.GrappleAnchored)
            {
                //so grip doesn't fight against carry force
                var c = Vector2.Dot(grapple.LastCarryForce, d);
                if (MathTools.OppositeSigns(grip, c))
                {
                    grip += grapplePullGripReduction * c;
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
            var a = MathTools.PseudoAngle(transform.right, grappleScurrying ? ScurryAngle() : groundDirection);
            f += a * (grounded ? balanceSpringForce : airborneBalanceSpringForce);
        }

        rb.AddTorque(rb.mass * f);
    }

    private Vector2 ScurryAngle()
    {
        var f = GrappleScurryResistanceFraction;
        if (f > 0)
        {
            return MathTools.CheapRotationalLerpClamped(groundDirection, ScurryAngleMax(), f, out _);
        }
        return groundDirection;
    }

    private Vector2 ScurryAngleMax()
    {
        return cosScurryAngleMax * groundDirection + sinScurryAngleMax * (FacingRight ? groundDirection.CWPerp() : groundDirection.CCWPerp());
    }

    private void RotateHead(float dt)
    {
        var g = grounded ? upcomingGroundDirection : (grapple.FreeHanging ? FreeHangingHeadRight() : (Vector2)transform.right);
        headBone.ApplyCheapRotationLerpClamped(g, headRotationSpeed * dt);//if rotate at constant speed, it starts to flicker when rotation is small

        //g = MathTools.CheapRotationalLerpClamped(headBone.right, g, headRotationSpeed * dt);
        //headBone.rotation = MathTools.QuaternionFrom2DUnitVector(g);
        //setting headBone.right instead of headBone.rotation occasionally causes flickering
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
        if (jumpInput)
        {
            jumpInput = false;
            jumpVerificationTimer = jumpVerificationTime;//needs to be first bc SetGround > OnTakeoff depends on VerifyingJump()
            SetGrounded(false);
            var jumpDir = JumpDirection();
            rb.AddForce(rb.mass * JumpForce() * jumpDir, ForceMode2D.Impulse);
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
        return transform.up;
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
        var f = l * heightSpringForce - heightSpringDamping * v;
        if (grapple.GrappleAnchored)
        {
            var dot = Vector2.Dot(grapple.LastCarryForce, down);
            if (dot < 0 && l > 0)
            {
                f += l > heightSpringBreakThreshold ? dot : l / heightSpringBreakThreshold * dot;
            }
            else if (dot > 0 && l < 0)
            {
                f -= grappleSquatReduction * dot;//fight grapple a little when it's pulling you into the ground
            }
        }
        rb.AddForce(rb.mass * (f - Vector2.Dot(Physics2D.gravity, down)) * down);
        //remove affect of gravity while height spring engaged, otherwise you will settle at a height which is off by -Vector2.Dot(Physics2D.gravity, down) / heightSpringForce
        //(meaning you will be under height when upright, and over height when upside down (which was causing feet to not reach ground while upside down))
        //(e.g. before ride height on flat ground was always off by plus or minus 32/400 = 0.08)
    }

    //private void UpdateAirborneLegDrift()
    //{
    //    if (legSynchronizer.outwardDrift < airborneLegDriftMax)
    //    {
    //        legSynchronizer.outwardDrift += airborneLegDriftRate * Time.deltaTime;
    //    }
    //}


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

    private void RecomputeGroundednessTolerance()
    {
        groundednessTolerance = (grounded ? groundedExitToleranceFactor : groundedEntryToleranceFactor) * preferredRideHeight;
        //rb.centerOfMass = heightReferencePoint.position - groundednessTolerance * transform.up - transform.position;
    }

    private void OnTakeOff()
    {
        //legSynchronizer.outwardDrift = 0;
        RecomputeGroundednessTolerance();
    }

    private void OnLanding()
    {
        //legSynchronizer.outwardDrift = 0;
        RecomputeGroundednessTolerance();
        //if (grapple.GrappleAnchored && moveInput != 0)
        //{
        //    grappleLandingVerificationTimer = grappleLandingVerificationTime;
        //}
    }

    //private bool VerifyingGrappleLanding()
    //{
    //    return grappleLandingVerificationTimer > 0;
    //}

    private void UpdateGroundData()
    {
        UpdateGroundMap();
        var i = groundMap.IndexOfFirstGroundHitFromCenter();
        var pt = groundMap[i];
        var isCentralIndex = groundMap.IsCentralIndex(i);
        var goalGroundDirection = pt.hitGround && isCentralIndex ?
            groundMap.AverageNormalFromCenter(-groundDirectionSampleWidth, groundDirectionSampleWidth).CWPerp()
            : pt.normal.CWPerp();

        if (!VerifyingJump())
        {
            SetGrounded(pt.hitGround);
        }

        if (moveInput != 0 || !StronglyGrounded)
        //updating when !StronglyGrounded allows groundpt to slip to an accurate position while settling down on landing
        {
            UpdateGroundPoint();
        }
        else
        {
            var l = Vector2.Dot(groundPt.point - pt.point, goalGroundDirection);
            if (l > groundPtSlipThreshold || l < -groundPtSlipThreshold)
            {
                var v = Vector2.Dot(rb.linearVelocity, goalGroundDirection);
                if (MathTools.OppositeSigns(l, v))
                {
                    l = l < 0 ? Mathf.Min(l - groundPtSlipRate * l * v, 0) : Mathf.Max(l + groundPtSlipRate * l * v, 0);
                    groundPt = groundMap.PointFromCenterByPositionClamped(l);//project onto ground method really does the same thing
                }
            }
        }

        void UpdateGroundPoint()
        {
            if (pt.hitGround && !isCentralIndex)
            {
                var r = Physics2D.Raycast(heightReferencePoint.position, -pt.normal,
                    backupGroundPtRaycastLengthFactor * groundednessTolerance, groundLayer);
                if (r)
                {
                    pt = new GroundMapPt(r.point, r.normal, r.normal.CWPerp(), 0, 0, r.collider);
                    goalGroundDirection = pt.normal.CWPerp();
                }
            }

            groundPt = pt;
        }

        groundDirection = grounded ? goalGroundDirection
            : MathTools.CheapRotationBySpeed(groundDirection, Vector2.right, failedGroundRaycastSmoothingRate, Time.deltaTime, out _);
        upcomingGroundDirection = FacingRight ? groundMap.AverageNormalFromCenter(upcomingGroundDirectionMinPos, upcomingGroundDirectionMaxPos)
            : groundMap.AverageNormalFromCenter(-upcomingGroundDirectionMaxPos, -upcomingGroundDirectionMinPos);
        upcomingGroundDirection = upcomingGroundDirection.CWPerp();
    }

    private void UpdateGroundMap()
    {
        if (grapple.FreeHanging)
        {
            groundMap.UpdateMap(heightReferencePoint.position,
                FreeHangGroundMapDown(),
                FreeHangGroundMapRight(),
                freeHangGroundedToleranceMultiplier * groundednessTolerance,
                groundMap.CentralIndex, 
                groundLayer);
            allGroundMapPtsHitGround = groundMap.AllHitGround();
        }
        else
        {
            groundMap.UpdateMap(heightReferencePoint.position, 
                -transform.up,  
                grappleScurrying ? groundDirection : transform.right, 
                groundednessTolerance,
                groundMap.CentralIndex, 
                groundLayer);
            allGroundMapPtsHitGround = groundMap.AllHitGround();
        }
    }

    private Vector2 FreeHangGroundMapDown()
    {
        var r = OrientedRight;
        if (r.y > 0)
        {
            return -transform.up;
        }

        if (MathTools.OppositeSigns(r.x, orientation))//upside down
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

        y *= FacingRight? -freeHangLegAngleSkew : freeHangLegAngleSkew;//to get -/+ tUp based on FacingRight
        return Mathf.Cos(y) * transform.right + Mathf.Sin(y) * transform.up;
    }

    //only used for releasing freeHang state, but we'll put it in this section I guess
    private bool IsTouchingGroundPtCollider(Collider2D c)
    {
        if (groundPt.groundCollider == null)
        {
            return false;
        }

        c.GetContacts(bodyCollisionFilter, bodyCollisionBuffer);
        return bodyCollisionBuffer[0] == groundPt.groundCollider;
    }

    private void InitializeGroundPoint()
    {
        groundPt = groundMap.Center;
    }

    private void InitializeGroundData()
    {
        RecomputeGroundednessTolerance();
        UpdateGroundMap();
        InitializeGroundPoint();
    }
}