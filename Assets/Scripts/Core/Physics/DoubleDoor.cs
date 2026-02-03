using System;
using UnityEngine;

public class DoubleDoor : MonoBehaviour
{
    [SerializeField] Transform door1;
    [SerializeField] Transform door2;
    [SerializeField] Quaternion door1Open;
    [SerializeField] Quaternion door1Closed;
    [SerializeField] Quaternion door2Open;
    [SerializeField] Quaternion door2Closed;
    [SerializeField] float rotationSpeed;
    [SerializeField] float rotationTolerance;

    Mode mode;

    enum Mode
    {
        idle, opening, closing
    }

    public void Open()
    {
        mode = Mode.opening;
    }

    public void Close()
    {
        mode = Mode.closing;
    }

    public void SnapOpen()
    {
        SnapToGoal(door1Open, door2Open);
    }

    public void SnapClosed()
    {
        SnapToGoal(door1Closed, door2Closed);
    }

    public void CaptureDoor1Open()
    {
        door1Open = door1.localRotation;
    }

    public void CaptureDoor1Closed()
    {
        door1Closed = door1.localRotation;
    }

    public void CaptureDoor2Open()
    {
        door2Open = door2.localRotation;
    }

    public void CaptureDoor2Closed()
    {
        door2Closed = door2.localRotation;
    }

    private void FixedUpdate()
    {
        switch (mode)
        {
            case Mode.opening:
                if (!RotateTowardsGoal(door1Open, door2Open, rotationSpeed * Time.deltaTime))
                {
                    mode = Mode.idle;
                }
                break;
            case Mode.closing:
                if (!RotateTowardsGoal(door1Closed, door2Closed, rotationSpeed * Time.deltaTime))
                {
                    Debug.Log($"{gameObject.name} close complete.");
                    mode = Mode.idle;
                }
                else
                {
                    Debug.Log($"{gameObject.name} closing...");
                }
                break;
        }
    }

    private void SnapToGoal(Quaternion goal1, Quaternion goal2)
    {
        door1.localRotation = goal1;
        door2.localRotation = goal2;
    }

    private bool RotateTowardsGoal(Quaternion goal1, Quaternion goal2, float lerpAmount)
    {
        //q & -q represent the same rotation, so take abs of the dot
        var min = Mathf.Min(Mathf.Abs(Quaternion.Dot(door1.localRotation, goal1)), Mathf.Abs(Quaternion.Dot(door2.localRotation, goal2)));
        if (min > rotationTolerance)
        {
            return false;
        }

        door1.localRotation = Quaternion.Lerp(door1.localRotation, goal1, lerpAmount);
        door2.localRotation = Quaternion.Lerp(door2.localRotation, goal2, lerpAmount);
        return true;
    }
}