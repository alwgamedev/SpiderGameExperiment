using UnityEngine;
using UnityEngine.Assertions.Must;

public class SpiderController : MonoBehaviour
{
    [SerializeField] Transform abdomenBone;
    [SerializeField] Transform headBone;
    [SerializeField] Transform heightReferencePoint;
    [SerializeField] float headRotationSpeed;
    [SerializeField] float groundedRotationSpeed;
    [SerializeField] float airborneRotationSpeed;
    [SerializeField] LayerMask groundLayer;
    [SerializeField] float groundedExitToleranceFactor;
    [SerializeField] float groundedEntryToleranceFactor;
    [SerializeField] float groundedSearchWidth;
    [SerializeField] float backupGroundPtRaycastLengthFactor;
    [SerializeField] float upcomingGroundDirectionOffset;
    [SerializeField] float failedGroundRaycastSmoothingRate;
    [SerializeField] float accelFactor;
    [SerializeField] float accelCap;
    //[SerializeField] float jumpVerificationAccelCap;
    [SerializeField] float decelFactor;
    [SerializeField] float airborneAccelMultiplier;
    [SerializeField] float gripStrength;
    [SerializeField] float maxSpeed;
    [SerializeField] float maxSpeedAirborne;
    [SerializeField] float preferredRideHeight;
    [SerializeField] float heightSpringForce;
    [SerializeField] float heightSpringDamping;
    [SerializeField] float balanceSpringForce;
    //[SerializeField] float balanceSpringPower;
    [SerializeField] float balanceSpringDamping;
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
    Vector2 groundDirection = Vector2.right;
    Vector2 upcomingGroundDirection = Vector2.right;
    Vector2 groundPoint = new(Mathf.Infinity, Mathf.Infinity);
    Vector2 groundPointGroundDirection = Vector2.right;

    //Vector2 rbAccel;
    //Vector2 rbLastVel;

    float crouchProgress;//0-1

    bool FacingRight => transform.localScale.x > 0;
    int Orientation => FacingRight ? 1 : -1;
    float PreferredBodyPosGroundHeight => transform.position.y - heightReferencePoint.position.y + preferredRideHeight;
    bool StronglyGrounded => grounded && groundMap.AllHitGround();
    //we can store this in a field, but then we have to worry about whether we're updating it everytime we need to (e.g. on jump takeoff have to set to false)
    //so for now it's much easier to just leave like this
    

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
        legSynchronizer = GetComponent<LegSynchronizer>();
        rb = GetComponent<Rigidbody2D>();

        //Time.timeScale = 0.25f;//useful for spotting issues
    }

    private void Start()
    {
        legSynchronizer.Initialize(PreferredBodyPosGroundHeight, FacingRight);
        InitializeGroundData();
    }

    private void Update()
    {
        if (VerifyingJump())
        {
            jumpVerificationTimer -= Time.deltaTime;
        }

        CaptureInput();

        RotateHead(Time.deltaTime);
        //Balance(/*Time.deltaTime*/);
    }

    private void LateUpdate()
    {
        legSynchronizer.UpdateAllLegs(Time.deltaTime, groundMap);
    }

    private void FixedUpdate()
    {
        //rbAccel = rb.linearVelocity - rbLastVel;
        //rbLastVel = rb.linearVelocity;

        UpdateGroundData();
        HandleMoveInput(); 
        HandleJumpInput();
        if (StronglyGrounded)
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

        //accelCap bc otherwise if speed is highly negative, we get ungodly rates of acceleration
        //(and note that we are doing it in a way that scales with max speed
        //-- so you can limit maxSpd - spd to being e.g. double the maxSpeed or w/e)
        if (moveInput != 0)
        {
            Vector2 d = FacingRight ? groundDirection : -groundDirection;
            var spd = Vector2.Dot(rb.linearVelocity, d);
            var maxSpd = grounded ? maxSpeed : maxSpeedAirborne;
            var a = grounded ? accelFactor : accelFactor * airborneAccelMultiplier;
            var aCap = VerifyingJump() ? Mathf.Lerp(0, accelCap, 1 - Mathf.Pow(jumpVerificationTimer / jumpVerificationTime, 2)) : accelCap;
                //^reduce accelCap during jump verification to prevent jittery floating if holding move input into slope while jumping
            var s = Mathf.Min(maxSpd - spd, aCap * maxSpd);
            rb.AddForce(a * s * rb.mass * d);
        }
        else if (StronglyGrounded)
        {
            Vector2 d = FacingRight ? groundPointGroundDirection : -groundPointGroundDirection;
            var spd = Vector2.Dot(rb.linearVelocity, d);
            var h = Vector2.Dot(groundPoint - (Vector2)heightReferencePoint.position, d);
            rb.AddForce((gripStrength * h - decelFactor * spd) * rb.mass * d);//grip to steep slope
        }
    }

    private void Balance(/*float dt*/)
    {
        var c = Vector2.Dot(transform.up, /*grounded ? predictiveGroundDirection :*/ groundDirection);
        var f = c * balanceSpringForce - balanceSpringDamping * rb.angularVelocity;
        rb.AddTorque(rb.mass * f);

        //var r = grounded ? groundedRotationSpeed : airborneRotationSpeed;
        //transform.right = MathTools.CheapRotationBySpeed(transform.right, groundDirection, r, dt);
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
        var g = grounded ? upcomingGroundDirection : Vector2.right;
        headBone.transform.right = MathTools.CheapRotationBySpeed(headBone.transform.right, g, headRotationSpeed, dt);
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
        //if (groundDirection.y * Orientation > 0 && !(groundDirection.x < 0))//if facing uphill (but not upside down) add a little forward component to the jump
        //{
        //    var t = uphillJumpDirectionRotationRate * Mathf.Abs(groundDirection.y);
        //    return MathTools.CheapRotationalLerp(transform.up, Vector2.up, t);
        //}

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
        //we can also do an average (or o/w) over hits near center to pull us closer to ground when on a sharp peak (to prevent legs overextending on sides)
        var f = heightSpringForce * l * d;
        var v = Vector2.Dot(rb.linearVelocity, d) * d;
        rb.AddForce(rb.mass * (f - heightSpringDamping * v));
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
        var ptRight = pt.normal.CWPerp();
        var isCentralIndex = groundMap.IsCentralIndex(i);

        if (!VerifyingJump())
        {
            SetGrounded(pt.hitGround);
        }

        if (moveInput != 0 || !StronglyGrounded)//updating when !StronglyGrounded allows groundpt to slip to an accurate position while settling down on landing
        {
            if (pt.hitGround && !isCentralIndex)
            {
                var r = Physics2D.Raycast(heightReferencePoint.position, -pt.normal,
                    backupGroundPtRaycastLengthFactor * groundednessTolerance, groundLayer);
                if (r)
                {
                    pt = new GroundMapPt(r.point, r.normal, 0, 0, true);
                    ptRight = pt.normal.CWPerp();
                    isCentralIndex = true;
                }
            }

            groundPoint = pt.point;
            groundPointGroundDirection = ptRight;
        }

        groundDirection = pt.hitGround ?
            ptRight
            : MathTools.CheapRotationBySpeed(groundDirection, Vector2.right, failedGroundRaycastSmoothingRate, Time.deltaTime);
        int j = isCentralIndex ?
            groundMap.IndexOfLastMarkedPointBeforePosition(FacingRight ? upcomingGroundDirectionOffset : -upcomingGroundDirectionOffset)
            : i;
        upcomingGroundDirection = groundMap[j].normal.CWPerp();
    }

    private void UpdateGroundMap()
    {
        groundMap.UpdateMap(heightReferencePoint.position, -transform.up, groundednessTolerance, groundMap.CentralIndex, groundLayer);
        //issues rn:
        //a) it prefers finding indexOfFirstGroundHit on right side (does not take facing direction into account)
        //b) second map update should be allowed to give a better indexOfFirstGroundHit (i.e. closer to center)
        //c) we're still sliding down when jumping at wall facing right (look at grip/decel fct again; really need a better friction simulation -- should be simple)
    }

    private void InitializeGroundPoint()
    {
        groundPoint = groundMap.Center.point;
        groundPointGroundDirection = groundMap.Center.normal.CWPerp();
    }

    private void InitializeGroundData()
    {
        RecomputeGroundednessTolerance();
        UpdateGroundMap();
        InitializeGroundPoint();
    }
}