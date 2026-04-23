using System;
using UnityEngine;
using UnityEngine.InputSystem;

[Serializable]
public class SpiderInput
{
    [SerializeField] PlayerInput playerInput;

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

    //yes, we're basically still using the input system legacy style (just reading values in update rather than subscribing to actions),
    //which you can just do through Keyboard.current,
    //but the nice thing about having all the input in one central place is you can disable all player input here, instead of disabling input handling in every script
    //(also allows us to set up input for console or other systems, like smart fridge)

    public void Initialize()
    {
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