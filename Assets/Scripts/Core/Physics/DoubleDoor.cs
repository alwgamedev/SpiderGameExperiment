using System;
using UnityEngine;

public class DoubleDoor : MonoBehaviour
{
    [SerializeField] Transform orientingTransform;//doors may be childed to a single transform that can flip on its x-axis to change direction
    [SerializeField] Transform door1;
    [SerializeField] Transform door2;
    [SerializeField] Transform door1EndPt;//for things like spider mouth where "door" is made up of several nested segments and endpt is not in the direction door.right
    [SerializeField] Transform door2EndPt;
    [SerializeField] float door1ClosedAngleRad;
    [SerializeField] float door1OpenAngleRad;
    [SerializeField] float door2ClosedAngleRad;
    [SerializeField] float door2OpenAngleRad;
    [SerializeField] float rotationSpeed;
    [SerializeField] float rotationTolerance;

    //in basis u = (door2 - door1).normalized, v = +/- u.CCWPerp() (depending on whether orientingTransform is facing right or left)
    Vector2 door1ClosedDirection;
    Vector2 door1OpenDirection;
    Vector2 door2ClosedDirection;
    Vector2 door2OpenDirection;

    float door1Width;
    float door2Width;
    float doorwayWidth;

    Mode mode;

    enum Mode
    {
        idle, opening, closing
    }

    public void Initialize()
    {
        door1ClosedDirection = new(Mathf.Cos(door1ClosedAngleRad), Mathf.Sin(door1ClosedAngleRad));
        door1OpenDirection = new(Mathf.Cos(door1OpenAngleRad), Mathf.Sin(door1OpenAngleRad));
        door2ClosedDirection = new(Mathf.Cos(door2ClosedAngleRad), Mathf.Sin(door2ClosedAngleRad));
        door2OpenDirection = new(Mathf.Cos(door2OpenAngleRad), Mathf.Sin(door2OpenAngleRad));

        door1Width = Vector2.Distance(door1.position, door1EndPt.position);
        door2Width = Vector2.Distance(door2.position, door2EndPt.position);
        doorwayWidth = Vector2.Distance(door1.position, door2.position);
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
        SnapToGoal(door1OpenDirection, door2OpenDirection);
    }

    public void SnapClosed()
    {
        SnapToGoal(door1ClosedDirection, door2ClosedDirection);
    }


    //for editor
    public void CaptureDoor1Open()
    {
        Vector2 u = (door2.position - door1.position).normalized;
        var v = orientingTransform.localScale.x > 0 ? u.CCWPerp() : u.CWPerp();
        Vector2 w = (door1EndPt.position - door1.position).normalized;
        w = new(Vector2.Dot(w, u), Vector2.Dot(w, v));
        door1OpenAngleRad = Mathf.Atan2(w.y, w.x);
    }

    public void CaptureDoor1Closed()
    {
        Vector2 u = (door2.position - door1.position).normalized;
        var v = orientingTransform.localScale.x > 0 ? u.CCWPerp() : u.CWPerp();
        Vector2 w = (door1EndPt.position - door1.position).normalized;
        w = new(Vector2.Dot(w, u), Vector2.Dot(w, v));
        door1ClosedAngleRad = Mathf.Atan2(w.y, w.x);
    }

    public void CaptureDoor2Open()
    {
        Vector2 u = (door2.position - door1.position).normalized;
        var v = orientingTransform.localScale.x > 0 ? u.CCWPerp() : u.CWPerp();
        Vector2 w = (door2EndPt.position - door2.position).normalized;
        w = new(Vector2.Dot(w, u), Vector2.Dot(w, v));
        door2OpenAngleRad = Mathf.Atan2(w.y, w.x);
    }

    public void CaptureDoor2Closed()
    {
        Vector2 u = (door2.position - door1.position).normalized;
        var v = orientingTransform.localScale.x > 0 ? u.CCWPerp() : u.CWPerp();
        Vector2 w = (door2EndPt.position - door2.position).normalized;
        w = new(Vector2.Dot(w, u), Vector2.Dot(w, v));
        door2ClosedAngleRad = Mathf.Atan2(w.y, w.x);
    }

    private void OnValidate()
    {
        Initialize();
    }

    private void FixedUpdate()
    {
        switch (mode)
        {
            case Mode.opening:
                if (!RotateTowardsGoal(door1OpenDirection, door2OpenDirection, rotationSpeed * Time.deltaTime))
                {
                    mode = Mode.idle;
                }
                break;
            case Mode.closing:
                if (!RotateTowardsGoal(door1ClosedDirection, door2ClosedDirection, rotationSpeed * Time.deltaTime))
                {
                    mode = Mode.idle;
                }
                break;
        }
    }

    private void SnapToGoal(Vector2 goal1, Vector2 goal2)
    {
        Vector2 u = (door2.position - door1.position) / doorwayWidth;
        var v = orientingTransform.localScale.x > 0 ? u.CCWPerp() : u.CWPerp();

        goal1 = goal1.x * u + goal1.y * v;
        goal2 = goal2.x * u + goal2.y * v;

        Vector2 cur1 = (door1EndPt.position - door1.position) / door1Width;
        Vector2 cur2 = (door2EndPt.position - door2.position) / door2Width;

        Vector2 w1 = MathTools.FromToRotation(cur1, goal1, (Vector2)door1.transform.right, true);
        Vector2 w2 = MathTools.FromToRotation(cur2, goal2, (Vector2)door2.transform.right, true);

        door1.rotation = MathTools.QuaternionFrom2DUnitVector(w1);
        door2.rotation = MathTools.QuaternionFrom2DUnitVector(w2);
    }

    private bool RotateTowardsGoal(Vector2 goal1, Vector2 goal2, float lerpAmount)
    {
        Vector2 u = (door2.position - door1.position) / doorwayWidth;
        var v = orientingTransform.localScale.x > 0 ? u.CCWPerp() : u.CWPerp();

        goal1 = goal1.x * u + goal1.y * v;
        goal2 = goal2.x * u + goal2.y * v;

        Vector2 cur1 = (door1EndPt.position - door1.position) / door1Width;
        Vector2 cur2 = (door2EndPt.position - door2.position) / door2Width;

        door1.ApplyCheapRotationalLerpClamped(cur1, goal1, lerpAmount, out var changed1);
        door2.ApplyCheapRotationalLerpClamped(cur2, goal2, lerpAmount, out var changed2);

        return changed1 || changed2;
    }
}