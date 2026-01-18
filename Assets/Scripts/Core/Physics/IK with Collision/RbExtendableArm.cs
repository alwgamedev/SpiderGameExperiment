using UnityEngine;
using UnityEngine.InputSystem;

public class RbExtendableArm : MonoBehaviour
{
    [SerializeField] Rigidbody2D[] chain;
    [SerializeField] Rigidbody2D effector;
    [SerializeField] float springConstant;
    [SerializeField] float springForceMax;
    [SerializeField] float springDamping;
    [SerializeField] float tolerance;

    Vector2 targetPosition;

    private void Update()
    {
        if (Mouse.current.leftButton.isPressed)
        {
            targetPosition = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        }
    }

    private void FixedUpdate()
    {
        Vector2 diff = targetPosition - (Vector2)effector.transform.position;
        float l = Vector2.SqrMagnitude(diff);
        if (l > tolerance * tolerance)
        {
            l = Mathf.Sqrt(l);
            var u = diff / l;
            var a = Mathf.Min(springConstant * l, springForceMax);
            var v = Vector2.Dot(effector.linearVelocity, u);
            effector.AddForce((a - springDamping * v) * u);
        }
    }
}