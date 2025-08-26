using System.Collections.Generic;
using UnityEngine;

public class SpiderController : MonoBehaviour
{
    [SerializeField] Transform heightReferencePoint;
    //[SerializeField] float groundRaycastHorizontalSpacing = 1;
    [SerializeField] float groundRaycastLengthFactor;
    [SerializeField] float groundednessToleranceFactor;
    [SerializeField] float accelFactor;
    [SerializeField] float decelFactor;
    [SerializeField] float steepSlopeGripStrength;
    [SerializeField] float steepSlopeGripDistancePower;
    [SerializeField] float slipRate;
    [SerializeField] float maxSpeed;
    [SerializeField] float preferredRideHeight;
    [SerializeField] float heightSpringForce;
    [SerializeField] float heightSpringDamping;
    [SerializeField] float balanceSpringForce;
    [SerializeField] float balanceSpringDamping;
    [SerializeField] float jumpForce;
    [SerializeField] float jumpVerificationTime;

    LegSynchronizer legSynchronizer;
    Rigidbody2D rb;
    int moveInput;

    bool jumpInput;
    float jumpVerificationTimer;

    bool grounded;
    Vector2 lastComputedGroundDirection = Vector2.right;
    Vector2 lastComputedGroundPoint = new(Mathf.Infinity, Mathf.Infinity);
    Vector2 groundSlipPoint;
    //to force it to initialize ground point (and then after it only gets set when moveInput != 0)
    float lastComputedGroundDistance = Mathf.Infinity;
    //use infinity instead of NaN, because equals check always fails for NaN (even if you check NaN == NaN)

    int groundLayer;

    bool FacingRight => transform.localScale.x > 0;
    int Orientation => FacingRight ? 1 : -1;
    float GroundRaycastLength => groundRaycastLengthFactor * preferredRideHeight;
    float GroundednessTolerance => groundednessToleranceFactor * preferredRideHeight;
    Vector2 JumpDirection => transform.up;//0.5f * (Vector2.up + lastComputedGroundDirection.CCWPerp()).normalized;

    private void Awake()
    {
        legSynchronizer = GetComponent<LegSynchronizer>();
        rb = GetComponent<Rigidbody2D>();
        groundLayer = LayerMask.GetMask("Ground");
    }

    private void Update()
    {
        if (VerifyingJump())
        {
            jumpVerificationTimer -= Time.deltaTime;
        }

        //only set jumpInput when !jumpInput, since multiple Updates may happen before FixedUpdate handles the jumpInput
        if (!jumpInput && grounded)
        {
            jumpInput = Input.GetKeyDown(KeyCode.Space);
        }

        moveInput = (Input.GetKey(KeyCode.RightArrow) ? 1 : 0) + (Input.GetKey(KeyCode.LeftArrow) ? -1 : 0);
        if (moveInput * Orientation < 0)
        {
            ChangeDirection();
        }

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

        legSynchronizer.bodyGroundSpeed = Vector2.Dot(rb.linearVelocity, Orientation * transform.right);
        //2do (minor performance improvement): we compute Orientation * lastComputedGroundDirection (or maybe now Ori * tRight) multiple times in one update
        //either compute it once, or have lastComputedDirection already point in orientation direction
        //(are there any places where you need it to be "right facing"? i think only for the balancing)
    }

    private void ChangeDirection()
    {
        var s = transform.localScale;
        transform.localScale = new Vector3(-s.x, s.y, s.z);
        legSynchronizer.OnBodyChangedDirection();
    }

    private void HandleMoveInput()
    {
        var d = Orientation * transform.right;//Orientation * lastComputedGroundDirection;
        var spd = Vector2.Dot(rb.linearVelocity, d);
        if (moveInput != 0 /*&& spd < maxSpeed*/)
        {
            rb.AddForce(accelFactor * (maxSpeed - spd) * rb.mass * d);
            //do this even when spd > maxSpeed so that spider controls its speed on downhills
        }
        else if (moveInput == 0 && grounded)
        {
            rb.AddForce(decelFactor * -spd * rb.mass * d);//simulate friction
            var grip = steepSlopeGripStrength * Mathf.Abs(lastComputedGroundDirection.y);
            var h = Vector2.Dot(groundSlipPoint - (Vector2)heightReferencePoint.position, d);
            grip *= Mathf.Sign(h) * Mathf.Pow(Mathf.Abs(h), steepSlopeGripDistancePower);
            rb.AddForce(grip * rb.mass * d);//grip to steep slope
        }
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
            rb.AddForce(jumpForce * rb.mass * JumpDirection, ForceMode2D.Impulse);
        }
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
        var c = Vector2.Dot(transform.up, grounded ? lastComputedGroundDirection : Vector2.right);
        var f = c * balanceSpringForce - balanceSpringDamping * rb.angularVelocity;
        rb.AddTorque(rb.mass * f);
    }


    //GROUND DETECTION

    private void SetGrounded(bool val)
    {
        if (VerifyingJump() || grounded == val) return;
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
        legSynchronizer.dragRestingLegs = true;
    }

    private void OnLanding()
    {
        legSynchronizer.dragRestingLegs = false;
        //legSynchronizer.RepositionAllLegs(lastComputedGroundDirection);
    }

    //always "right pointing" (relative to ground outward normal)
    private void UpdateGroundData()
    {
        Vector2 o = heightReferencePoint.position;
        Vector2 tDown = -transform.up;
        Vector2 tRight = transform.right;
        var l = GroundRaycastLength;
        var r = Physics2D.Raycast(o, tDown, l, groundLayer);

        if (r)
        {
            HandleSuccessfulGroundHit(r);
            return;
        }

        //if r1 fails, compute backup ground hits and choose shortest one
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
            return;
        }

        SetGrounded(false);//generally we should not set grounded while verifying jump, but setting false is fine (so no pt in slowing things down with a bool check)
        lastComputedGroundDistance = Mathf.Infinity;
        lastComputedGroundDirection = Vector2.right;
    }

    private void HandleSuccessfulGroundHit(RaycastHit2D r)
    {
        if (moveInput != 0 || !grounded || lastComputedGroundPoint.x == Mathf.Infinity)
        {
            lastComputedGroundPoint = r.point;
            groundSlipPoint = lastComputedGroundPoint;
        }
        else
        {
            groundSlipPoint = Vector2.Lerp(lastComputedGroundPoint, r.point, slipRate * Time.deltaTime);
        }
        lastComputedGroundDistance = r.distance;
        lastComputedGroundDirection = r.normal.CWPerp();

        SetGrounded(r.distance < GroundednessTolerance);
    }

    //2do: should we distribute these "radially" or horizontally?
    //radial has the advantage the we can see ground in front of us (the 90 deg cast)
    //but horizontally would be simpler (and maybe if all horizontally spaced hits fail we want to slide anyway)
    //we could also just do horizontal casts + the two 90 deg casts
    private IEnumerable<RaycastHit2D> BackupGroundHits(Vector2 origin, Vector2 tDown, Vector2 tRight, float length)
    {
        var d30 = MathTools.cos30 * tDown + MathTools.sin30 * tRight;
        var d60 = MathTools.cos60 * tDown + MathTools.sin60 * tRight;
        var dM30 = MathTools.cos30 * tDown - MathTools.sin30 * tRight;
        var dM60 = MathTools.cos60 * tDown - MathTools.sin60 * tRight;
        //var l30 = length / MathTools.cos30;
        //var l60 = length / MathTools.cos60;
        //var l90 = 2 * length;
        yield return Physics2D.Raycast(origin, d30, length, groundLayer);
        yield return Physics2D.Raycast(origin, d60, length, groundLayer);
        yield return Physics2D.Raycast(origin, tRight, length, groundLayer);
        yield return Physics2D.Raycast(origin, dM30, length, groundLayer);
        yield return Physics2D.Raycast(origin, dM60, length, groundLayer);
        yield return Physics2D.Raycast(origin, -tRight, length, groundLayer);

    }
}