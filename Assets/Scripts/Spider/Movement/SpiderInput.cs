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

    public Vector2 MoveInput => ArrowsAction.ReadValue<Vector2>();
    public Vector2 SecondaryInput => WASDAction.ReadValue<Vector2>();
    
    //I thought it would be good to have these all in one central place
    //-- e.g. you can disable all player input here, instead of disabling input handling in every script

    //we can enable/disable input *capturing* here
    //we can also enable/disable input *handling* on individual dependent scripts

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
    }
}