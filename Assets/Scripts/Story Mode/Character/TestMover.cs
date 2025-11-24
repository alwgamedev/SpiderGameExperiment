using UnityEngine;

public class TestMover : MonoBehaviour
{
    [SerializeField] float moveSpeed;

    Vector3 moveInput;

    // Update is called once per frame
    void Update()
    {
        moveInput.x = (Input.GetKey(KeyCode.RightArrow) ? 1 : 0) + (Input.GetKey(KeyCode.LeftArrow) ? -1 : 0);
        moveInput.z = (Input.GetKey(KeyCode.UpArrow) ? 1 : 0) + (Input.GetKey(KeyCode.DownArrow) ? -1 : 0);

        if (moveInput.x != 0 && moveInput.z != 0)
        {
            moveInput.x *= MathTools.cos45;
            moveInput.z *= MathTools.cos45;
        }

        transform.position += Time.deltaTime * moveSpeed * moveInput;
    }
}
