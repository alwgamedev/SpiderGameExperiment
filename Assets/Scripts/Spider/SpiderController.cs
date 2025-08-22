using UnityEngine;

public class SpiderController : MonoBehaviour
{
    [SerializeField] float accelFactor;
    [SerializeField] float decelFactor;
    [SerializeField] float maxSpeed;
    [SerializeField] float preferredRideHeight;
    [SerializeField] float heightSpringForce;
    [SerializeField] float heightSpringDamping;
    [SerializeField] Transform heightReferencePoint;

    LegSynchronizer legSynchronizer;
    Rigidbody2D rb;
    int moveInput;

    Vector2 lastComputedGroundDirection = Vector2.right;

    int groundLayer;
    const float groundRaycastHorizontalSpacing = .1f;

    int Orientation => transform.localScale.x > 0 ? 1 : -1;

    private void Awake()
    {
        legSynchronizer = GetComponent<LegSynchronizer>();
        rb = GetComponent<Rigidbody2D>();
        groundLayer = LayerMask.GetMask("Ground");
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
        HandleMoveInput();
        UpdateHeightSpring();
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
        if (moveInput != 0 && spd < maxSpeed)
        {
            rb.AddForce(accelFactor * (maxSpeed - spd) * rb.mass * d);
        }
        else if (moveInput == 0)
        {
            //have to do this because there's no friction, since we're not in contact with ground
            rb.AddForce(decelFactor * -spd * rb.mass * d);
        }
    }

    private void UpdateHeightSpring()
    {
        var u = lastComputedGroundDirection.CWPerp();
        var r = Physics2D.Raycast(heightReferencePoint.position, lastComputedGroundDirection.CWPerp(), Mathf.Infinity, groundLayer);
        if (r)
        {
            var l = r.distance - preferredRideHeight;
            var down = lastComputedGroundDirection.CWPerp();
            var f = heightSpringForce * l * u;
            var v = Vector2.Dot(rb.linearVelocity, u) * u;
            rb.AddForce(rb.mass * (f - heightSpringDamping * v));
        }
    }

    private void BalanceRotation()
    {

    }

    private Vector2 GroundDirection()
    {
        Vector2 pos = heightReferencePoint.position;
        Vector2 tRight = transform.right;
        var o1 = pos - groundRaycastHorizontalSpacing * tRight;
        var o2 = pos + groundRaycastHorizontalSpacing * tRight;
        var r1 = Physics2D.Raycast(o1, -transform.up, groundLayer);
        var r2 = Physics2D.Raycast(o2, -transform.up, groundLayer);

        if (r1 && r2)
        {
            return (r2.point - r1.point).normalized;
        }

        return transform.right;
    }


}