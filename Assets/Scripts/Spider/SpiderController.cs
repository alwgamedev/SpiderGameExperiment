using System.Collections.Generic;
using UnityEngine;

public class SpiderController : MonoBehaviour
{
    [SerializeField] Transform abdomenBone;
    [SerializeField] Transform headBone;
    [SerializeField] Transform heightReferencePoint;
    [SerializeField] float groundRaycastLengthFactor;
    [SerializeField] float groundednessToleranceFactor;
    [SerializeField] float predictiveGroundDirectionSpacing;
    [SerializeField] float failedGroundRaycastSmoothingRate;
    [SerializeField] float accelFactor;
    [SerializeField] float accelCap;
    [SerializeField] float decelFactor;
    [SerializeField] float airborneAccelMultiplier;
    [SerializeField] float steepSlopeGripStrength;
    [SerializeField] float steepSlopeGripDistancePower;
    //[SerializeField] float slipPointSmoothingRate;
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
    [SerializeField] float jumpVerificationTime;
    [SerializeField] float crouchHeightFraction;
    [SerializeField] float crouchTime;
    [SerializeField] float crouchReleaseSpeedMultiplier;
    [SerializeField] float airborneLegAnimationTimeScale;
    [SerializeField] float airborneLegDriftRate;
    [SerializeField] float airborneLegDriftMax;

    LegSynchronizer legSynchronizer;
    Rigidbody2D rb;
    int moveInput;

    bool jumpInput;
    float jumpVerificationTimer;

    bool grounded;
    Vector2 predictiveGroundDirection;
    Vector2 lastComputedGroundDirection = Vector2.right;
    Vector2 lastComputedGroundPoint = new(Mathf.Infinity, Mathf.Infinity);
    //Vector2 groundSlipPoint;
    //to force it to initialize ground point (and then after it only gets set when moveInput != 0)
    float lastComputedGroundDistance = Mathf.Infinity;
    //use infinity instead of NaN, because equals check always fails for NaN (even if you check NaN == NaN)

    float crouchProgress;//0-1

    int groundLayer;

    bool FacingRight => transform.localScale.x > 0;
    int Orientation => FacingRight ? 1 : -1;
    float GroundRaycastLength => groundRaycastLengthFactor * preferredRideHeight;
    float GroundednessTolerance => groundednessToleranceFactor * preferredRideHeight;
    float PreferredBodyPosGroundHeight => transform.position.y - heightReferencePoint.position.y + preferredRideHeight;

    private void Awake()
    {
        legSynchronizer = GetComponent<LegSynchronizer>();
        rb = GetComponent<Rigidbody2D>();
        groundLayer = LayerMask.GetMask("Ground");

        //Time.timeScale = 0.25f;//useful for spotting issues
    }

    private void Start()
    {
        legSynchronizer.Initialize(PreferredBodyPosGroundHeight, FacingRight);
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
        Balance();

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
            if (crouchProgress > 0)
            {
                UpdateCrouch(crouchReleaseSpeedMultiplier * -Time.deltaTime);
            }
            if (!jumpInput && grounded)
            {
                jumpInput = Input.GetKeyUp(KeyCode.Space);
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
        legSynchronizer.OnBodyChangedDirection();
    }

    private void HandleMoveInput()
    {
        var d = Orientation * transform.right;
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
        else if (moveInput == 0 && grounded)
        {
            rb.AddForce(decelFactor * -spd * rb.mass * d);//simulate friction
            var grip = steepSlopeGripStrength * Mathf.Abs(lastComputedGroundDirection.y);
            var h = Vector2.Dot(lastComputedGroundPoint - (Vector2)heightReferencePoint.position, d);
            grip *= Mathf.Sign(h) * Mathf.Pow(Mathf.Abs(h), steepSlopeGripDistancePower);
            rb.AddForce(grip * rb.mass * d);//grip to steep slope
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
            lastComputedGroundDirection = jumpDir.CWPerp();
            rb.AddForce(rb.mass * JumpForce() * jumpDir, ForceMode2D.Impulse);
        }
    }

    private float JumpForce()
    {
        return jumpForce + crouchProgress * jumpForceCrouchBoostRate;
    }

    private Vector2 JumpDirection()
    {
        if (lastComputedGroundDirection.y * Orientation > 0)//if facing uphill add a little forward component to the jump
        {
            var t = uphillJumpDirectionRotationRate * Mathf.Abs(lastComputedGroundDirection.y);
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
        var direction = -transform.up;
        var l = lastComputedGroundDistance - preferredRideHeight;
        var f = heightSpringForce * l * direction;
        var v = Vector2.Dot(rb.linearVelocity, direction) * direction;
        rb.AddForce(rb.mass * (f - heightSpringDamping * v));
    }

    private void Balance()
    {
        var c = Vector2.Dot(transform.up, grounded ? predictiveGroundDirection : lastComputedGroundDirection);
        var f = c * balanceSpringForce - balanceSpringDamping * rb.angularVelocity;
        rb.AddTorque(rb.mass * f);
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

    private void OnTakeOff()
    {
        legSynchronizer.timeScale = airborneLegAnimationTimeScale;
        legSynchronizer.outwardDrift = 0;
        legSynchronizer.EnterStaticMode();
    }

    private void OnLanding()
    {
        legSynchronizer.timeScale = 1;
        legSynchronizer.outwardDrift = 0;
        legSynchronizer.EndStaticMode(FacingRight, predictiveGroundDirection);//makes more sense to use predictive GroundDir bc that's what we rotate towards?
    }

    //always "right pointing" (relative to ground outward normal)
    private void UpdateGroundData()
    {
        if (!VerifyingJump())
        {
            Vector2 o = heightReferencePoint.position;
            Vector2 tDown = -transform.up;
            Vector2 tRight = transform.right;
            var l = GroundRaycastLength;
            var r = MathTools.DebugRaycast(o, tDown, l, groundLayer, Color.red);

            if (r)
            {
                HandleSuccessfulGroundHit(r);
                if (grounded)
                {
                    var r2 = MathTools.DebugRaycast(o + Orientation * tRight, tDown, l, groundLayer, Color.red);
                    predictiveGroundDirection = r2 ? (FacingRight ? (r2.point - r.point).normalized : (r.point - r2.point).normalized) : lastComputedGroundDirection;
                    return;
                }
            }

            //if r1 fails or distance was too large to be considered grounded, compute backup ground hits and choose shortest one
            float minDist = Mathf.Infinity;
            foreach (var s in BackupGroundHits(o, tDown, tRight, l))
            {
                if (s && s.distance < minDist)
                {
                    minDist = s.distance;
                    r = s;
                }
            }

            if (r)
            {
                HandleSuccessfulGroundHit(r);
                predictiveGroundDirection = lastComputedGroundDirection;
                return;
            }
        }

        lastComputedGroundDistance = Mathf.Infinity;
        lastComputedGroundDirection = MathTools.CheapRotationalLerp(lastComputedGroundDirection, Vector2.right, failedGroundRaycastSmoothingRate * Time.deltaTime); 
        predictiveGroundDirection = lastComputedGroundDirection;
        SetGrounded(false);

    }

    private void HandleSuccessfulGroundHit(RaycastHit2D r)
    {
        Vector2 p = heightReferencePoint.position;//the raycast origin
        var up = r.normal;
        var right = r.normal.CWPerp();
        var d = Vector2.Dot(r.point - p, up);
        var q = p + d * up;

        lastComputedGroundDirection = right;
        lastComputedGroundDistance = Mathf.Abs(d);
        //lastComputedGroundDistance = r.distance;

        if (moveInput != 0 || !grounded || lastComputedGroundPoint.x == Mathf.Infinity)
        {
            lastComputedGroundPoint = q;
            //groundSlipPoint = lastComputedGroundPoint;
            //lastComputedGroundDistance = Mathf.Abs(d);
        }
        //else
        //{
        //    groundSlipPoint = Vector2.Lerp(lastComputedGroundPoint, q, slipPointSmoothingRate * Time.deltaTime);
        //    lastComputedGroundDistance = Vector2.Distance(p, groundSlipPoint);
        //}

        SetGrounded(lastComputedGroundDistance < GroundednessTolerance);
    }

    //2do: should we distribute these "radially" or horizontally?
    //radial has the advantage the we can see ground in front of us (the 90 deg cast)
    //but horizontally would be simpler (and maybe if all horizontally spaced hits fail we want to slide anyway)
    //we could also just do horizontal casts + the two 90 deg casts
    private IEnumerable<RaycastHit2D> BackupGroundHits(Vector2 origin, Vector2 tDown, Vector2 tRight, float length)
    {
        var d30 = MathTools.cos30 * tDown - MathTools.sin30 * tRight;
        var d45 = MathTools.cos45 * tDown - MathTools.sin45 * tRight;
        //var d60 = MathTools.cos60 * tDown + MathTools.sin60 * tRight;
        var dM30 = MathTools.cos30 * tDown + MathTools.sin30 * tRight;
        var dM45 = MathTools.cos45 * tDown + MathTools.sin45 * tRight;
        //var dM60 = MathTools.cos60 * tDown - MathTools.sin60 * tRight;
        //var l30 = length / MathTools.cos30;
        //var l60 = length / MathTools.cos60;
        //var l90 = 3 * length;
        yield return MathTools.DebugRaycast(origin, d30, length, groundLayer, Color.yellow);
        yield return MathTools.DebugRaycast(origin, d45, length, groundLayer, Color.yellow);
        //yield return MathTools.DebugRaycast(origin, d60, length, groundLayer, Color.yellow);
        //yield return MathTools.DebugRaycast(origin, tRight, length, groundLayer, Color.red);
        yield return MathTools.DebugRaycast(origin, dM30, length, groundLayer, Color.yellow);
        yield return MathTools.DebugRaycast(origin, dM45, length, groundLayer, Color.yellow);
        //yield return MathTools.DebugRaycast(origin, dM60, length, groundLayer, Color.yellow);
        //yield return MathTools.DebugRaycast(origin, -tRight, length, groundLayer, Color.red);
    }
}