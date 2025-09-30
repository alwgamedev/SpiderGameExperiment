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
    float collisionThreshold;
    float collisionBounciness;
    readonly Vector2[] raycastDirections;
    int currentCollisionLayer;

    public bool Anchored => anchored;
    public int CurrentCollisionLayer => currentCollisionLayer;

    public RopeNode(Vector2 position, Vector2 velocity, Vector2 acceleration, float mass, float drag,
        int collisionMask, float collisionThreshold, float collisionBounciness,
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
        if (r != Vector2.zero)
        {
            right = r;
            up = r.CCWPerp();
        }
        else
        {
            right = Vector2.right;
            up = Vector2.up;
        }
        raycastDirections = new Vector2[]{ right, -right, up, -up };

        this.drag = drag;
        this.collisionMask = collisionMask;
        this.collisionThreshold = collisionThreshold;
        this.collisionBounciness = collisionBounciness;
        currentCollisionLayer = 0;



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
            raycastDirections[0] = right;
            raycastDirections[1] = -right;
            raycastDirections[2] = up;
            raycastDirections[3] = -up;
        }
    }

    public void ResolveCollisions(float dt)
    {
        if (anchored) return;
        var r = Physics2D.Raycast(position, raycastDirections[0], collisionThreshold, collisionMask);
        if (!r)
        {
            int i = 0;
            while (!r && i < raycastDirections.Length - 1)
            {
                i++;
                r = Physics2D.Raycast(position, raycastDirections[i], collisionThreshold, collisionMask);
            }
        }

        if (r)
        {
            HandlePotentialCollision(dt, r);
        }
        else
        {
            currentCollisionLayer = 0;
        }
    }

    private void HandlePotentialCollision(float dt, RaycastHit2D r)
    {
        var p = r.point;
        var l = r.distance;
        var n = r.normal;

        if (l <= 10E-05f)
        {
            //this is not great (normal may be in wrong direction or zero)
            //last major 2D0
            n = (lastPosition - position).normalized;
            var w = collisionThreshold * n;
            position += w;
            lastPosition -= collisionBounciness * w;
            StoreCollisionVelocity(r.collider.attachedRigidbody, n);
            currentCollisionLayer = 1 << r.collider.gameObject.layer;
            return;

        }

        if (l < collisionThreshold)
        {
            ResolveCollision(dt, l, n, r.collider.attachedRigidbody);
            currentCollisionLayer = 1 << r.collider.gameObject.layer;
        }
        else
        {
            currentCollisionLayer = 0;
        }
    }

    private void ResolveCollision(float dt, float distanceToContactPoint, Vector2 collisionNormal, Rigidbody2D attachedRb)
    {
        var velocity = (position - lastPosition) / dt;
        var speed = velocity.magnitude;

        if (speed > 10E-05f)
        {
            var timeSinceCollision = (collisionThreshold - distanceToContactPoint) / speed;
            var newVelocity = collisionBounciness * Mathf.Sign(Vector2.Dot(velocity, collisionNormal))
                * (2 * Vector2.Dot(velocity, collisionNormal) * collisionNormal - velocity);
            position += (collisionThreshold - distanceToContactPoint) * collisionNormal + newVelocity * timeSinceCollision;
            lastPosition = position - newVelocity * dt;

            StoreCollisionVelocity(attachedRb, collisionNormal);
        }
        else
        {
            position += (collisionThreshold - distanceToContactPoint) * collisionNormal;
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