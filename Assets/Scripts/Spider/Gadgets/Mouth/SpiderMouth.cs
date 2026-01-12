using UnityEngine;
using UnityEngine.InputSystem;

public class SpiderMouth : MonoBehaviour
{
    [SerializeField] Animator animator;

    int mouthOpenParameter;

    private void Start()
    {
        mouthOpenParameter = Animator.StringToHash("mouthOpen");
    }

    private void Update()
    {
        animator.SetBool(mouthOpenParameter, Keyboard.current.pKey.isPressed);
    }
}
