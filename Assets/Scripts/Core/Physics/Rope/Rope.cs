using System;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

public class Rope
{
    const float CONSTRAINTS_TOLERANCE = MathTools.o41;
    const float MAX_UPDATE_TIME = 2.5f;

    //Rope Data
    public readonly float width;
    public readonly float minNodeSpacing;
    public readonly float maxNodeSpacing;
    float nodeSpacing;

    //all nodes have same drag, and all nodes have same mass except possibly the last one
    public readonly float drag;
    public readonly float nodeMass;
    public readonly float terminusMass;

    public readonly float constraintIterations;
    public readonly int terminusAnchorMask;

    int anchorPointer;
    bool terminusAnchored;
    readonly int terminusIndex;

    //Node Data
    public readonly Vector2[] position;
    public readonly Vector2[] lastPosition;
    public readonly Vector2[] acceleration;

    readonly Vector2[] positionBuffer;
    readonly Vector2[] lastPositionBuffer;

    readonly int collisionMask;
    readonly float collisionSearchRadius;
    readonly float tunnelEscapeRadius;
    readonly float collisionThreshold;
    readonly float collisionBounciness;
    readonly Vector2[] lastCollisionNormal;
    public readonly Collider2D[] currentCollision;

    readonly Vector2[] raycastDirections;

    Stopwatch stopwatch = new();

    public int AnchorPointer => anchorPointer;
    public int TerminusIndex => terminusIndex;
    public float NodeSpacing => nodeSpacing;
    public float Length => nodeSpacing * (terminusIndex - anchorPointer);
    public bool TerminusAnchored => terminusAnchored;
    public UnityEvent TerminusBecameAnchored;

    public Rope(Vector2 position, float width, float length, int numNodes, float minNodeSpacing, float maxNodeSpacing,
    float nodeMass, float terminusMass, float nodeDrag, int collisionMask, float collisionSearchRadius, float tunnelEscapeRadius, float collisionBounciness, int terminusAnchorMask,
    int constraintIterations)
    {
        terminusIndex = numNodes - 1;
        this.width = width;
        nodeSpacing = Mathf.Clamp(length / terminusIndex, minNodeSpacing, maxNodeSpacing);
        anchorPointer = terminusIndex - Mathf.Clamp((int)(length / nodeSpacing), 1, terminusIndex);
        this.minNodeSpacing = minNodeSpacing;
        this.maxNodeSpacing = maxNodeSpacing;
        this.constraintIterations = constraintIterations;
        this.terminusAnchorMask = terminusAnchorMask;

        this.position = Enumerable.Repeat(position, numNodes).ToArray();
        lastPosition = Enumerable.Repeat(position, numNodes).ToArray();
        acceleration = Enumerable.Repeat(Physics2D.gravity, numNodes).ToArray();

        positionBuffer = new Vector2[numNodes];
        lastPositionBuffer = new Vector2[numNodes];

        drag = nodeDrag;
        this.nodeMass = nodeMass;
        this.terminusMass = terminusMass;

        this.collisionMask = collisionMask;
        this.collisionSearchRadius = collisionSearchRadius;
        this.tunnelEscapeRadius = tunnelEscapeRadius;
        collisionThreshold = 0.5f * width;
        this.collisionBounciness = collisionBounciness;
        currentCollision = new Collider2D[numNodes];
        lastCollisionNormal = new Vector2[numNodes];

        raycastDirections = new Vector2[]
        {
            new(0,-1),  new(0, 1), new(1, 0), new(-1, 0),
            new (MathTools.cos45, -MathTools.cos45), new(-MathTools.cos45, MathTools.cos45),
            new(-MathTools.cos45, -MathTools.cos45), new(MathTools.cos45, MathTools.cos45)
        };
    }


    //ROPE FUNCTIONS

    public void DrawGizmos()
    {
        Gizmos.color = Color.red;
        for (int i = 0; i < position.Length; i++)
        {
            Gizmos.DrawSphere(position[i], 0.5f * width);
        }
    }

    public void SetLength(float length)
    {
        nodeSpacing = length / (terminusIndex - anchorPointer);
        Rescale();
    }

    public void SetAnchorPosition(Vector2 position)
    {
        for (int i = 0; i < anchorPointer + 1; i++)
        {
            this.position[i] = position;
        }
    }

    public void Update(float dt, float dt2)
    {
        stopwatch.Restart();
        UpdateVerletSimulation(dt, dt2);
        ResolveCollisions();

        for (int i = 0; i < constraintIterations; i++)
        {
            SpacingConstraintIteration();
            if (stopwatch.Elapsed.TotalMilliseconds > MAX_UPDATE_TIME)
            {
                return;//no more dry meat!!!
            }
        }
    }

    public bool CollisionIsFailing(float threshold)
    {
        float distance = 0;
        bool chaining = false;

        for (int i = anchorPointer + 1; i < terminusIndex; i++)
        {
            if (chaining)
            {
                distance += Vector2.Distance(position[i - 1], position[i]);
                if (distance > threshold)
                {
                    return true;
                }
            }

            if (Physics2D.OverlapPoint(position[i], collisionMask) 
                && Physics2D.OverlapPoint(position[i] + collisionThreshold * Vector2.up, collisionMask)
                && Physics2D.OverlapPoint(position[i] - collisionThreshold * Vector2.up, collisionMask)
                && Physics2D.OverlapPoint(position[i] + collisionThreshold * Vector2.right, collisionMask)
                && Physics2D.OverlapPoint(position[i] - collisionThreshold * Vector2.right, collisionMask))
            {
                if (!chaining)
                {
                    chaining = true;
                    distance = Vector2.Distance(position[i - 1], position[i]);
                    if (distance > threshold)
                    {
                        return true;
                    }
                }
            }
            else
            {
                chaining = false;
            }
        }

        return false;
    }

    private void SpacingConstraintIteration()
    {
        FirstConstraint();
        for (int i = anchorPointer + 2; i < terminusIndex; i++)
        {
            ApplySpacingConstraint(i);
        }
        LastConstraint();

        void ApplySpacingConstraint(int i)
        {
            var d = position[i] - position[i - 1];
            var l = d.magnitude;

            var error = l - nodeSpacing;

            if (error > CONSTRAINTS_TOLERANCE)
            {
                if (currentCollision[i - 1] && currentCollision[i])
                {
                    var v0 = lastCollisionNormal[i - 1].CCWPerp();
                    var v1 = lastCollisionNormal[i].CCWPerp();
                    if (Vector2.Dot(v0, d) < 0)
                    {
                        v0 = -v0;
                    }
                    if (Vector2.Dot(v1, d) < 0)
                    {
                        v1 = -v1;
                    }

                    var t = 0.5f * error / l;
                    var tangentScale = 0.25f * l;//this seems to work well
                    v0 *= tangentScale;
                    v1 *= tangentScale;
                    position[i - 1] = MathTools.CubicInterpolation(position[i - 1], v0, position[i], v1, t);
                    position[i] = MathTools.CubicInterpolation(position[i - 1], v0, position[i], v1, 1 - t);
                }
                else
                {
                    var c = 0.5f * error / l * d;
                    position[i - 1] += c;
                    position[i] -= c;
                }

                //RELIABLE COLLISION IS THE #1 PRIORITY!
                //constraints pulling nodes through obstacles was the biggest problem with collision,
                //and it's much, MUCH better when you resolve collisions immediately after each constraint step
                ResolveCollision(i - 1);
                ResolveCollision(i);
            }
        }

        void FirstConstraint()
        {
            var d = position[anchorPointer + 1] - position[anchorPointer];
            var l = d.magnitude;

            var error = l - nodeSpacing;

            if (error > CONSTRAINTS_TOLERANCE)
            {
                position[anchorPointer + 1] -= error / l * d;
                ResolveCollision(anchorPointer + 1);
            }
        }

        void LastConstraint()
        {
            var d = position[terminusIndex] - position[terminusIndex - 1];
            var l = d.magnitude;

            var error = l - nodeSpacing;

            if (error > CONSTRAINTS_TOLERANCE)
            {
                if (terminusAnchored)
                {
                    position[terminusIndex - 1] += error / l * d;
                    ResolveCollision(terminusIndex - 1);
                }
                else
                {
                    var c = 1 / (nodeMass + terminusMass) * error / l * d;
                    position[terminusIndex - 1] += terminusMass * c;
                    position[terminusIndex] -= nodeMass * c;
                    ResolveCollision(terminusIndex - 1);
                    ResolveCollision(terminusIndex);
                }
            }
        }
    }

    private void UpdateVerletSimulation(float dt, float dt2)
    {
        for (int i = anchorPointer + 1; i < (terminusAnchored ? terminusIndex : position.Length); i++)
        {
            UpdateVerletSimulation(i, dt, dt2);
        }
    }

    private void ResolveCollisions()
    {
        for (int i = anchorPointer + 1; i < position.Length; i++)
        {
            ResolveCollision(i);
        }
    }

    private void Rescale()
    {
        if (nodeSpacing < minNodeSpacing || nodeSpacing > maxNodeSpacing)
        {
            float goalSpacing = nodeSpacing < minNodeSpacing ? minNodeSpacing : maxNodeSpacing;
            int newAnchorPointer = terminusIndex - Mathf.Clamp((int)(Length / goalSpacing), 1, terminusIndex);
            //clamps anchor pointer to btwn 0 and lastIndex - 1
            //note: length / nodeSpacing = num nodes PAST anchor pointer

            if (anchorPointer != newAnchorPointer)
            {
                Reparametrize(newAnchorPointer);
            }
        }
    }

    private void Reparametrize(int newAnchorPointer)
    {
        float newNodeSpacing = Length / (terminusIndex - newAnchorPointer);
        int i = anchorPointer;//start index of current segment we're copying from
        int j = newAnchorPointer + 1;//index in rescaleBuffer that we're copying to
        float dt = newNodeSpacing / nodeSpacing;//when we move one segment forward in new path, this is how many segments we cover in old path
        float t = dt;//time along current segment we're copying from (0 = nodes[i], 1 = nodes[i + 1])
        while (t > 1)
        {
            i++;
            t -= 1;
        }

        while (j < terminusIndex)
        {
            positionBuffer[j] = Vector2.Lerp(position[i], position[i + 1], t);
            lastPositionBuffer[j] = Vector2.Lerp(lastPosition[i], lastPosition[i + 1], t);

            t += dt;
            j++;
            while (t > 1)
            {
                i++;
                t -= 1;
            }
        }

        if (newAnchorPointer > anchorPointer)
        {
            for (int k = anchorPointer + 1; k < newAnchorPointer + 1; k++)
            {
                position[k] = position[anchorPointer];
                Anchor(k);
            }
        }
        else
        {
            for (int k = newAnchorPointer + 1; k < anchorPointer + 1; k++)
            {
                DeAnchor(k, 0, Vector2.zero);
            }
        }

        anchorPointer = newAnchorPointer;
        nodeSpacing = newNodeSpacing;
        int start = anchorPointer + 1;
        int count = terminusIndex - anchorPointer - 1;
        Array.Copy(positionBuffer, start, position, start, count);
        Array.Copy(lastPositionBuffer, start, lastPosition, start, count);
    }


    //NODE FUNCTIONS

    private void Anchor(int i)
    {
        lastPosition[i] = position[i];
        currentCollision[i] = null;
        lastCollisionNormal[i] = Vector2.zero;
    }

    private void DeAnchor(int i, float dt, Vector2 initialVelocity)
    {
        lastPosition[i] = position[i] - initialVelocity * dt;
    }

    private void UpdateVerletSimulation(int i, float dt, float dt2)
    {
        var p = position[i];
        var d = p - lastPosition[i];
        var v = d / dt;
        position[i] += d + dt2 * (acceleration[i] - drag * v.magnitude * v);
        lastPosition[i] = p;
    }

    private void ResolveCollision(int i)
    {
        if (i == terminusIndex)
        {
            if (!terminusAnchored)
            {
                var p = position[terminusIndex];
                ResolveCollisionInternal(terminusIndex);

                //check if terminus made contact and should become anchored
                if (currentCollision[terminusIndex] && (1 << currentCollision[terminusIndex].gameObject.layer & terminusAnchorMask) != 0)
                {
                    var v = p - position[terminusIndex];
                    var r = Physics2D.CircleCast(position[terminusIndex], collisionThreshold, v, v.magnitude, terminusAnchorMask);
                    if (r)
                    {
                        //anchor just outside collider, so that nodes near lastIndex don't get caught in perpetual collision
                        position[terminusIndex] = r.point + collisionThreshold * r.normal;
                        Anchor(terminusIndex);
                        terminusAnchored = true;
                        TerminusBecameAnchored.Invoke();
                    }
                }
            }
        }
        else
        {
            ResolveCollisionInternal(i);
        }
    }

    private void ResolveCollisionInternal(int i)
    {
        var r = Physics2D.CircleCast(position[i], collisionSearchRadius, Vector2.zero, 0f, collisionMask);
        if (!r)
        {
            currentCollision[i] = null;
            return;
        }

        bool tunneling = false;
        r = Physics2D.Raycast(position[i], raycastDirections[0], collisionSearchRadius, collisionMask);
        if (!r)
        {
            for (int j = 1; j < raycastDirections.Length; j++)
            {
                r = Physics2D.Raycast(position[i], raycastDirections[j], collisionSearchRadius, collisionMask);
                if (r)
                {
                    break;
                }
            }
        }

        if (r && r.distance == 0)
        {
            tunneling = true;
            for (int j = 0; j < raycastDirections.Length; j++)
            {
                r = Physics2D.Raycast(position[i] + tunnelEscapeRadius * raycastDirections[j], -raycastDirections[j], tunnelEscapeRadius, collisionMask);
                if (r && r.distance > 0)
                {
                    break;
                }
            }
            r.distance = tunnelEscapeRadius - r.distance;
        }

        if (r)
        {
            HandlePotentialCollision(i, r.collider, r.distance, r.normal, tunneling ? tunnelEscapeRadius : collisionThreshold, collisionBounciness);
        }
    }

    private void HandlePotentialCollision(int i, Collider2D collider, float distance, Vector2 normal, float collisionThreshold, float collisionBounciness)
    {
        if (distance != 0)
        {
            lastCollisionNormal[i] = normal;
        }
        else
        {
            normal = lastCollisionNormal[i];
        }

        if (distance < collisionThreshold)
        {
            currentCollision[i] = collider;
            var diff = Mathf.Min(collisionThreshold - distance, this.collisionThreshold);
            var velocity = position[i] - lastPosition[i];
            position[i] += diff * normal;
            var b = Vector2.Dot(velocity, normal);
            if (b < 0)
            {
                var tang = normal.CWPerp();
                var a = Vector2.Dot(velocity, tang);
                var newVelocity = -collisionBounciness * (velocity - 2 * a * tang);
                lastPosition[i] = position[i] - newVelocity;
            }
        }
        else
        {
            currentCollision[i] = null;
        }
    }
}