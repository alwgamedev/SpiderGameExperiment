using UnityEngine;

public struct RopeNode
{
    public Vector3 position;
    public Vector3 lastPosition;
    public Vector3 acceleration;

    bool anchored;
    float drag;
    float collisionRadius;
    float collisionThreshold;
    float collisionBounciness;

    //public readonly Rigidbody2D rb;

    public bool Anchored => anchored;

    public RopeNode(Vector2 position, Vector2 velocity, Vector2 acceleration, float drag,
        /*CircleCollider2D prefab,*/ float collisionRadius, float collisionThreshold, float collisionBounciness, 
        bool anchored)
    {
        this.anchored = anchored;
        this.position = position;
        lastPosition = position - velocity * Time.fixedDeltaTime;
        this.acceleration = acceleration;
        this.drag = drag;
        this.collisionRadius = collisionRadius;
        this.collisionThreshold = collisionThreshold;
        this.collisionBounciness = collisionBounciness;

        //CircleCollider2D c = Object.Instantiate(prefab, position, Quaternion.identity);
        //c.radius = collisionThreshold;
        //rb = c.attachedRigidbody;
        //if (anchored)
        //{
        //    rb.bodyType = RigidbodyType2D.Kinematic;
        //}

        //lastPositionBeforeCollision = position;
        //this.collisionBounciness = collisionBounciness;
        //this.collisionIterations = collisionIterations;
        //incomingCollision = new RaycastHit2D();
    }

    //public void MoveRigidbody()
    //{
    //    //transform.position = position;
    //    //rb.MovePosition(position);
    //    if (anchored)
    //    {
    //        rb.MovePosition(position);
    //    }
    //    else
    //    {
    //        rb.linearVelocity = (position - lastPosition) / Time.deltaTime;
    //    }
    //}

    public void Anchor()
    {
        anchored = true;
        lastPosition = position;
    }

    public void DeAnchor(Vector3 initialVelocity)
    {
        anchored = false;
        lastPosition = position - initialVelocity * Time.fixedDeltaTime;
    }

    public void UpdateVerletSimulation(float dt, float dt2, ContactFilter2D collisionContactFilter, Collider2D[] collisionBuffer)
    {
        if (anchored) return;

        UpdateVerletSimulationCore(dt, dt2);
    }

    private void UpdateVerletSimulationCore(float dt, float dt2)
    {
        var p = position;
        position += NextPositionStep(dt, dt2);//USE PROPERTY POSITION SO THAT COLLISIONS GET UPDATED
        lastPosition = p;
    }

    public void ResolveCollisions(float dt, ContactFilter2D contactFilter, Collider2D[] buffer)
    {
        //another idea: do a single overlap circle.
        //if there's a collision (within collisionThreshold), resolve it (get new pos), and repeat (up to max_num_collisions times)
        //if no overlap circle or closest point is outside collisionThreshold, we can stop iterating

        Physics2D.OverlapCircle(position, collisionRadius, contactFilter, buffer);

        foreach (var c in buffer)
        {
            HandlePotentialCollision(dt, c);
        }
    }

    private void HandlePotentialCollision(float dt, Collider2D c)
    {
        if (c == null) return;
        var p = c.ClosestPoint(position);
        var l = Vector2.Distance(position, p);
        if (l <= 10E-05f)
        {
            //2do...
            p = position + (position - lastPosition).normalized * 0.1f * collisionThreshold;
            l = 0.1f * collisionThreshold;
        }
        if (l < collisionThreshold)
        {
            ResolveCollision(dt, p, l);

        }
    }

    private void ResolveCollision(float dt, Vector3 contactPoint, float distanceToContactPoint)
    {
        if (distanceToContactPoint > 10E-05f)
        {
            var collisionOffset = contactPoint - position;
            var collisionNormal = -collisionOffset / distanceToContactPoint;
            var velocity = (position - lastPosition) / dt;
            if (Vector2.Dot(velocity, collisionNormal) < 0)
            {
                position += (collisionThreshold - distanceToContactPoint) * collisionNormal;
            }
            else
            {
                var spd = velocity.magnitude;
                if (spd > 10E-05f)
                {
                    var velocityDirection = velocity / spd;
                    var y = Vector2.Dot(collisionOffset, velocityDirection);
                    var distanceTravelledSinceCollision = -y
                        + Mathf.Sqrt(y * y + collisionThreshold * collisionThreshold - distanceToContactPoint * distanceToContactPoint);
                    var timeSinceCollision = distanceTravelledSinceCollision / spd;
                    var newVelocity = collisionBounciness * (2 * Vector2.Dot(velocity, collisionNormal) * collisionNormal - velocity);
                    position += -distanceTravelledSinceCollision * velocityDirection
                        + newVelocity * timeSinceCollision;
                    lastPosition = position - newVelocity * dt;
                }
                else
                {
                    position += (collisionThreshold - distanceToContactPoint) * collisionNormal;
                }
            }
            //lastPosition = position - collisionBounciness * MathTools.ReflectAcrossHyperplane(position - lastPosition, u);

            //remember to set lastPos! (to reflect new velocity
            //or let's try it without first... (bc it's more accurate to the verlet model)
            //but setting the lastPos will give stronger bounceback on collision
        }
    }

    //public Vector3 LastStepVelocity(float dt)
    //{
    //    return (position - lastPosition) / dt;
    //}

    public Vector3 NextPositionStep(float dt, float dt2)
    {
        var d = position - lastPosition;
        var v = d / dt;
        return d + dt2 * (acceleration - drag * v.magnitude * v);
    }

    //for now we'll just have constant acceleration from gravity
    //but later we could add methods for adding force
}