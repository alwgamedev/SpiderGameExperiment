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

    readonly int collisionMask;
    readonly float collisionSearchRadius;
    readonly float collisionThreshold;
    readonly float collisionBounciness;
    readonly Vector2[] raycastDirections;
    Vector2 lastCollisionNormal;

    public bool Anchored => anchored;
    public int CurrentCollisionLayerMask => CurrentCollision ? 1 << CurrentCollision.gameObject.layer : 0;
    public Collider2D CurrentCollision { get; private set; }
    public float CollisionThreshold => collisionThreshold;
    public Vector2 LastCollisionNormal => lastCollisionNormal;

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
        //var r = Vector2.right;
        //var u = Vector2.up;
        var rr = MathTools.cos45 * r;
        var uu = MathTools.cos45 * u;
        raycastDirections = new Vector2[] { r, rr + uu, u, -rr + uu, -r, -rr - uu, -u, rr - uu };//{ r, -r, u, -u, rr + uu, -rr - uu, -rr + uu, rr - uu };

        this.drag = drag;
        this.collisionMask = collisionMask;
        this.collisionThreshold = collisionThreshold;
        collisionSearchRadius = collisionThreshold + collisionSearchRadiusBuffer;
        this.collisionBounciness = collisionBounciness;
        CurrentCollision = null;
        lastCollisionNormal = Vector2.zero;
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
        if (!anchored)
        {
            UpdateLocalCoordinates();
            UpdateVerletSimulationCore(dt, dt2);
        }
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
            raycastDirections[1] = rr + uu;
            raycastDirections[2] = up;
            raycastDirections[3] = -rr + uu;
            raycastDirections[4] = -right;
            raycastDirections[5] = -rr - uu;
            raycastDirections[6] = -up;
            raycastDirections[7] = rr - uu;
        }
    }

    public void ResolveCollisions(float dt)
    {
        if (anchored) return;

        var l = collisionThreshold;
        var r = Physics2D.Raycast(position, raycastDirections[0], collisionSearchRadius, collisionMask);
        if (!r)
        {
            for (int i = 1; i < raycastDirections.Length; i++)
            {
                if (r)
                {
                    r = Physics2D.Raycast(position, raycastDirections[i], collisionSearchRadius, collisionMask);
                    break;
                }
            }
        }
        else if (r.distance == 0)
        {
            l = collisionSearchRadius;
            r = Physics2D.Raycast(position + l * raycastDirections[0], -raycastDirections[0], l, collisionMask);
            for (int i = 1; i < raycastDirections.Length; i++)
            {
                var s = Physics2D.Raycast(position + l * raycastDirections[i], -raycastDirections[i], l, collisionMask);
                if (s && s.distance > r.distance)
                {
                    r = s;
                }
            }
            r.distance = l - r.distance;
        }

        if (r)
        {
            HandlePotentialCollision(dt, ref r, l, collisionBounciness);
        }
        else
        {
            CurrentCollision = null;
            lastCollisionNormal = Vector2.zero;//can try to get rid of this
        }
    }

    private void HandlePotentialCollision(float dt, ref RaycastHit2D r, float collisionThreshold, float collisionBounciness)
    {
        var l = r.distance;
        var n = r.normal;

        if (l <= MathTools.o51)
        {
            if (lastCollisionNormal == Vector2.zero)
            {
                lastCollisionNormal = (r.point - (Vector2)r.collider.bounds.center).normalized;
            }
            n = lastCollisionNormal;
            var w = this.collisionThreshold * n;
            var velocity = (position - lastPosition) / dt;
            var tang = n.CWPerp();
            var a = Vector2.Dot(velocity, tang);
            var b = Vector2.Dot(velocity, n);
            var newVelocity = collisionBounciness * Mathf.Sign(b) * (velocity - 2 * a * tang);
            position += w;
            lastPosition = position - newVelocity * dt;
            StoreCollisionVelocity(r.collider.attachedRigidbody, n);
            CurrentCollision = r.collider;
            return;
        }
        else
        {
            lastCollisionNormal = n;
        }

        if (l < collisionThreshold)
        {
            ResolveCollision(dt, l, collisionThreshold, collisionBounciness, n, r.collider.attachedRigidbody);
            CurrentCollision = r.collider;
        }
        else
        {
            CurrentCollision = null;
            //but we don't set lastTrueCollisionNormal = 0, bc we still had a successful raycast, which could be useful for an upcoming collision!
        }
    }

    private void ResolveCollision(float dt, float distanceToContactPoint, float collisionThreshold, float collisionBounciness, Vector2 collisionNormal, Rigidbody2D attachedRb)
    {
        var velocity = (position - lastPosition) / dt;
        var speed = velocity.magnitude;
        var diff = Mathf.Min(collisionThreshold - distanceToContactPoint, this.collisionThreshold);
        var tang = collisionNormal.CWPerp();
        var a = Vector2.Dot(velocity, tang);
        var b = Vector2.Dot(velocity, collisionNormal);
        var newVelocity = collisionBounciness * Mathf.Sign(b) * (velocity - 2 * a * tang);

        if (speed > MathTools.o51)
        {
            var timeSinceCollision = diff / speed;
            position += diff * collisionNormal + newVelocity * timeSinceCollision;
            lastPosition = position - newVelocity * dt;

            StoreCollisionVelocity(attachedRb, collisionNormal);
        }
        else
        {
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