using UnityEngine;

public class TestObstacle : MonoBehaviour
{
    [SerializeField] float moveSpeed;

    Rigidbody2D rb;
    Vector2 input;

    //just to test transfer of rb velocity to fluid

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        input.x = (Input.GetKey(KeyCode.LeftArrow) ? -1 : 0) + (Input.GetKey(KeyCode.RightArrow) ? 1 : 0);
        input.y = (Input.GetKey(KeyCode.DownArrow) ? -1 : 0) + (Input.GetKey(KeyCode.UpArrow) ? 1 : 0);
        if (input.x != 0 && input.y != 0)
        {
            input *= MathTools.cos45;
        }
    }

    private void FixedUpdate()
    {
        rb.linearVelocity = moveSpeed * input;
    }
}