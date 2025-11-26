using UnityEngine;

public class TestMover : MonoBehaviour
{
    [SerializeField] float moveSpeed;

    OutfitManager outfitManager;
    Vector3 moveInput;
    MathTools.OrientationXZ moveDirection;

    private void Start()
    {
        outfitManager = GetComponent<OutfitManager>();
        moveDirection = MathTools.OrientationXZ.front;
        outfitManager.SetCurrentOutfit(true);
        outfitManager.SetFace(moveDirection);
    }

    // Update is called once per frame
    private void Update()
    {
        CaptureInput();
        UpdateOrientation();
        transform.position += Time.deltaTime * moveSpeed * moveInput;
    }

    private void CaptureInput()
    {
        moveInput.x = (Input.GetKey(KeyCode.RightArrow) ? 1 : 0) + (Input.GetKey(KeyCode.LeftArrow) ? -1 : 0);
        moveInput.z = (Input.GetKey(KeyCode.UpArrow) ? 1 : 0) + (Input.GetKey(KeyCode.DownArrow) ? -1 : 0);

        if (moveInput.x != 0 && moveInput.z != 0)
        {
            moveInput.x *= MathTools.cos45;
            moveInput.z *= MathTools.cos45;
        }
    }

    private void UpdateOrientation()
    {
        var o = MoveDirection();
        if (o != moveDirection)
        {
            moveDirection = o;
            outfitManager.SetFace(o);
        }
    }

    private MathTools.OrientationXZ MoveDirection()
    {
        if (moveInput.z != 0)
        {
            return moveInput.z > 0 ? MathTools.OrientationXZ.back : MathTools.OrientationXZ.front;
        }
        if (moveInput.x != 0)
        {
            return moveInput.x > 0 ? MathTools.OrientationXZ.right : MathTools.OrientationXZ.left;
        }

        return moveDirection;
    }
}
