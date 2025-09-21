using UnityEngine;

public struct RopeNode
{
    bool anchored;
    Vector2 acceleration;
    Vector2 lastPosition;
    float drag;
    public Vector2 position;

    public bool Anchored => anchored;
    public Vector2 Position => position;

    public RopeNode(Vector2 position, Vector2 velocity, Vector2 acceleration, float drag, bool anchored)
    {
        this.anchored = anchored;
        this.position = position;
        lastPosition = position - velocity * Time.fixedDeltaTime;
        this.acceleration = acceleration;
        this.drag = drag;
    }

    public void Anchor()
    {
        anchored = true;
        lastPosition = position;
    }

    public void DeAnchor(Vector2 initialVelocity)
    {
        anchored = false;
        lastPosition = position - initialVelocity * Time.fixedDeltaTime;
    }

    public void UpdateVerletSimulation(float dt, float dt2)
    {
        if (anchored) return;

        var p = position;
        var v = (position - lastPosition) / dt;
        position += p - lastPosition + dt2 * (acceleration - drag * v.magnitude * v);
        lastPosition = p;
    }

    //for now we'll just have constant acceleration from gravity
    //but later we could add methods for adding force
}