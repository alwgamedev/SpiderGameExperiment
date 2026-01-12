using UnityEngine;

public class InventoryDoor : MonoBehaviour
{
    [SerializeField] Animator animator;

    int openParameter;

    private void Start()
    {
        openParameter = Animator.StringToHash("inventoryDoorOpen");
    }

    public void SetOpen(bool val)
    {
        animator.SetBool(openParameter, val);
    }
}