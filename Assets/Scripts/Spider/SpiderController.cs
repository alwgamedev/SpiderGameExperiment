using UnityEngine;

public class SpiderController : MonoBehaviour
{
    [SerializeField] Transform heightReferencePoint;
    [SerializeField] float groundRaycastHorizontalSpacing;
    [SerializeField] float groundRaycastLengthFactor;
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
    float jumpVerificationTimer = float.MinValue;

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

    private void Awake()
    {
        legSynchronizer = GetComponent<LegSynchronizer>();
        rb = GetComponent<Rigidbody2D>();
        groundLayer = LayerMask.GetMask("Ground");
    }

    private void Update()
    {
        //important to only set jumpInput when !jumpInput, since multiple Updates may happen before FixedUpdate handles the jumpInput
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
        if (jumpVerificationTimer < 0)//initialized to negative value so we don't have to use <=
        {
            UpdateGroundData();
        }
        else
        {
            jumpVerificationTimer -= Time.deltaTime;
        }

        HandleMoveInput();
        HandleJumpInput();

        if (grounded)
        {
            UpdateHeightSpring();
            Balance();
        }

        legSynchronizer.bodyGroundSpeed = Vector2.Dot(rb.linearVelocity, Orientation * lastComputedGroundDirection);
        //2do (minor performance improvement): we compute Orientation * lastComputedGroundDirection multiple times in one update
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
        var d = lastComputedGroundDirection * Orientation;
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
            grounded = false;
            jumpVerificationTimer = jumpVerificationTime;
            rb.AddForce(jumpForce * rb.mass * transform.up, ForceMode2D.Impulse);
        }
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
        var c = Vector2.Dot(transform.up, lastComputedGroundDirection);
        var f = c * balanceSpringForce - balanceSpringDamping * rb.angularVelocity;
        rb.AddTorque(rb.mass * f);
    }

    //always "right pointing" (relative to ground outward normal)
    private void UpdateGroundData()
    {
        Vector2 pos = heightReferencePoint.position;
        Vector2 tRight = transform.right;
        var o1 = pos;
        var o2 = pos + Orientation * groundRaycastHorizontalSpacing * tRight;
        var l = GroundRaycastLength;
        var r1 = Physics2D.Raycast(o1, -transform.up, l, groundLayer);
        var r2 = Physics2D.Raycast(o2, -transform.up, l, groundLayer);


        if (r1 && r2)
        {
            grounded = true;
            if (moveInput != 0 || lastComputedGroundPoint.x == Mathf.Infinity)
            {
                lastComputedGroundPoint = r1.point;
                groundSlipPoint = lastComputedGroundPoint;
            }
            else
            {
                groundSlipPoint = Vector2.Lerp(lastComputedGroundPoint, r1.point, slipRate * Time.deltaTime);
            }
            lastComputedGroundDistance = r1.distance;
            lastComputedGroundDirection = FacingRight ? (r2.point - r1.point).normalized : (r1.point - r2.point).normalized;
        }
        else
        {
            grounded = r1 || r2;
            lastComputedGroundDistance = r1 ? r1.distance : (r2 ? r2.distance : Mathf.Infinity);
            lastComputedGroundDirection = Vector2.right;
        }
    }
}