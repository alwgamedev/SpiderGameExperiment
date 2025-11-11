using System;
using System.Linq;
using UnityEngine;

[Serializable]
public struct RopeNode
{
    const int NUM_STORED_VELOCITY_RBS = 4;

    public Vector2 position;
    public Vector2 lastPosition;
    public Vector2 acceleration;

    public float mass;
    float drag;

    Vector2 storedVelocity;
    readonly Rigidbody2D[] storedVelocityRbs;
    int storedVelocityPointer;

    //Vector2 right;
    //Vector2 up;

    bool anchored;

    readonly int collisionMask;
    readonly float collisionSearchRadius;
    readonly float tunnelEscapeRadius;
    readonly float collisionThreshold;
    readonly float collisionBounciness;
    Vector2 lastCollisionNormal;

    static readonly Vector2[] raycastDirections = new Vector2[]
    {
        new(0,-1),  new(0, 1), new(1, 0), new(-1, 0),
        new(-MathTools.cos45, MathTools.cos45),
        new(-MathTools.cos45, -MathTools.cos45), new(MathTools.cos45, -MathTools.cos45)
    };
    //{
    //    new(1, 0), new(MathTools.cos22pt5, MathTools.sin22pt5), new(MathTools.cos45, MathTools.cos45), new(MathTools.sin22pt5, MathTools.cos22pt5),
    //    new(0, 1), new(-MathTools.sin22pt5, MathTools.cos22pt5), new(-MathTools.cos45, MathTools.cos45), new(-MathTools.cos22pt5, MathTools.sin22pt5),
    //    new(-1, 0), new(-MathTools.cos22pt5, -MathTools.sin22pt5), new(-MathTools.cos45, -MathTools.cos45), new(-MathTools.sin22pt5, -MathTools.cos22pt5),
    //    new(0, -1), new(MathTools.sin22pt5, -MathTools.cos22pt5), new(MathTools.cos45, -MathTools.cos45), new(MathTools.cos22pt5, -MathTools.sin22pt5)
    //};

    public bool Anchored => anchored;
    public int CurrentCollisionLayerMask => CurrentCollision ? 1 << CurrentCollision.gameObject.layer : 0;
    public Collider2D CurrentCollision { get; private set; }
    public float CollisionThreshold => collisionThreshold;
    public Vector2 LastCollisionNormal => lastCollisionNormal;

    public RopeNode(Vector2 position, Vector2 velocity, Vector2 acceleration, float mass, float drag,
        int collisionMask, float collisionThreshold, float collisionSearchRadius, float tunnelEscapeRadius, float collisionBounciness,
        bool anchored)
    {
        this.anchored = anchored;
        this.position = position;
        lastPosition = anchored ? position : position - Time.fixedDeltaTime * velocity;
        this.acceleration = acceleration;
        this.mass = mass;

        storedVelocity = Vector2.zero;
        storedVelocityRbs = new Rigidbody2D[NUM_STORED_VELOCITY_RBS];
        storedVelocityPointer = 0;

        //circleCastBuffer = new RaycastHit2D[] { new(), new() };

        //var r = velocity.normalized;
        //if (r == Vector2.zero)
        //{
        //    r = Vector2.right;
        //}
        //var u = r.CCWPerp();
        //right = r;
        //up = u;
        //var ru = MathTools.cos45 * r;
        //var ur = MathTools.sin45 * u;
        //raycastDirections = new Vector2[] { r, -r, u, -u, ru + ur, -ru - ur, -ru + ur, ru - ur };

        this.drag = drag;
        this.collisionMask = collisionMask;
        this.collisionThreshold = collisionThreshold;
        this.collisionSearchRadius = collisionSearchRadius;
        this.tunnelEscapeRadius = tunnelEscapeRadius;
        this.collisionBounciness = collisionBounciness;
        CurrentCollision = null;
        lastCollisionNormal = Vector2.zero;
    }

    public void Anchor()
    {
        anchored = true;
        lastPosition = position;
        CurrentCollision = null;
        lastCollisionNormal = Vector2.zero;
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
            //UpdateLocalCoordinates();
            UpdateVerletSimulationCore(dt, dt2);
            //UpdateLocalCoordinates(); //doesn't it make more sense to do after?
        }
    }



    //public void UpdateLocalCoordinates()
    //{
    //    if (!anchored)
    //    {
    //        var r = (position - lastPosition).normalized;
    //        if (r != Vector2.zero)
    //        {
    //            right = r;
    //            up = r.CCWPerp();
    //            var ru = MathTools.cos45 * right;
    //            var ur = MathTools.cos45 * up;
    //            raycastDirections[0] = right;
    //            raycastDirections[1] = ru + ur;
    //            raycastDirections[2] = up;
    //            raycastDirections[3] = -ru + ur;
    //            raycastDirections[4] = -right;
    //            raycastDirections[5] = -ru - ur;
    //            raycastDirections[6] = -up;
    //            raycastDirections[7] = ru - ur;
    //        }
    //    }
    //}

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

    public void ResolveCollisions(float dt)
    {
        if (anchored) return;

        var circleCast = Physics2D.CircleCast(position, collisionSearchRadius, Vector2.zero, 0f, collisionMask);
        if (!circleCast)
        {
            CurrentCollision = null;
            lastCollisionNormal = Vector2.zero;
            return;
        }

        var l = collisionThreshold;
        var r = Physics2D.Raycast(position, raycastDirections[0], collisionSearchRadius, collisionMask);
        if (!r)
        {
            for (int i = 0; i < raycastDirections.Length; i++)
            {
                r = Physics2D.Raycast(position, raycastDirections[i], collisionSearchRadius, collisionMask);
                if (r)
                {
                    break;
                }
            }
        }

        if (r.distance == 0)
        {
            l = tunnelEscapeRadius;
            r = Physics2D.Raycast(position + l * raycastDirections[0], -raycastDirections[0], l, collisionMask);
            for (int i = 1; i < raycastDirections.Length; i++)
            {
                r = Physics2D.Raycast(position + l * raycastDirections[i], -raycastDirections[i], l, collisionMask);
                if (r && r.distance > 0)
                {
                    break;
                }
            }
            r.distance = l - r.distance;
        }

        if (r)
        {
            HandlePotentialCollision(dt, ref r, l, collisionBounciness);
        }
        //else
        //{
        //    //CurrentCollision = null;
        //    lastCollisionNormal = Vector2.zero;
        //}
    }

    private void HandlePotentialCollision(float dt, ref RaycastHit2D r, float collisionThreshold, float collisionBounciness)
    {
        var l = r.distance;
        var n = r.normal;

        if (!(l > MathTools.o31))
        {
            if (lastCollisionNormal == Vector2.zero)
            {
                //Debug.Log("using the shitty case");
                lastCollisionNormal = (r.point - (Vector2)r.collider.bounds.center).normalized;
            }
            n = lastCollisionNormal;
            //var w = this.collisionThreshold * n;
            var velocity = (position - lastPosition) / dt;
            var tang = n.CWPerp();
            var a = Vector2.Dot(velocity, tang);
            var b = Vector2.Dot(velocity, n);
            var newVelocity = collisionBounciness * Mathf.Sign(b) * (velocity - 2 * a * tang);
            position += this.collisionThreshold * n;
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
        //var speed = velocity.magnitude;
        var diff = Mathf.Min(collisionThreshold - distanceToContactPoint, this.collisionThreshold);
        var tang = collisionNormal.CWPerp();
        var a = Vector2.Dot(velocity, tang);
        var b = Vector2.Dot(velocity, collisionNormal);
        var newVelocity = collisionBounciness * Mathf.Sign(b) * (velocity - 2 * a * tang);

        position += diff * collisionNormal;
        lastPosition = position - newVelocity * dt;

        //if (speed > MathTools.o31)
        //{
        //    var timeSinceCollision = diff / speed;
        //    position += diff * collisionNormal + newVelocity * timeSinceCollision;
        //    lastPosition = position - newVelocity * dt;

        //    StoreCollisionVelocity(attachedRb, collisionNormal);
        //}
        //else
        //{
        //    position += diff * collisionNormal;
        //    lastPosition = position - newVelocity * dt;
        //}
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