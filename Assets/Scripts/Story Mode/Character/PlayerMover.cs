using UnityEngine;
using UnityEngine.AI;

public class PlayerMover : MonoBehaviour
{
    [SerializeField] float moveSpeed;
    [SerializeField] float rotationSpeed;
    [SerializeField] float navMeshSampleDistance;
    [SerializeField] float groundRaycastHorizontalBuffer;
    [SerializeField] float groundRaycastHeightBuffer;
    [SerializeField] Transform navMeshAnchor;

    OutfitManager outfitManager;
    CharacterAnimationControl animator;
    Vector3 moveInput;
    MathTools.OrientationXZ moveDirection;

    int groundLayer;

    private void Awake()
    {
        groundLayer = LayerMask.GetMask("Ground");
        outfitManager = GetComponent<OutfitManager>();
        animator = GetComponent<CharacterAnimationControl>();
    }

    private void Start()
    {
        moveDirection = MathTools.OrientationXZ.front;
        outfitManager.SetCurrentOutfit(true);
        outfitManager.SetFace(moveDirection);
        //initial snap to navmesh
        if (NavMesh.SamplePosition(transform.position, out var h, navMeshSampleDistance, NavMesh.AllAreas))
        {
            transform.position = h.position + transform.position - navMeshAnchor.position;
        }
    }

    private void Update()
    {
        CaptureInput();
        UpdateOrientation();
        var moved = HandleMoveInput();
        animator.UpdateMoveSpeed(moved ? moveSpeed : 0, Time.deltaTime);
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
            animator.OnOrientationChanged(o);
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

    private bool HandleMoveInput()
    {
        var result = false;
        if (moveInput != Vector3.zero)
        { 
            var p = navMeshAnchor.position + Time.deltaTime * moveSpeed * moveInput;
            if (NavMesh.SamplePosition(p, out var h, navMeshSampleDistance, NavMesh.AllAreas))
            {
                transform.position = h.position + transform.position - navMeshAnchor.position;
                result = true;
            }
        }
        var q = navMeshAnchor.position + groundRaycastHorizontalBuffer * moveInput.ApplyTransformation(transform.right, transform.up, transform.forward) +
            groundRaycastHeightBuffer * transform.up;
        if (Physics.Raycast(q, -transform.up, out var r, navMeshSampleDistance, groundLayer))
        {
            transform.up = MathTools.CheapRotationalLerp(transform.up, r.normal, rotationSpeed * Time.deltaTime, out var changed);
        }

        return result;
    }
}