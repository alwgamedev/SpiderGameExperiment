using UnityEngine;

public class SpiderMovementController : MonoBehaviour
{
    [SerializeField] Transform abdomenBone;
    [SerializeField] Transform headBone;
    [SerializeField] Transform heightReferencePoint;
    [SerializeField] SilkGrapple grapple;
    [SerializeField] float headRotationSpeed;
    [SerializeField] float groundedRotationSpeed;
    [SerializeField] float airborneRotationSpeed;
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
    [SerializeField] float accelFactor;
    [SerializeField] float accelFactorFreeHanging;
    [SerializeField] float accelCap;
    [SerializeField] float decelFactor;
    [SerializeField] float gripStrength;
    [SerializeField] float grapplePullGripReduction;
    [SerializeField] float maxSpeed;
    [SerializeField] float maxSpeedAirborne;
    [SerializeField] float preferredRideHeight;
    [SerializeField] float heightSpringForce;
    [SerializeField] float heightSpringDamping;
    [SerializeField] float heightSpringBreakThreshold;
    [SerializeField] float grappleSquatReduction;
    [SerializeField] float heightSampleWidth;//2do: scale with spider size (and other fields)
    [SerializeField] float balanceSpringForce;
    [SerializeField] float airborneBalanceSpringForce;
    [SerializeField] float balanceSpringDamping;
    [SerializeField] float jumpForce;
    [SerializeField] float jumpForceCrouchBoostRate;
    [SerializeField] float uphillJumpDirectionRotationRate;
    [SerializeField] float uphillJumpTakeoffRotationFraction;
    [SerializeField] float jumpVerificationTime;
    [SerializeField] float tapJumpVerificationTime;
    [SerializeField] float freeHangEntryThreshold;
    [SerializeField] float weakFreeHangEntryThreshold;
    [SerializeField] float freeHangGroundedToleranceMultiplier;
    [SerializeField] float freeHangLegAngleMin;
    [SerializeField] float freeHangLegAngleSkew;
    [SerializeField] float freeHangHeadAngle;
    //[SerializeField] float freeHangStepHeightMultiplier;
    [SerializeField] float crouchHeightFraction;
    [SerializeField] float crouchTime;
    [SerializeField] float crouchBoostMinProgress;
    [SerializeField] float crouchReleaseSpeedMultiplier;
    [SerializeField] float airborneLegAnimationTimeScale;
    [SerializeField] float airborneStrideMultiplier;
    [SerializeField] float strideMultiplierSmoothingRate;
    [SerializeField] GroundMap groundMap;

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
    float groundednessTolerance;
    Vector2 groundDirection = Vector2.right;
    Vector2 upcomingGroundDirection = Vector2.right;
    GroundMapPt groundPt = new GroundMapPt(new(Mathf.Infinity, Mathf.Infinity), Vector2.up, Vector2.right, 0, Mathf.Infinity, null);
    //Vector2 groundMapDown;

    float cosLegAngleMin;
    float sinLegAngleMin;

    bool allGroundMapPtsHitGround;

    float crouchProgress;//0-1

    bool FacingRight => transform.localScale.x > 0;
    Vector2 OrientedRight => FacingRight ? transform.right : -transform.right;
    Vector2 OrientedGroundDirection => FacingRight ? groundDirection : -groundDirection;
    float PreferredBodyPosGroundHeight => transform.position.y - heightReferencePoint.position.y + preferredRideHeight;
    float MaxSpeed => grounded ? maxSpeed : maxSpeedAirborne;
    float GroundVelocity => Vector2.Dot(rb.linearVelocity, OrientedGroundDirection);
    //float Speed => grounded ? Mathf.Abs(Vector2.Dot(rb.linearVelocity, groundDirection)) : rb.linearVelocity.magnitude;
    bool StronglyGrounded => grounded && allGroundMapPtsHitGround;
    Vector2 GroundPtGroundDirection => groundPt.normal.CWPerp();


    //private void OnDrawGizmos()
    //{
    //    if (Application.isPlaying)
    //    {
    //        groundMap.DrawGizmos();
    //    }
    //    if (groundPoint.x != Mathf.Infinity)
    //    {
    //        Gizmos.color = Color.cyan;
    //        Gizmos.DrawSphere(groundPoint, 0.1f);
    //    }
    //}

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        legSynchronizer = GetComponent<LegSynchronizer>();

        cosLegAngleMin = Mathf.Cos(freeHangLegAngleMin);
        sinLegAngleMin = Mathf.Sin(freeHangLegAngleMin);

        //Time.timeScale = 0.25f;//useful for spotting issues
    }

    private void Start()
    {
        headCollider = headBone.GetComponent<Collider2D>();
        abdomenCollider = abdomenBone.GetComponent<Collider2D>();
        legSynchronizer.Initialize(PreferredBodyPosGroundHeight, FacingRight);
        bodyCollisionFilter.NoFilter();
        bodyCollisionFilter.layerMask = groundLayer;
        InitializeGroundData();
    }

    private void Update()
    {
        CaptureInput();
        RotateHead(Time.deltaTime);
    }

    private void FixedUpdate()
    {
        if (VerifyingJump())
        {
            jumpVerificationTimer -= Time.deltaTime;
        }

        UpdateGroundData();
        //if (grapple.GrappleAnchored)
        //{
        //    UpdateFreeHangingState();
        //}
        HandleMoveInput();
        HandleJumpInput();

        if (StronglyGrounded)
        {
            UpdateHeightSpring();
        }
        Balance();

        var v = GroundVelocity;
        legSynchronizer.bodyGroundSpeedSign = grounded && grapple.GrappleAnchored ? 1 : Mathf.Sign(v);
        legSynchronizer.absoluteBodyGroundSpeed = grounded ? Mathf.Abs(v) : rb.linearVelocity.magnitude;
        legSynchronizer.preferredBodyPosGroundHeight = PreferredBodyPosGroundHeight;
        legSynchronizer.stepHeightFraction = 1 - crouchProgress * crouchHeightFraction;
        //if (grapple.FreeHanging)
        //{
        //    legSynchronizer.stepHeightFraction *= freeHangStepHeightMultiplier;
        //}
        if (grapple.FreeHanging)
        {
            legSynchronizer.strideMultiplier = MathTools.LerpAtConstantRate(legSynchronizer.strideMultiplier, AirborneStrideMultiplier(),
                strideMultiplierSmoothingRate, Time.deltaTime);
        }
        else if (legSynchronizer.strideMultiplier != 1)
        {
            legSynchronizer.strideMultiplier = 1;
        }

        legSynchronizer.UpdateAllLegs(Time.deltaTime, groundMap);
        //when done in late update get weird things like legs lagging behind (up) during long freefalls.
        //we can do it on one fixed update per update, but then speed is not always accurate, so get moonwalking.
    }

    private float AirborneStrideMultiplier()
    {
        var clampedRotation = ClampedRotation();
        //using cosLegAngleMax b/c we really want sin(90 - legAngleMax)
        return clampedRotation > cosLegAngleMin ? 1 :
            clampedRotation > 0 ? Mathf.Lerp(airborneStrideMultiplier, 1, clampedRotation / cosLegAngleMin)
            : Mathf.Lerp(airborneStrideMultiplier, 1, -clampedRotation);
    }

    private void CaptureInput()
    {
        if (grounded && Input.GetKey(KeyCode.Space))
        {
            if (crouchProgress < 1)
            {
                UpdateCrouch(Time.deltaTime);
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

        //make sure you update before changing direction
        if (grapple.GrappleAnchored)
        {
            grapple.FreeHanging = (moveInput == 0 || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                && !(StronglyGrounded || IsTouchingGroundPtCollider(headCollider) || IsTouchingGroundPtCollider(abdomenCollider));
        }

        if (MathTools.OppositeSigns(moveInput, orientation))
        {
            ChangeDirection();
        }
    }

    private void ChangeDirection()
    {
        //2do: can we interpolate between these by free hang strength in a reasonable way (or is there no need to do that)
        if (!grapple.FreeHanging)
        {
            var s = transform.localScale;
            transform.localScale = new Vector3(-s.x, s.y, s.z);
        }
        else
        {
            Vector2 o = grapple./*Smoothed*/FreeHangLeveragePoint;
            var s = transform.localScale;
            transform.localScale = new Vector3(-s.x, s.y, s.z);
            var n = grapple.LastCarryForceDirection.CCWPerp();
            transform.up = MathTools.ReflectAcrossHyperplane((Vector2)transform.up, n);
            //groundMapDown = MathTools.ReflectAcrossHyperplane(groundMapDown, n);
            transform.position += (Vector3)(o - grapple./*Smoothed*/FreeHangLeveragePoint);
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
            if (grapple.FreeHanging)
            {
                rb.AddForceAtPosition(rb.mass * accelFactorFreeHanging * FreeHangingMoveDirection(), grapple.FreeHangLeveragePoint);
                return;
            }

            Vector2 d = OrientedGroundDirection;
            var spd = Vector2.Dot(rb.linearVelocity, d);
            var maxSpd = MaxSpeed;

            var accCap = accelCap;
            var accFactor = accelFactor;
            //if (VerifyingJump())
            //{
            //    var lerpAmt = 1 - Mathf.Pow(jumpVerificationTimer / jumpVerificationTime, 1);
            //    maxSpd = JumpVerificationMaxSpeed(lerpAmt);
            //    accCap = JumpVerificationAccelCap(lerpAmt);
            //    accFactor = JumpVerificationAccelFactor(accFactor, lerpAmt);
            //}

            var s = Mathf.Min(maxSpd - spd, accCap * maxSpd);
            if (grounded || s > 0)
            {
                rb.AddForce(accFactor * s * rb.mass * d);
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

    //private float JumpVerificationAccelFactor(float accelFactor, float lerpAmt)
    //{
    //    return Mathf.Lerp(0, accelFactor, lerpAmt);
    //}

    //private float JumpVerificationAccelCap(float lerpAmt)
    //{
    //    return Mathf.Lerp(0, accelCap, lerpAmt);
    //}

    //private float JumpVerificationMaxSpeed(float lerpAmt)
    //{
    //    return Mathf.Lerp(maxSpeed, maxSpeedAirborne, lerpAmt);
    //}

    private void Balance()
    {
        var f = -balanceSpringDamping * rb.angularVelocity;

        if (!grapple.FreeHanging)
        {
            //var x = Vector2.Dot(transform.up, groundDirection);
            //var y = Vector2.Dot(transform.up, groundDirection.CCWPerp());
            //if (y > 1)//in case of rounding errors
            //{
            //    y = 1;
            //}
            //x = x < 0 ? -Mathf.Sqrt(0.5f * (1 - y)) : Mathf.Sqrt(0.5f * (1 - y));
            ////result is x = cos(0.5f(t + pi/2)), which smoothly decreases from 1 to -1 as t goes from -pi/2 to 3pi/2
            ////where t is angle between transform.up and groundDirection
            var a = MathTools.PseudoAngle(transform.right, groundDirection);
            f += a * (grounded ? balanceSpringForce : airborneBalanceSpringForce);
        }

        rb.AddTorque(rb.mass * f);
    }

    //only called when !grounded
    private void UpdateFreeHangingState()
    {
        if (grapple.FreeHanging && (StronglyGrounded || !grapple.GrappleAnchored || IsTouchingGroundPtCollider(headCollider) || IsTouchingGroundPtCollider(abdomenCollider)))
        {
            grapple.FreeHanging = false;
            legSynchronizer.strideMultiplier = 1;
        }
        else if (!grapple.FreeHanging && !grounded && grapple.GrappleAnchored
            && grapple.LastCarryForceMagnitude / rb.mass > (moveInput != 0 ? freeHangEntryThreshold : weakFreeHangEntryThreshold)
            && !VerifyingJump())
        {
            grapple.FreeHanging = true;
            legSynchronizer.strideMultiplier = airborneStrideMultiplier;
        }
    }

    //private bool AboveAnchorGround()
    //{
    //    var r = Physics2D.Raycast(heightReferencePoint.position, Vector2.down, freeHangDownCastLengthFactor * preferredRideHeight, grapple.AnchorMask);
    //    return r && r.collider == grapple.AnchorCollider;
    //}

    //private bool AnchorGroundIsOverhead()
    //{
    //    var r = Physics2D.Raycast(heightReferencePoint.position, Vector2.up, freeHangDownCastLengthFactor * grapple.Grapple.Length, grapple.AnchorMask);
    //    return r && r.collider == grapple.AnchorCollider;
    //}

    private bool IsTouchingGroundPtCollider(Collider2D c)
    {
        if (groundPt.groundCollider == null)
        {
            return false;
        }

        c.GetContacts(bodyCollisionFilter, bodyCollisionBuffer);
        return bodyCollisionBuffer[0] == groundPt.groundCollider;
    }

    //pass negative dt when reversing crouch
    private void UpdateCrouch(float dt)
    {
        var progressDelta = dt / crouchTime;
        progressDelta = Mathf.Clamp(progressDelta, -crouchProgress, 1 - crouchProgress);
        crouchProgress += progressDelta;
        abdomenBone.position -= progressDelta * crouchHeightFraction * preferredRideHeight * transform.up;
    }

    private void RotateHead(float dt)
    {
        var g = grounded ? upcomingGroundDirection : (grapple.FreeHanging ? FreeHangingHeadRight() : (Vector2)transform.right);
        headBone.transform.right = MathTools.CheapRotationBySpeed(headBone.transform.right, g, headRotationSpeed, dt);
    }

    private Vector2 FreeHangingHeadRight()
    {
        var f = ClampedRotation();
        if (f < 0)
        {
            f = -f * freeHangHeadAngle;
            return Mathf.Cos(f) * transform.right + Mathf.Sin(f) * (FacingRight ? transform.up : -transform.up);
        }
        return transform.right;
    }

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
        //(e.g. before ride height on flat ground was always off by plus or minus 400/32 = 0.08)
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
    }

    private void OnTakeOff()
    {
        legSynchronizer.timeScale = airborneLegAnimationTimeScale;
        legSynchronizer.outwardDrift = 0;
        RecomputeGroundednessTolerance();
    }

    private void OnLanding()
    {
        legSynchronizer.timeScale = 1;
        legSynchronizer.outwardDrift = 0;
        RecomputeGroundednessTolerance();
    }

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
                    groundPt = groundMap.PointFromCenterByPositionClamped(l);//project onto ground does the same thing
                    //var groundPointGroundDirection = n.CWPerp();
                    //groundPt = new(groundPt, n, 0, 0)
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
                    //ptRight = pt.normal.CWPerp();
                    goalGroundDirection = pt.normal.CWPerp();
                }
            }

            groundPt = pt;
        }

        groundDirection = grounded ? goalGroundDirection
            : MathTools.CheapRotationBySpeed(groundDirection, Vector2.right, failedGroundRaycastSmoothingRate, Time.deltaTime);
        upcomingGroundDirection = FacingRight ? groundMap.AverageNormalFromCenter(upcomingGroundDirectionMinPos, upcomingGroundDirectionMaxPos)
            : groundMap.AverageNormalFromCenter(-upcomingGroundDirectionMaxPos, -upcomingGroundDirectionMinPos);
        upcomingGroundDirection = upcomingGroundDirection.CWPerp();
    }

    private void UpdateGroundMap()
    {
        //groundMapDown = grapple.FreeHanging ? MathTools.CheapRotationBySpeed(groundMapDown, FreeHangGroundMapDown(), freeHangGMDownSmoothingRate, Time.deltaTime) : -transform.up;
        if (grapple.FreeHanging)
        {
            var f = ClampedRotation();
            groundMap.UpdateMap(heightReferencePoint.position,
                FreeHangGroundMapDown(f),
                FreeHangGroundMapRight(f),
                freeHangGroundedToleranceMultiplier * groundednessTolerance,
                groundMap.CentralIndex, 
                groundLayer);
            allGroundMapPtsHitGround = groundMap.AllHitGround();
        }
        else
        {
            groundMap.UpdateMap(heightReferencePoint.position, 
                -transform.up,  
                transform.right, 
                groundednessTolerance,
                groundMap.CentralIndex, 
                groundLayer);
            allGroundMapPtsHitGround = groundMap.AllHitGround();
        }
    }

    private Vector2 FreeHangGroundMapDown(float clampedRotation)
    {
        return clampedRotation == Mathf.Infinity ? -transform.up :
            clampedRotation > cosLegAngleMin ? -transform.up : clampedRotation < -cosLegAngleMin ?  cosLegAngleMin * OrientedRight - sinLegAngleMin * (Vector2)transform.up : Vector2.down;
    }

    private Vector2 FreeHangGroundMapRight(float clampedRotation)
    {
        if (clampedRotation < 0)
        {
            clampedRotation = -clampedRotation * freeHangLegAngleSkew;
            return Mathf.Cos(clampedRotation) * transform.right + Mathf.Sin(clampedRotation) * (FacingRight ? -transform.up : transform.up);
        }
        return transform.right;
    }

    private float ClampedRotation()
    {
        return transform.up.y < 0 ? Mathf.Infinity : OrientedRight.y;
    }

    private void InitializeGroundPoint()
    {
        //groundPoint = groundMap.Center.point;
        //groundPointGroundDirection = groundMap.Center.normal.CWPerp();
        groundPt = groundMap.Center;
    }

    private void InitializeGroundData()
    {
        RecomputeGroundednessTolerance();
        UpdateGroundMap();
        InitializeGroundPoint();
    }
}