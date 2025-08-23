using UnityEngine;

public class SpiderController : MonoBehaviour
{
    [SerializeField] float groundRaycastHorizontalSpacing;
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
    //[SerializeField] float gripSmoothingRate;
    //[SerializeField] float gripSmoothThreshold;
    //[SerializeField] float gripSmoothingPower;
    [SerializeField] Transform heightReferencePoint;

    LegSynchronizer legSynchronizer;
    Rigidbody2D rb;
    int moveInput;
    //float smoothedMoveInput = 0;

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

    private void Awake()
    {
        legSynchronizer = GetComponent<LegSynchronizer>();
        rb = GetComponent<Rigidbody2D>();
        groundLayer = LayerMask.GetMask("Ground");
    }

    private void Update()
    {
        moveInput = (Input.GetKey(KeyCode.RightArrow) ? 1 : 0) + (Input.GetKey(KeyCode.LeftArrow) ? -1 : 0);
        //smoothedMoveInput = Mathf.Lerp(smoothedMoveInput, moveInput, gripSmoothingRate * Time.deltaTime);
        if (moveInput * Orientation < 0)
        {
            ChangeDirection();
        }

    }

    private void FixedUpdate()
    {
        UpdateGroundData();

        if (grounded)
        {
            HandleMoveInput();
            UpdateHeightSpring();
            Balance();
        }

        legSynchronizer.bodyGroundSpeed = Vector2.Dot(rb.linearVelocity, Orientation * lastComputedGroundDirection);
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

        //replace with correct friction implementation -- maybe more friction on steeper slopes
        //(but you would really just have something like this that tries to keep spider in place)
        else if (moveInput == 0)
        {
            rb.AddForce(decelFactor * -spd * rb.mass * d);
            var grip = steepSlopeGripStrength * Mathf.Abs(lastComputedGroundDirection.y);
            var h = Vector2.Dot(groundSlipPoint - (Vector2)heightReferencePoint.position, d);
            grip *= Mathf.Sign(h) * Mathf.Pow(Mathf.Abs(h), steepSlopeGripDistancePower);
            rb.AddForce(grip * rb.mass * d);
        }
    }

    private void UpdateHeightSpring()
    {
        var direction = -transform.up;
        var l = lastComputedGroundDistance - preferredRideHeight;
        var f = heightSpringForce * l * direction;
        var v = Vector2.Dot(rb.linearVelocity, direction) * direction;
        rb.AddForce(rb.mass * (f - heightSpringDamping * v));

        //redo:
        //-height spring always working 
        //if (smoothedMoveInputAboveThreshold && lastComputedGroundDistance != Mathf.Infinity)
        //{
        //    //MAINTAIN HEIGHT
        //    var direction = -transform.up;
        //    var l = lastComputedGroundDistance - preferredRideHeight;
        //    var f = heightSpringForce * l * direction;
        //    var v = Vector2.Dot(rb.linearVelocity, direction) * direction;
        //    rb.AddForce(rb.mass * (f - heightSpringDamping * v));
        //}
        //else if (!smoothedMoveInputAboveThreshold && lastComputedGroundPoint.x != Mathf.Infinity)
        //{
        //    //MAINTAIN HEIGHT & "GRIP" TO POSITION (helps climb steep slopes)
        //    var groundUp = lastComputedGroundDirection.CCWPerp();
        //    var g = lastComputedGroundPoint + preferredRideHeight * groundUp;
        //    var d = g - (Vector2)heightReferencePoint.position;
        //    var l = d.magnitude;
        //    if (l > 10E-05f)
        //    {
        //        d /= l;
        //        //var s = 1 - Mathf.Abs(smoothedMoveInput);
        //        var f = /*Mathf.Pow(s, gripSmoothingPower) **/ heightSpringForce * l * d;
        //        var v = Vector2.Dot(rb.linearVelocity, d) * d;
        //        rb.AddForce((1 - Mathf.Abs(smoothedMoveInput)) * (rb.mass * (f - heightSpringDamping * v)));
        //    }
        //}
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
        var r1 = Physics2D.Raycast(o1, -transform.up, Mathf.Infinity, groundLayer);
        var r2 = Physics2D.Raycast(o2, -transform.up, Mathf.Infinity, groundLayer);

        if (r1 && r2)
        {
            grounded = true;
            //if (smoothedMoveInputAboveThreshold || lastComputedGroundPoint.x == Mathf.Infinity)
            //{
            //    lastComputedGroundPoint = r1.point;
            //}
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
            //so we get right pointing ground direction
        }
        else
        {
            grounded = false;
            lastComputedGroundDistance = Mathf.Infinity;
            lastComputedGroundDirection = tRight;
        }
    }

    //private bool SmoothedMoveInputAboveThreshold()
    //{
    //    return Mathf.Abs(smoothedMoveInput) > gripSmoothThreshold;
    //}
}