using System.Collections.Generic;
using UnityEngine;

public class SpiderController : MonoBehaviour
{
    [SerializeField] Transform abdomenBone;
    [SerializeField] Transform headBone;
    [SerializeField] Transform heightReferencePoint;
    [SerializeField] float headRotationSpeed;
    [SerializeField] float rotationSpeed;
    [SerializeField] float groundedExitToleranceFactor;
    [SerializeField] float groundedEntryToleranceFactor;
    [SerializeField] float predictiveGroundDirectionSpacing;
    [SerializeField] float failedGroundRaycastSmoothingRate;
    //[SerializeField] float upcomingGroundDirectionSmoothingRate;
    [SerializeField] float accelFactor;
    [SerializeField] float accelCap;
    [SerializeField] float decelFactor;
    [SerializeField] float airborneAccelMultiplier;
    [SerializeField] float steepSlopeGripStrength;
    [SerializeField] float steepSlopeGripDistancePower;
    [SerializeField] float maxSpeed;
    [SerializeField] float maxSpeedAirborne;
    [SerializeField] float preferredRideHeight;
    [SerializeField] float heightSpringForce;
    [SerializeField] float heightSpringDamping;
    [SerializeField] float balanceSpringForce;
    [SerializeField] float balanceSpringDamping;
    [SerializeField] float jumpForce;
    [SerializeField] float jumpForceCrouchBoostRate;
    [SerializeField] float uphillJumpDirectionRotationRate;
    [SerializeField] float uphillJumpTakeoffRotationFraction;
    [SerializeField] float jumpVerificationTime;
    [SerializeField] float crouchHeightFraction;
    [SerializeField] float crouchTime;
    [SerializeField] float crouchBoostMinProgress;
    [SerializeField] float crouchReleaseSpeedMultiplier;
    [SerializeField] float airborneLegAnimationTimeScale;
    [SerializeField] float airborneLegDriftRate;
    [SerializeField] float airborneLegDriftMax;
    [SerializeField] GroundMap groundMap;

    LegSynchronizer legSynchronizer;
    Rigidbody2D rb;
    int moveInput;

    bool jumpInput;
    float jumpVerificationTimer;

    bool grounded;
    float groundednessTolerance;
    //Vector2 predictiveGroundDirection;
    Vector2 groundDirection = Vector2.right;
    Vector2 upcomingGroundDirection = Vector2.right;
    Vector2 groundPoint = new(Mathf.Infinity, Mathf.Infinity);
    //Vector2 groundSlipPoint;
    //to force it to initialize ground point (and then after it only gets set when moveInput != 0)
    //float lastComputedGroundDistance = Mathf.Infinity;
    //use infinity instead of NaN, because equals check always fails for NaN (even if you check NaN == NaN)

    float crouchProgress;//0-1

    //int groundLayer;

    bool FacingRight => transform.localScale.x > 0;
    int Orientation => FacingRight ? 1 : -1;
    //float GroundRaycastLength => groundRaycastLengthFactor * preferredRideHeight;
    //float GroundednessTolerance => (grounded ? groundedExitToleranceFactor : groundedEntryToleranceFactor) * preferredRideHeight;
    float PreferredBodyPosGroundHeight => transform.position.y - heightReferencePoint.position.y + preferredRideHeight;

    private void OnDrawGizmos()
    {
        groundMap.DrawGizmos();
    }

    private void Awake()
    {
        legSynchronizer = GetComponent<LegSynchronizer>();
        rb = GetComponent<Rigidbody2D>();
        //groundLayer = LayerMask.GetMask("Ground");

        //Time.timeScale = 0.25f;//useful for spotting issues
    }

    private void Start()
    {
        //rb.centerOfMass = (Vector2)heightReferencePoint.position - rb.centerOfMass;
        legSynchronizer.Initialize(PreferredBodyPosGroundHeight, FacingRight);
        RecomputeGroundednessTolerance();
    }

    private void Update()
    {
        if (VerifyingJump())
        {
            jumpVerificationTimer -= Time.deltaTime;
        }

        CaptureInput();

        //if (!grounded)
        //{
        //    UpdateAirborneLegDrift();
        //}

        RotateHead(Time.deltaTime);
        Balance(Time.deltaTime);
    }

    private void LateUpdate()
    {
        legSynchronizer.UpdateAllLegs(Time.deltaTime, groundMap);
    }

    private void FixedUpdate()
    {
        UpdateGroundData();
        HandleMoveInput();
        HandleJumpInput();
        if (grounded)
        {
            UpdateHeightSpring();
        }

        legSynchronizer.bodyGroundSpeed = grounded ? Vector2.Dot(rb.linearVelocity, Orientation * transform.right) : rb.linearVelocity.magnitude;
        legSynchronizer.preferredBodyPosGroundHeight = PreferredBodyPosGroundHeight;
        legSynchronizer.stepHeightFraction = 1 - crouchProgress * crouchHeightFraction;
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
        if (moveInput * Orientation < 0)
        {
            ChangeDirection();
        }
    }

    private void ChangeDirection()
    {
        var s = transform.localScale;
        transform.localScale = new Vector3(-s.x, s.y, s.z);
        //legSynchronizer.OnBodyChangedDirection();
    }

    private void HandleMoveInput()
    {
        Vector2 d = FacingRight ? groundDirection : -groundDirection;//grounded ? (FacingRight ? transform.right : - transform.right) : (FacingRight ? Vector2.right : -Vector2.right);
        var spd = Vector2.Dot(rb.linearVelocity, d);
        var maxSpd = grounded ? maxSpeed : maxSpeedAirborne;
        var a = grounded ? accelFactor : accelFactor * airborneAccelMultiplier;
        var s = Mathf.Min(maxSpd - spd, accelCap * maxSpd);
        //otherwise if speed is highly negative, we get ungodly rates of acceleration
        //(and note that we are doing it in a way that scales with max speed
        //-- so you can limit maxSpd - spd to being e.g. double the maxSpeed or w/e)
        if (moveInput != 0)
        {
            rb.AddForce(a * s * rb.mass * d);
        }
        else if (grounded && moveInput == 0)
        {
            rb.AddForce(decelFactor * -spd * rb.mass * d);//simulate friction
            Grip();
        }

        void Grip()
        {
            if (groundPoint.x != Mathf.Infinity)
            {
                var grip = steepSlopeGripStrength * Mathf.Abs(groundDirection.y);
                var h = Vector2.Dot(groundPoint - (Vector2)heightReferencePoint.position, d);
                //grip *= Mathf.Sign(h) * Mathf.Pow(Mathf.Abs(h), steepSlopeGripDistancePower);
                rb.AddForce(grip * h * rb.mass * d);//grip to steep slope
            }
        }
    }

    private void Balance(float dt)
    {
        //var c = Vector2.Dot(transform.up, grounded ? predictiveGroundDirection : groundDirection);
        //var f = c * balanceSpringForce - balanceSpringDamping * rb.angularVelocity;
        //rb.AddTorque(rb.mass * f);

        transform.right = MathTools.CheapRotationalLerp(transform.right, groundDirection, rotationSpeed * dt);
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
        //var d = FacingRight ? upcomingGroundDirection : -upcomingGroundDirection;
        var g = grounded ? upcomingGroundDirection : Vector2.right;
        headBone.transform.right = MathTools.CheapRotationalLerp(headBone.transform.right, g, headRotationSpeed * dt);
    }

    //not checking anything here, bc i have it set up so it only collects jump input when you are able to jump
    //(i.e. when grounded and not verifying jump)
    private void HandleJumpInput()
    {
        if (jumpInput)
        {
            jumpInput = false;
            SetGrounded(false);
            jumpVerificationTimer = jumpVerificationTime;
            var jumpDir = JumpDirection();
            groundDirection = MathTools.CheapRotationalLerp(groundDirection, jumpDir.CWPerp(), uphillJumpTakeoffRotationFraction);
            rb.AddForce(rb.mass * JumpForce() * jumpDir, ForceMode2D.Impulse);
        }
    }

    private float JumpForce()
    {
        return jumpForce + crouchProgress * jumpForceCrouchBoostRate;
    }

    private Vector2 JumpDirection()
    {
        if (groundDirection.y * Orientation > 0 && !(groundDirection.x < 0))//if facing uphill (but not upside down) add a little forward component to the jump
        {
            var t = uphillJumpDirectionRotationRate * Mathf.Abs(groundDirection.y);
            return MathTools.CheapRotationalLerp(transform.up, Vector2.up, t);
        }

        return transform.up;
    }

    private bool VerifyingJump()
    {
        return jumpVerificationTimer > 0;
    }

    private void UpdateHeightSpring()
    {
        Vector2 d = -transform.up;
        var l = groundMap.Center.raycastDistance - preferredRideHeight;
        var f = heightSpringForce * l * d;
        var v = Vector2.Dot(rb.linearVelocity, d) * d;
        rb.AddForce(rb.mass * (f - heightSpringDamping * v));
        //var g = groundPoint + preferredRideHeight * (Vector2)transform.up;
        //var d = g - (Vector2)heightReferencePoint.position;
        //var u = d.normalized;
        //var f = heightSpringForce * d;
        //var v = Vector2.Dot(rb.linearVelocity, u) * u;
        //rb.AddForce(rb.mass * (f - heightSpringDamping * v));
    }

    private void UpdateAirborneLegDrift()
    {
        if (legSynchronizer.outwardDrift < airborneLegDriftMax)
        {
            legSynchronizer.outwardDrift += airborneLegDriftRate * Time.deltaTime;
        }
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

    private void RecomputeGroundednessTolerance()
    {
        groundednessTolerance = (grounded ? groundedExitToleranceFactor : groundedEntryToleranceFactor) * preferredRideHeight;
    }

    private void OnTakeOff()
    {
        legSynchronizer.timeScale = airborneLegAnimationTimeScale;
        legSynchronizer.outwardDrift = 0;
        //legSynchronizer.EnterStaticMode();
        RecomputeGroundednessTolerance();
    }

    private void OnLanding()
    {
        legSynchronizer.timeScale = 1;
        legSynchronizer.outwardDrift = 0;
        //legSynchronizer.EndStaticMode(/*FacingRight, predictiveGroundDirection*/);//makes more sense to use predictive GroundDir bc that's what we rotate towards?
        RecomputeGroundednessTolerance();
    }

    private void UpdateGroundData()
    {
        groundMap.UpdateMap(heightReferencePoint.position, -transform.up, groundednessTolerance);

        if (moveInput != 0 || !grounded || groundPoint.x == Mathf.Infinity)
        {
            groundPoint = groundMap.Center.point;
        }

        if (!VerifyingJump())
        {
            SetGrounded(groundMap.Center.hitGround);
        }

        //if (groundMap.Center.hitGround)
        //{
        //    lastComputedGroundDistance = groundMap.Center.raycastDistance;
        //    groundDirection = groundMap.Center.normal.CWPerp();
        //}
        //else
        //{
        //    lastComputedGroundDistance = Mathf.Infinity;
        //    groundDirection = MathTools.CheapRotationalLerp(groundDirection, Vector2.right, failedGroundRaycastSmoothingRate * Time.deltaTime);
        //}
        groundDirection = groundMap.Center.hitGround ?
            groundMap.Center.normal.CWPerp()
            : MathTools.CheapRotationalLerp(groundDirection, Vector2.right, failedGroundRaycastSmoothingRate * Time.deltaTime);
        upcomingGroundDirection = groundMap.PointFromCenterByIndex(FacingRight ? 1 : -1).normal.CWPerp();
        //predictiveGroundDirection = FacingRight ? 
        //    (groundMap.RightEndPt.point - groundMap.Center.point).normalized
        //    : (groundMap.Center.point - groundMap.LeftEndPt.point).normalized;
    }

    //always "right pointing" (relative to ground outward normal)
    //private void OldUpdateGroundData()
    //{
    //    if (!VerifyingJump())//to avoid floaty/hovery rotation while taking off
    //    {
    //        Vector2 o = heightReferencePoint.position;
    //        Vector2 tDown = -transform.up;
    //        Vector2 tRight = transform.right;
    //        var l = GroundednessTolerance;
    //        var r = MathTools.DebugRaycast(o, tDown, l, groundLayer, Color.clear);

    //        if (r)
    //        {
    //            HandleSuccessfulGroundHit(r);
    //            if (grounded)
    //            {
    //                var r2 = MathTools.DebugRaycast(o + predictiveGroundDirectionSpacing * Orientation * tRight, tDown, l, groundLayer, Color.clear);
    //                if (r2)
    //                {
    //                    predictiveGroundDirection = FacingRight ? (r2.point - r.point).normalized : (r.point - r2.point).normalized;
    //                    upcomingGroundDirection = r2.normal.CWPerp();
    //                }
    //                else
    //                {
    //                    predictiveGroundDirection = groundDirection;
    //                    upcomingGroundDirection = groundDirection;//MathTools.CheapRotationalLerp(upcomingGroundDirection, groundDirection.CWPerp(), upcomingGroundDirectionSmoothingRate * Time.deltaTime);
    //                }

    //                return;
    //            }
    //        }

    //        //if r1 fails or distance was too large to be considered grounded, compute backup ground hits and choose shortest one
    //        float minDist = Mathf.Infinity;
    //        foreach (var s in BackupGroundHits(o, tDown, tRight, l))
    //        {
    //            if (s && s.distance < minDist)
    //            {
    //                minDist = s.distance;
    //                r = s;
    //            }
    //        }

    //        if (r)
    //        {
    //            HandleSuccessfulGroundHit(r);
    //            predictiveGroundDirection = groundDirection;
    //            upcomingGroundDirection = groundDirection;//MathTools.CheapRotationalLerp(upcomingGroundDirection, groundDirection, upcomingGroundDirectionSmoothingRate * Time.deltaTime);
    //            return;
    //        }
    //    }

    //    lastComputedGroundDistance = Mathf.Infinity;
    //    groundDirection = MathTools.CheapRotationalLerp(groundDirection, Vector2.right, failedGroundRaycastSmoothingRate * Time.deltaTime);
    //    predictiveGroundDirection = groundDirection;//don't lerp in this one case because it affects the rate at which we level out when jumping... (a little tangled)
    //    upcomingGroundDirection = groundDirection;//MathTools.CheapRotationalLerp(upcomingGroundDirection, groundDirection, upcomingGroundDirectionSmoothingRate * Time.deltaTime);
    //    SetGrounded(false);
    //}

    //private void HandleSuccessfulGroundHit(RaycastHit2D r)
    //{
    //    Vector2 p = heightReferencePoint.position;//the raycast origin
    //    var up = r.normal;
    //    var right = r.normal.CWPerp();
    //    var d = Vector2.Dot(r.point - p, up);
    //    var q = p + d * up;

    //    groundDirection = right;
    //    lastComputedGroundDistance = r.distance;

    //    if (moveInput != 0 || !grounded || groundPoint.x == Mathf.Infinity)
    //    {
    //        groundPoint = q;
    //    }

    //    SetGrounded(lastComputedGroundDistance < GroundednessTolerance);
    //}

    ////2do: should we distribute these "radially" or horizontally?
    ////radial has the advantage the we can see ground in front of us (the 90 deg cast)
    ////but horizontally would be simpler (and maybe if all horizontally spaced hits fail we want to slide anyway)
    ////we could also just do horizontal casts + the two 90 deg casts
    //private IEnumerable<RaycastHit2D> BackupGroundHits(Vector2 origin, Vector2 tDown, Vector2 tRight, float length)
    //{
    //    var d30 = MathTools.cos30 * tDown - MathTools.sin30 * tRight;
    //    var d45 = MathTools.cos45 * tDown - MathTools.sin45 * tRight;
    //    //var d60 = MathTools.cos60 * tDown + MathTools.sin60 * tRight;
    //    var dM30 = MathTools.cos30 * tDown + MathTools.sin30 * tRight;
    //    var dM45 = MathTools.cos45 * tDown + MathTools.sin45 * tRight;
    //    //var dM60 = MathTools.cos60 * tDown - MathTools.sin60 * tRight;
    //    var l30 = length / MathTools.cos30;
    //    var l45 = length / MathTools.cos45;
    //    //var l60 = length / MathTools.cos60;
    //    //var l90 = 3 * length;
    //    yield return MathTools.DebugRaycast(origin, d30, l30, groundLayer, Color.clear);
    //    yield return MathTools.DebugRaycast(origin, d45, l45, groundLayer, Color.clear);
    //    //yield return MathTools.DebugRaycast(origin, d60, length, groundLayer, Color.yellow);
    //    //yield return MathTools.DebugRaycast(origin, tRight, length, groundLayer, Color.red);
    //    yield return MathTools.DebugRaycast(origin, dM30, l30, groundLayer, Color.clear);
    //    yield return MathTools.DebugRaycast(origin, dM45, l45, groundLayer, Color.clear);
    //    //yield return MathTools.DebugRaycast(origin, dM60, length, groundLayer, Color.yellow);
    //    //yield return MathTools.DebugRaycast(origin, -tRight, length, groundLayer, Color.red);
    //}
}