using UnityEngine;

public class SpiderController : MonoBehaviour
{
    [SerializeField] float accelFactor;
    [SerializeField] float decelFactor;
    [SerializeField] float maxSpeed;
    [SerializeField] float preferredRideHeight;
    [SerializeField] float heightSpringForce;
    [SerializeField] float heightSpringDamping;
    [SerializeField] float balanceSpringForce;
    [SerializeField] float balanceSpringDamping;
    [SerializeField] Transform heightReferencePoint;

    LegSynchronizer legSynchronizer;
    Rigidbody2D rb;
    int moveInput;

    bool grounded;
    Vector2 lastComputedGroundDirection = Vector2.right;
    Vector2 lastComputedGroundPoint = new(float.NaN, float.NaN);

    int groundLayer;
    const float groundRaycastHorizontalSpacing = .1f;

    int Orientation => transform.localScale.x > 0 ? 1 : -1;

    private void Awake()
    {
        legSynchronizer = GetComponent<LegSynchronizer>();
        rb = GetComponent<Rigidbody2D>();
        groundLayer = LayerMask.GetMask("Ground");
    }

    private void Start()
    {
        TryUpdateGroundPoint(out _, out _);
    }

    private void Update()
    {
        moveInput = (Input.GetKey(KeyCode.RightArrow) ? 1 : 0) + (Input.GetKey(KeyCode.LeftArrow) ? -1 : 0);
        if (moveInput * Orientation < 0)
        {
            ChangeDirection();
        }
    }

    private void FixedUpdate()
    {
        lastComputedGroundDirection = GroundDirection();

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
            //have to do this because there's no friction, since we're not in contact with ground
            rb.AddForce(decelFactor * -spd * rb.mass * d);
        }
    }

    private void UpdateHeightSpring()
    {
        if (moveInput != 0 || lastComputedGroundPoint.x == float.NaN)
        {
            if (TryUpdateGroundPoint(out var distance, out var direction))
            {
                var l = distance - preferredRideHeight;
                var f = heightSpringForce * l * direction;
                var v = Vector2.Dot(rb.linearVelocity, direction) * direction;
                rb.AddForce(rb.mass * (f - heightSpringDamping * v));
            }
        }
        else
        {
            var groundUp = lastComputedGroundDirection.CCWPerp();
            var g = lastComputedGroundPoint + preferredRideHeight * groundUp;
            var d = g - (Vector2)heightReferencePoint.position;
            var l = d.magnitude;
            if (l > 10E-05f)
            {
                d /= l;
                var f = heightSpringForce * l * d;
                var v = Vector2.Dot(rb.linearVelocity, d) * d;
                rb.AddForce(rb.mass * (f - heightSpringDamping * v));
            }
        }
    }

    private void Balance()
    {
        var c = Vector2.Dot(transform.up, lastComputedGroundDirection);
        var f = c * balanceSpringForce - balanceSpringDamping * rb.angularVelocity;
        rb.AddTorque(rb.mass * f);
    }

    private Vector2 GroundDirection()
    {
        Vector2 pos = heightReferencePoint.position;
        Vector2 tRight = transform.right;
        var o1 = pos - groundRaycastHorizontalSpacing * tRight;
        var o2 = pos + groundRaycastHorizontalSpacing * tRight;
        var r1 = Physics2D.Raycast(o1, -transform.up, Mathf.Infinity, groundLayer);
        var r2 = Physics2D.Raycast(o2, -transform.up, Mathf.Infinity, groundLayer);

        if (r1 && r2)
        {
            grounded = true;
            return (r2.point - r1.point).normalized;
        }

        grounded = false;
        return transform.right;
    }

    private bool TryUpdateGroundPoint(out float distance, out Vector2 direction)
    {
        direction = -transform.up;
        var r = Physics2D.Raycast(heightReferencePoint.position, direction, Mathf.Infinity, groundLayer);
        if (r)
        {
            distance = r.distance;
            lastComputedGroundPoint = r.point;
            return true;
        }
        
        distance = Mathf.Infinity;
        return false;
    }
}