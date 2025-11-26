using UnityEngine;
using UnityEngine.AI;

public class TestMover : MonoBehaviour
{
    [SerializeField] float moveSpeed;
    [SerializeField] Transform navMeshAnchor;

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

    private void Update()
    {
        CaptureInput();
        UpdateOrientation();
        HandleMoveInput();
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

    private void HandleMoveInput()
    {
        var p = navMeshAnchor.position + Time.deltaTime * moveSpeed * moveInput;
        if (NavMesh.SamplePosition(p, out var h, 5f, NavMesh.AllAreas))
        {
            transform.position = h.position + transform.position - navMeshAnchor.position;
        }
        else
        {
            Debug.Log("navmesh sample failed");
        }
    }
}
