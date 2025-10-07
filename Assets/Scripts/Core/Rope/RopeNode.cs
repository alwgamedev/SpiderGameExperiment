using System.Linq;
using UnityEngine;

public struct RopeNode
{
    public Vector2 position;
    public Vector2 lastPosition;
    public Vector2 acceleration;

    public float mass;

    Vector2 storedVelocity;
    readonly Rigidbody2D[] storedVelocityRbs;
    int storedVelocityPointer;

    Vector2 right;
    Vector2 up;

    bool anchored;
    float drag;

    int collisionMask;
    float collisionSearchRadius;
    float collisionThreshold;
    float collisionBounciness;
    readonly Vector2[] raycastDirections;
    //int currentCollisionLayerMask;
    Vector2 lastTrueCollisionNormal;

    public bool Anchored => anchored;
    public int CurrentCollisionLayerMask => CurrentCollision ? 1 << CurrentCollision.gameObject.layer : 0;//currentCollisionLayerMask;
    public Collider2D CurrentCollision { get; private set; }
    public float CollisionThreshold => collisionThreshold;

    public RopeNode(Vector2 position, Vector2 velocity, Vector2 acceleration, float mass, float drag,
        int collisionMask, float collisionThreshold, float collisionSearchRadiusBuffer, float collisionBounciness,
        bool anchored)
    {
        this.anchored = anchored;
        this.position = position;
        lastPosition = anchored ? position : position - Time.fixedDeltaTime * velocity;
        this.acceleration = acceleration;
        this.mass = mass;

        storedVelocity = Vector2.zero;
        storedVelocityRbs = new Rigidbody2D[Rope.MAX_NUM_COLLISIONS];
        storedVelocityPointer = 0;

        var r = velocity.normalized;
        if (r == Vector2.zero)
        {
            right = Vector2.right;
        }
        var u = r.CCWPerp();
        right = r;
        up = u;
        var rr = MathTools.cos45 * r;
        var uu = MathTools.cos45 * u;
        raycastDirections = new Vector2[]{ right, -right, up, -up, rr + uu, -rr - uu, -rr + uu, rr - uu };

        this.drag = drag;
        this.collisionMask = collisionMask;
        this.collisionThreshold = collisionThreshold;
        collisionSearchRadius = collisionThreshold + collisionSearchRadiusBuffer;
        this.collisionBounciness = collisionBounciness;
        //currentCollisionLayerMask = 0;
        CurrentCollision = null;
        lastTrueCollisionNormal = Vector2.zero;



    }

    public void Anchor()
    {
        anchored = true;
        lastPosition = position;
    }

    public void DeAnchor(float dt, Vector2 initialVelocity)
    {
        anchored = false;
        lastPosition = position - initialVelocity * dt;
    }

    public void DeAnchor(Vector2 lastPosOffset)
    {
        anchored = false;
        lastPosition = position - lastPosOffset;
    }

    public void UpdateVerletSimulation(float dt, float dt2)
    {
        if (anchored) return;

        UpdateLocalCoordinates();
        UpdateVerletSimulationCore(dt, dt2);
    }

    private void UpdateVerletSimulationCore(float dt, float dt2)
    {
        var p = position;
        position += NextPositionStep(dt, dt2);
        if (storedVelocity != Vector2.zero)
        {
            position += dt * storedVelocity;
            storedVelocity = Vector2.zero;
        }
        if (storedVelocityPointer != 0)
        {
            for (int i = 0; i < storedVelocityRbs.Length; i++)
            {
                storedVelocityRbs[i] = null;
            }
            storedVelocityPointer = 0;
        }
        lastPosition = p;
    }

    private void UpdateLocalCoordinates()
    {
        var r = (position - lastPosition).normalized;
        if (r != Vector2.zero)
        {
            right = r;
            up = r.CCWPerp();
            var rr = MathTools.cos45 * right;
            var uu = MathTools.cos45 * up;
            raycastDirections[0] = right;
            raycastDirections[1] = -right;
            raycastDirections[2] = up;
            raycastDirections[3] = -up;
            raycastDirections[4] = rr + uu;
            raycastDirections[5] = -rr - uu;
            raycastDirections[6] = -rr + uu;
            raycastDirections[7] = rr - uu;
        }
    }

    public void ResolveCollisions(float dt)
    {
        if (anchored) return;

        //var r = Physics2D.Raycast(position, raycastDirections[0], collisionThreshold, collisionMask);
        //if (r && r.distance == 0)
        //{
        //    r = Physics2D.Linecast(lastPosition, position, collisionMask);
        //}
        //else
        //{
        //    var r0 = r;
        //    //var d0 = d;
        //    var min = r && r.distance > 0 ? r.distance : Mathf.Infinity;
        //    for (int i = 1; i < raycastDirections.Length; i++)
        //    {
        //        var s = Physics2D.Raycast(position, raycastDirections[i], collisionThreshold, collisionMask);
        //        if (s && s.distance < min)
        //        {
        //            if (s.distance > 0)
        //            {
        //                r = s;
        //                min = s.distance;
        //            }
        //            else if (!r0 || r0.distance > 0)
        //            {
        //                r0 = s;
        //            }
        //        }
        //    }
        //    if (!r)
        //    {
        //        r = r0;
        //    }
        //}

        var l = collisionThreshold;
        var r = Physics2D.Raycast(position, raycastDirections[0], collisionSearchRadius, collisionMask);
        if (!r)
        {
            int i = 1;
            while (!r && i < raycastDirections.Length)
            {
                if (r)
                {
                    r = Physics2D.Raycast(position, raycastDirections[i], collisionSearchRadius, collisionMask);
                    break;
                }
                i++;
            }
        }
        else if (r.distance == 0)
        {
            l = collisionSearchRadius;
            //var c = l - collisionThreshold + Mathf.Epsilon;
            r = Physics2D.Raycast(position + l * raycastDirections[0], -raycastDirections[0], l, collisionMask);
            for (int i = 1; i < raycastDirections.Length; i++)
            {
                var s = Physics2D.Raycast(position + l * raycastDirections[i], -raycastDirections[i], l, collisionMask);
                if (s && s.distance > r.distance)
                {
                    r = s;
                }
            }
            //if (!r || r.distance < c)
            //{
            //    int i = 1;
            //    while ((!r || r.distance < c) && i < raycastDirections.Length)
            //    {
            //        var s = Physics2D.Raycast(position + l * raycastDirections[i], -raycastDirections[i], l, collisionMask);
            //        if (s.distance > r.distance)
            //        {
            //            r = s;
            //        }
            //        i++;
            //    }
            //}
            r.distance = l - r.distance;
            //2do: what to do if distance is still zero (entire search radius is inside collider)
        }

        if (r)
        {
            //2do: creating variable l is overhead
            HandlePotentialCollision(dt, r, l);
        }
        else
        {
            //currentCollisionLayerMask = 0;
            CurrentCollision = null;
            lastTrueCollisionNormal = Vector2.zero;//can try to get rid of this
        }
    }

    private void HandlePotentialCollision(float dt, RaycastHit2D r, float collisionThreshold)
    {
        //var p = r.point;
        var l = r.distance;
        var n = r.normal;

        if (l <= MathTools.o51)
        {
            n = lastTrueCollisionNormal != Vector2.zero ? lastTrueCollisionNormal : (lastPosition - position).normalized;
            var w = this.collisionThreshold * n;
            var velocity = (position - lastPosition) / dt;
            var a = Vector2.Dot(velocity, n);
            var newVelocity = collisionBounciness * Mathf.Sign(a) * (2 * a * n - velocity);
            position += w;
            lastPosition = position - newVelocity * dt;
            StoreCollisionVelocity(r.collider.attachedRigidbody, n);
            //currentCollisionLayerMask = 1 << r.collider.gameObject.layer;
            CurrentCollision = r.collider;
            return;
        }
        else
        {
            lastTrueCollisionNormal = n;
        }

        if (l < collisionThreshold)
        {
            ResolveCollision(dt, l, collisionThreshold, n, r.collider.attachedRigidbody);
            //currentCollisionLayerMask = 1 << r.collider.gameObject.layer;
            CurrentCollision = r.collider;
        }
        else
        {
            CurrentCollision = null;
            //currentCollisionLayerMask = 0;
            //but we don't set lastTrueCollisionNormal = 0, bc we still had a successful raycast, which could be useful for an upcoming collision!
        }
    }

    private void ResolveCollision(float dt, float distanceToContactPoint, float collisionThreshold, Vector2 collisionNormal, Rigidbody2D attachedRb)
    {
        var velocity = (position - lastPosition) / dt;
        var speed = velocity.magnitude;
        var diff = Mathf.Min(collisionThreshold - distanceToContactPoint, this.collisionThreshold);
        var a = Vector2.Dot(velocity, collisionNormal);
        var newVelocity = collisionBounciness * Mathf.Sign(a) * (2 * a * collisionNormal - velocity);

        if (speed > MathTools.o51)
        {
            var timeSinceCollision = diff / speed;
            position += diff * collisionNormal + newVelocity * timeSinceCollision;
            lastPosition = position - newVelocity * dt;

            StoreCollisionVelocity(attachedRb, collisionNormal);
        }
        else
        {
            //lastPosition = position;
            position += diff * collisionNormal;
            lastPosition = position - newVelocity * dt;
        }
    }

    private void StoreCollisionVelocity(Rigidbody2D attachedRb, Vector2 collisionNormal)
    {
        if (attachedRb)
        {
            if (storedVelocityPointer < storedVelocityRbs.Length && Vector2.Dot(attachedRb.linearVelocity, collisionNormal) > 0
                && !storedVelocityRbs.Contains(attachedRb))
            {
                storedVelocity += attachedRb.linearVelocity
                    + collisionBounciness * Vector2.Dot(attachedRb.linearVelocity, collisionNormal) * collisionNormal;
                storedVelocityRbs[storedVelocityPointer] = attachedRb;
                storedVelocityPointer++;
            }
        }
    }

    public Vector2 NextPositionStep(float dt, float dt2)
    {
        var d = position - lastPosition;
        var v = d / dt;
        return d + dt2 * (acceleration - drag * v.magnitude * v);
    }
}