using UnityEngine;
using Unity.U2D.Physics;
using UnityEngine.InputSystem;

public class MoverCastTest : MonoBehaviour
{
    [SerializeField] float timeScale;
    [SerializeField] float radius;
    [SerializeField] float acceleration;
    [SerializeField] float drag;
    [SerializeField] float collisionBounciness;
    [SerializeField] PhysicsQuery.QueryFilter filter;

    Vector2 position;
    Vector2 lastPosition;
    Vector2 lastCollisionNormal;

    Vector2 moveInput;

    private void OnValidate()
    {
        Time.timeScale = timeScale;
    }

    private void Start()
    {
        position = transform.position;
        lastPosition = position;
    }

    private void Update()
    {
        moveInput.x = (Keyboard.current.leftArrowKey.isPressed ? -1 : 0) + (Keyboard.current.rightArrowKey.isPressed ? 1 : 0);
        moveInput.y = (Keyboard.current.downArrowKey.isPressed ? -1 : 0) + (Keyboard.current.upArrowKey.isPressed ? 1 : 0);
        if (moveInput.x != 0 && moveInput.y != 0)
        {
            moveInput *= MathTools.cos45;
        }
    }

    //collision detection seems pretty much bullet proof
    //CastGeometry might be somewhat expensive with 100+ nodes doing this many times per frame, but we can cross that bridge when we get there
    //(we can likely reduce rope iterations, num rope nodes, and increase physics timestep now that collision is reliable)
    private void FixedUpdate()
    {
        var dp = position - lastPosition;
        Vector2 targetPosition = position + (1 - drag) * dp + Time.deltaTime * Time.deltaTime * acceleration * moveInput;

        var circle = new CircleGeometry()
        {
            center = position,
            radius = radius
        };

        dp = targetPosition - position;
        var castResults = PhysicsWorld.defaultWorld.CastGeometry(circle, dp, filter);

        if (castResults.Length > 0)
        {
            var result = castResults[0];
            var positionAtTimeOfImpact = position + result.fraction * dp;

            Vector2 normal;
            if (result.normal == Vector2.zero)
            {
                normal = lastCollisionNormal;
            }
            else
            {
                lastCollisionNormal = result.normal;
                normal = result.normal;
            }

            if (Vector2.Dot(dp, normal) < 0)
            {
                var tang = normal.CCWPerp();
                dp = -collisionBounciness * (dp - 2 * Vector2.Dot(dp, tang) * tang);
            }

            position = positionAtTimeOfImpact + (1 - result.fraction) * dp;
            lastPosition = position - dp;
            transform.position = position;
        }
        else
        {
            lastPosition = position;
            position = targetPosition;
            transform.position = position;
        }
    }
}
