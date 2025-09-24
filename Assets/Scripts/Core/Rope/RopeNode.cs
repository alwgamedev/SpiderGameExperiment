using Unity.Mathematics;
using UnityEngine;

public struct RopeNode
{
    public Vector2 position;
    public Vector2 lastPosition;
    public Vector2 acceleration;

    bool anchored;
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

    public void ResolveCollisions()
    {
        //maybe instead of trying to handle a murky case where we are already inside collider,
        //we should do a better job of preventing that (i.e. check for "pass-through" collisions coming in next step,
        //and deal with it then or leave some data to know it's coming (we are already doing a larger than necessary overlap circle))

        var colls = Physics2D.OverlapCircleAll(position, collisionRadius);
        foreach (var c in colls)
        {
            var p = c.ClosestPoint(position);
            var l = Vector2.Distance(position, p);
            if (l <= 10E-05f)
            {
                //2do...
                p = 2 * position - lastPosition;
                l = Vector2.Distance(position, p);
            }
            if (l < collisionThreshold && l > 10E-05f)
            {
                var u = (position - p) / l;
                l = collisionThreshold - l;
                position += l * u;
                lastPosition = position - collisionBounciness * MathTools.ReflectAcrossHyperplane(position - lastPosition, u);
            }
        }

        //ResolveCollisions(1, Time.deltaTime, 0, out var dt);
        //if (dt != 0)
        //{
        //    UpdateVerletSimulation(dt, dt * dt);
        //}
    }

    //private void ResolveCollisions(int iteration, float lastDt, float dtRemaining, out float newDtRemaining)
    //{
    //    newDtRemaining = dtRemaining;
    //    if (anchored || lastDt == 0 || iteration > 5) return;

    //    var r = Physics2D.Linecast(lastPosition, position);
    //    if (r && r.distance > 0)
    //    {
    //        var v = position - lastPosition;//(position - lastPosition) / lastDt;
    //        var spd = v.magnitude;
    //        if (spd > 10E-05f)
    //        {
    //            var distToCollision = r.distance - collisionThreshold;
    //            var timeOfCollision = distToCollision / spd;
    //            newDtRemaining += lastDt - timeOfCollision;
    //            lastDt = timeOfCollision;
    //            position = lastPosition + timeOfCollision * v;//set position to where it would have been at time collision was detected;
    //            if (ResolveCollision(r.point, distToCollision))
    //            {
    //                ResolveCollisions(++iteration, lastDt, newDtRemaining, out newDtRemaining);
    //            }
    //        }
    //    }

    //    //var colls = Physics2D.OverlapCircleAll(lastPosition, collisionRadius);
    //    //foreach (var c in colls)
    //    //{
    //    //    var p = c.ClosestPoint(position);
    //    //    var l = Vector2.Distance(position, p);
    //    //    if (l < collisionThreshold && l > 10E-05f)
    //    //    {
    //    //        var u = (position - p) / l;
    //    //        l = collisionThreshold - l;
    //    //        position += l * u;
    //    //        //lastPosition = position - collisionBounciness * MathTools.ReflectAcrossHyperplane(position - lastPosition, u);
    //    //    }
    //    //}
    //}

    private void ResolveCollision(Vector2 contactPoint, float distanceToContactPoint)
    {
        if (distanceToContactPoint > 10E-05f)
        {
            var u = (position - contactPoint) / distanceToContactPoint;
            position += (collisionThreshold - distanceToContactPoint) * u;
        }
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