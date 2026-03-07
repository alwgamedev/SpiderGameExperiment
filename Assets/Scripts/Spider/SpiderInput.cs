using UnityEngine;
using UnityEngine.InputSystem;

public class SpiderInput : MonoBehaviour
{
    PlayerInput playerInput;

    public InputAction ArrowsAction { get; private set; }
    public InputAction WASDAction { get; private set; }
    public InputAction SpaceAction { get; private set; }
    public InputAction ControlAction { get; private set; }
    public InputAction ShiftAction { get; private set; }
    public InputAction QAction { get; private set; }
    public InputAction ZAction { get; private set; }
    public InputAction FAction { get; private set; }

    public Vector2 MoveInput => ArrowsAction.ReadValue<Vector2>();
    public Vector2 SecondaryInput => WASDAction.ReadValue<Vector2>();
    
    //yes, you can get still get key presses through Keyboard.current, but
    //this allows you to disable all player input at once, instead of disabling input handling in every script

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        ArrowsAction = playerInput.actions["Arrows"];
        WASDAction = playerInput.actions["WASD"];
        SpaceAction = playerInput.actions["SpaceAction"];
        ControlAction = playerInput.actions["ControlAction"];
        ShiftAction = playerInput.actions["ShiftAction"];
        QAction = playerInput.actions["QAction"];
        ZAction = playerInput.actions["ZAction"];
        FAction = playerInput.actions["FAction"];
    }
}