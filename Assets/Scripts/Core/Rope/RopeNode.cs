using UnityEngine;

public struct RopeNode
{
    bool anchored;
    Vector2 acceleration;
    Vector2 lastPosition;
    Vector2 position;
    float drag;
    float collisionRadius;
    float collisionBounciness;
    int collisionIterations;

    public bool Anchored => anchored;
    public Vector2 Position
    {
        get => position;
        set
        {
            position = value;
            ResolveCollisions();
        }
    }

    public RopeNode(Vector2 position, Vector2 velocity, Vector2 acceleration, float drag, 
        float collisionRadius, float collisionBounciness, int collisionIterations, bool anchored)
    {
        this.anchored = anchored;
        this.position = position;
        lastPosition = position - velocity * Time.fixedDeltaTime;
        this.acceleration = acceleration;
        this.drag = drag;
        this.collisionRadius = collisionRadius;
        this.collisionBounciness = collisionBounciness;
        this.collisionIterations = collisionIterations;
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

        var p = Position;
        var v = (p - lastPosition) / dt;
        Position += p - lastPosition + dt2 * (acceleration - drag * v.magnitude * v);//USE PROPERTY POSITION SO THAT COLLISIONS GET UPDATED
        lastPosition = p;
    }

    private void ResolveCollisions()
    {
        if (anchored) return;

        int i = 0;
        while (i < collisionIterations) 
        {
            var p = Position;
            var v = (p - lastPosition) / Time.deltaTime;
            var u = v.normalized;
            //var spd = v.magnitude;
            //if (spd < 10E-05f)
            //{
            //    return;
            //}
            //var u = v / spd;
            var r = Physics2D.Raycast(p, u, collisionRadius);
            if (r)
            {
                var d = collisionRadius - r.distance;
                var n = r.distance > 0 ? r.normal : -u;
                position = p + collisionBounciness * Vector2.Dot(-v, r.normal) * d * n;
                //if (r.distance == 0)
                //{
                //    position = lastPosition;
                //}
                //else
                //{
                //    var d = collisionRadius - r.distance;
                //    position = p + collisionBounciness * Vector2.Dot(-v, r.normal) * d * r.normal;
                //}
            }
            else
            {
                break;
            }

            i++;
        }
    }

    //for now we'll just have constant acceleration from gravity
    //but later we could add methods for adding force
}