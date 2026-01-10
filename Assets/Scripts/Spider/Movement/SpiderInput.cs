using UnityEngine;
using UnityEngine.InputSystem;

public class SpiderInput : MonoBehaviour
{
    PlayerInput playerInput;

    InputAction ArrowsAction;
    InputAction WASDAction;
    InputAction SpaceAction;
    InputAction ControlAction;
    InputAction ShiftAction;
    InputAction ZAction;

    public Vector2 MoveInput => ArrowsAction.ReadValue<Vector2>();
    public Vector2 SecondaryInput => WASDAction.ReadValue<Vector2>();
    public bool SpaceHeld => SpaceAction.IsPressed();
    public bool ControlHeld => ControlAction.IsPressed();
    public bool ShiftHeld => ShiftAction.IsPressed();
    public bool ZHeld => ZAction.IsPressed();

    //idea these will respond to input actions
    //then dependent scripts like MC will check these fields in update and handle them appropriately

    //we can enable/disable input *capturing* here
    //we can also enable/disable input *handling* on dependent scripts

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        ArrowsAction = playerInput.actions["Arrows"];
        WASDAction = playerInput.actions["WASD"];
        SpaceAction = playerInput.actions["SpaceAction"];
        ControlAction = playerInput.actions["ControlAction"];
        ShiftAction = playerInput.actions["ShiftAction"];
        ZAction = playerInput.actions["ZAction"];
    }
}