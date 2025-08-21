using UnityEngine;

public class SpiderController : MonoBehaviour
{
    [SerializeField] float accelFactor;
    [SerializeField] float maxSpeed;

    LegSynchronizer legSynchronizer;
    Rigidbody2D rb;
    int moveInput;
    Vector2 groundDirection = Vector2.right;
    int orientation => transform.localScale.x > 0 ? 1 : -1;

    int groundLayer;
    const float groundRaycastHorizontalSpacing = .1f;

    private void Awake()
    {
        legSynchronizer = GetComponent<LegSynchronizer>();
        rb = GetComponent<Rigidbody2D>();
        groundLayer = LayerMask.GetMask("Ground");
    }

    private void Update()
    {
        moveInput = (Input.GetKey(KeyCode.RightArrow) ? 1 : 0) + (Input.GetKey(KeyCode.LeftArrow) ? -1 : 0);
        if (moveInput * orientation < 0)
        {
            ChangeDirection();
        }
    }

    private void FixedUpdate()
    {
        HandleMoveInput();
    }

    private void ChangeDirection()
    {
        var s = transform.localScale;
        transform.localScale = new Vector3(-s.x, s.y, s.z);
        legSynchronizer.OnBodyChangedDirection();
    }

    private void HandleMoveInput()
    {
        if (moveInput == 0) return;

        groundDirection = GroundDirection();
        var d = groundDirection * moveInput;
        var spd = Vector2.Dot(rb.linearVelocity, d);
        if (spd > maxSpeed) return;
        rb.AddForce(accelFactor * (maxSpeed - spd) * rb.mass * d);
    }

    private Vector2 GroundDirection()
    {
        Vector2 pos = transform.position + transform.up;
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