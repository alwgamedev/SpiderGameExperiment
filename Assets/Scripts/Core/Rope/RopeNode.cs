using Unity.Mathematics;
using UnityEngine;

public struct RopeNode
{
    public Vector2 position;

    bool anchored;
    Vector2 acceleration;
    Vector2 lastPosition;
    float drag;
    float collisionRadius;
    float collisionThreshold;
    float collisionBounciness;
    //int collisionIterations;

    //RaycastHit2D incomingCollision;//in future we may store an array

    //Vector2 lastPositionBeforeCollision;

    public bool Anchored => anchored;
    //public Vector2 Position
    //{
    //    get => position;
    //    set
    //    {
    //        position = value;
    //        ResolveCollisions();
    //    }
    //}


    public RopeNode(Vector2 position, Vector2 velocity, Vector2 acceleration, float drag, 
        float collisionRadius, float collisionThreshold, float collisionBounciness, /*int collisionIterations,*/ bool anchored)
    {
        this.anchored = anchored;
        this.position = position;
        lastPosition = position - velocity * Time.fixedDeltaTime;
        this.acceleration = acceleration;
        this.drag = drag;
        this.collisionRadius = collisionRadius;
        this.collisionThreshold = collisionThreshold;
        this.collisionBounciness = collisionBounciness;
        //lastPositionBeforeCollision = position;
        //this.collisionBounciness = collisionBounciness;
        //this.collisionIterations = collisionIterations;
        //incomingCollision = new RaycastHit2D();
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
        var v = (p - lastPosition) / dt;
        position += NextPositionStep(dt, dt2);//USE PROPERTY POSITION SO THAT COLLISIONS GET UPDATED
        lastPosition = p;
    }

    public void ResolveCollisions(/*float dt, float dt2*/)
    {
        if (anchored) return;

        //to do: better system

        var colls = Physics2D.OverlapCircleAll(position, collisionRadius);
        foreach(var c in colls)
        {
            var p = c.ClosestPoint(position);
            var l = Vector2.Distance(position, p);
            //2D0: handle the case l <= 10E-05f (i.e. we ended up inside collider before we ever detected collision)
            //essential for high speed collisions (with very frequent fixed updates (like 200 per sec) the issue is almost gone with system as is
            //basically we need to INTERPOLATE (look at what they discuss in that blogpost)
            //(e.g. raycast from position - collisionRadius * velocity.normalized
            if (l < collisionThreshold && l > 10E-05f)
            {
                var u = (position - p) / l;
                l = collisionThreshold - l;
                position += l * u;
                lastPosition = position - collisionBounciness * MathTools.ReflectAcrossHyperplane(position - lastPosition, u);
            }
        }

        //int i = 0;
        //while (i < collisionIterations) 
        //{
        //    var p = Position;
        //    var v = (p - lastPosition) / Time.deltaTime;
        //    var u = v.normalized;
        //    //var spd = v.magnitude;
        //    //if (spd < 10E-05f)
        //    //{
        //    //    return;
        //    //}
        //    //var u = v / spd;
        //    var r = Physics2D.Raycast(p, u, collisionRadius);
        //    if (r)
        //    {
        //        var d = collisionRadius - r.distance;
        //        var n = r.distance > 0 ? r.normal : -u;
        //        position = p + collisionBounciness * Vector2.Dot(-v, r.normal) * d * n;
        //        //if (r.distance == 0)
        //        //{
        //        //    position = lastPosition;
        //        //}
        //        //else
        //        //{
        //        //    var d = collisionRadius - r.distance;
        //        //    position = p + collisionBounciness * Vector2.Dot(-v, r.normal) * d * r.normal;
        //        //}
        //    }
        //    else
        //    {
        //        break;
        //    }

        //    i++;
        //}
    }

    public Vector2 LastStepVelocity(float dt)
    {
        return (position - lastPosition) / dt;
    }

    public Vector2 NextPositionStep(float dt, float dt2)
    {
        var d = position - lastPosition;
        var v = d / dt;
        return d + dt2 * (acceleration - drag * v.magnitude * v);
    }

    //for now we'll just have constant acceleration from gravity
    //but later we could add methods for adding force
}