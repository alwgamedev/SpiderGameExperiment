using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public struct RopeNode
{
    //const int NUM_STORED_VELOCITY_RBS = 4;

    public Vector2 position;
    public Vector2 lastPosition;
    public Vector2 acceleration;

    public Vector2 lastCollisionNormal;

    public float mass;
    float drag;

    //Vector2 storedVelocity;
    //readonly Rigidbody2D[] storedVelocityRbs;
    //int storedVelocityPointer;

    bool anchored;
    readonly int collisionMask;
    readonly float collisionSearchRadius;
    readonly float tunnelEscapeRadius;
    readonly float collisionThreshold;
    readonly float collisionBounciness;
    RaycastHit2D collisionRay;

    readonly static RaycastHit2D defaultRay = default;

    readonly Vector2[] raycastDirections;
    readonly Vector2[] tunnelEscapeDirections;
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
    //public Vector2 LastCollisionNormal => lastCollisionNormal;

    public RopeNode(Vector2 position, Vector2 velocity, Vector2 acceleration, float mass, float drag,
        int collisionMask, float collisionThreshold, float collisionSearchRadius, float tunnelEscapeRadius, float collisionBounciness,
        bool anchored)
    {
        this.anchored = anchored;
        this.position = position;
        lastPosition = anchored ? position : position - Time.fixedDeltaTime * velocity;
        this.acceleration = acceleration;
        this.mass = mass;

        //storedVelocity = Vector2.zero;
        //storedVelocityRbs = new Rigidbody2D[NUM_STORED_VELOCITY_RBS];
        //storedVelocityPointer = 0;

        this.drag = drag;
        this.collisionMask = collisionMask;
        this.collisionThreshold = collisionThreshold;
        this.collisionSearchRadius = collisionSearchRadius;
        this.tunnelEscapeRadius = tunnelEscapeRadius;
        this.collisionBounciness = collisionBounciness;
        CurrentCollision = null;
        collisionRay = defaultRay;
        lastCollisionNormal = Vector2.zero;

        raycastDirections = new Vector2[]
        {
            new(0,-1),  new(0, 1), new(1, 0), new(-1, 0),
            new(-MathTools.cos45, MathTools.cos45),
            new(-MathTools.cos45, -MathTools.cos45), new(MathTools.cos45, -MathTools.cos45)
        };

        tunnelEscapeDirections = raycastDirections.Select(v => tunnelEscapeRadius * v).ToArray();
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
            UpdateVerletSimulationCore(dt, dt2);
        }
    }

    private void UpdateVerletSimulationCore(float dt, float dt2)
    {
        var p = position;
        position += NextPositionStep(dt, dt2);
        //if (storedVelocity != Vector2.zero)
        //{
        //    position += dt * storedVelocity;
        //    storedVelocity = Vector2.zero;
        //}
        //if (storedVelocityPointer != 0)
        //{
        //    for (int i = 0; i < storedVelocityRbs.Length; i++)
        //    {
        //        storedVelocityRbs[i] = null;
        //    }
        //    storedVelocityPointer = 0;
        //}
        lastPosition = p;
    }

    //public void ResolveCollisionsNew(Vector2 nextNodePos, float dt)
    //{
    //    var u = (nextNodePos - position).normalized.CCWPerp(); ;

    //    var l = collisionThreshold;

    //    collisionRay = Physics2D.Raycast(position, u, collisionSearchRadius, coll)
    //}

    public void ResolveCollisions(float dt)
    {
        if (anchored) return;

        var circleCast = Physics2D.CircleCast(position, collisionSearchRadius, Vector2.zero, 0f, collisionMask);
        if (!circleCast)
        {
            CurrentCollision = null;
            //lastCollisionNormal = Vector2.zero;
            return;
        }

        bool tunneling = false;

        for (int i = 0; i < raycastDirections.Length; i++)
        {
            collisionRay = Physics2D.Raycast(position, raycastDirections[i], collisionSearchRadius, collisionMask);
            if (collisionRay)
            {
                break;
            }
        }


        if (collisionRay && collisionRay.distance == 0)
        {
            tunneling = true;
            for (int i = 0; i < raycastDirections.Length; i++)
            {
                collisionRay = Physics2D.Raycast(position + tunnelEscapeDirections[i], -tunnelEscapeDirections[i], tunnelEscapeRadius, collisionMask);
                if (collisionRay && collisionRay.distance > 0)
                {
                    lastCollisionNormal = collisionRay.normal;
                    break;
                }
            }
            collisionRay.distance = tunnelEscapeRadius - collisionRay.distance;
        }

        if (collisionRay)
        {
            HandlePotentialCollision(dt, ref collisionRay, tunneling ? tunnelEscapeRadius : collisionThreshold, collisionBounciness);
        }
    }

    private void HandlePotentialCollision(float dt, ref RaycastHit2D r, float collisionThreshold, float collisionBounciness)
    {
        if (r.distance != 0)
        {
            lastCollisionNormal = r.normal;
        }
        else
        {
            r.normal = lastCollisionNormal;
        }


        if (r.distance < collisionThreshold)
        {
            ResolveCollision(dt, r.distance, collisionThreshold, collisionBounciness, r.normal);
            CurrentCollision = r.collider;
        }
        else
        {
            CurrentCollision = null;
            //but we don't set lastCollisionNormal = 0, bc we still had a successful raycast, which could be useful for an upcoming collision!
        }
    }

    private void ResolveCollision(float dt, float distanceToContactPoint, float collisionThreshold, float collisionBounciness,
        Vector2 collisionNormal)
    {
        var diff = Mathf.Min(collisionThreshold - distanceToContactPoint, this.collisionThreshold);
        var velocity = (position - lastPosition) / dt;
        var tang = collisionNormal.CWPerp();
        var a = Vector2.Dot(velocity, tang);
        var b = Vector2.Dot(velocity, collisionNormal);
        var newVelocity = collisionBounciness * Mathf.Sign(b) * (velocity - 2 * a * tang);
        position += diff * collisionNormal;
        lastPosition = position - newVelocity * dt;
    }

    //private void StoreCollisionVelocity(Rigidbody2D attachedRb, Vector2 collisionNormal)
    //{
    //    if (attachedRb)
    //    {
    //        if (storedVelocityPointer < storedVelocityRbs.Length && Vector2.Dot(attachedRb.linearVelocity, collisionNormal) > 0
    //            && !storedVelocityRbs.Contains(attachedRb))
    //        {
    //            storedVelocity += attachedRb.linearVelocity
    //                + collisionBounciness * Vector2.Dot(attachedRb.linearVelocity, collisionNormal) * collisionNormal;
    //            storedVelocityRbs[storedVelocityPointer] = attachedRb;
    //            storedVelocityPointer++;
    //        }
    //    }
    //}

    //to be added to position
    public Vector2 NextPositionStep(float dt, float dt2)
    {
        var d = position - lastPosition;
        var v = d / dt;
        return d + dt2 * (acceleration - drag * v.magnitude * v);
    }
}