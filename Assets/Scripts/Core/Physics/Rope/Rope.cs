using System;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

public class Rope
{
    const float CONSTRAINTS_TOLERANCE = MathTools.o41;
    const float MAX_UPDATE_TIME = 2f;

    //Rope Data
    public float width;//really the width of the rope (not half of it)
    public float minNodeSpacing;
    public float maxNodeSpacing;
    float nodeSpacing;

    //all nodes have same drag, and all nodes have same mass except possibly the last one
    public float drag;
    public float nodeMass;
    public float terminusMass;
    public float dynamicAnchorPullForce;

    public float constraintIterations;

    int startIndex;//effective beginning of rope (we set some nodes at beginning of rope to sleep to maintain reasonable node spacing)
    TerminusAnchorMode terminusAnchorMode;
    Collider2D terminusAnchor;
    Vector2 terminusAnchorLocalPos;

    enum TerminusAnchorMode
    {
        notAnchored, staticAnchor, dynamicAnchor
    }

    //Node Data
    public Vector2[] position;
    public Vector2[] lastPosition;

    Vector2[] positionBuffer;
    Vector2[] lastPositionBuffer;

    public int collisionMask;
    public float collisionSearchRadius;
    public float tunnelEscapeRadius;
    public float collisionThreshold;
    public float collisionBounciness;
    public Vector2[] lastCollisionNormal;
    public bool[] nearCollision;
    public bool[] hadCollision;

    readonly Vector2[] raycastDirections;

    Stopwatch stopwatch = new();

    public bool Enabled { get; private set; }
    public int StartIndex => startIndex;
    public int TerminusIndex => position.Length - 1;
    public float NodeSpacing => nodeSpacing;
    public float Length => nodeSpacing * (TerminusIndex - startIndex);
    public bool TerminusAnchored => terminusAnchorMode != TerminusAnchorMode.notAnchored;//terminusAnchored;

    public UnityEvent TerminusBecameAnchored;

    public Rope(Vector2 position, float width, float length, int numNodes, float minNodeSpacing, float maxNodeSpacing,
    float nodeMass, float terminusMass, float nodeDrag, int collisionMask, float collisionSearchRadius, float tunnelEscapeRadius, float collisionBounciness,
    int constraintIterations, float dynamicAnchorPullForce)
    {
        int terminusIndex = numNodes - 1;
        terminusAnchorMode = TerminusAnchorMode.notAnchored;
        this.dynamicAnchorPullForce = dynamicAnchorPullForce;

        this.width = width;
        nodeSpacing = Mathf.Clamp(length / terminusIndex, minNodeSpacing, maxNodeSpacing);
        startIndex = terminusIndex - Mathf.Clamp((int)(length / nodeSpacing), 1, terminusIndex);
        this.minNodeSpacing = minNodeSpacing;
        this.maxNodeSpacing = maxNodeSpacing;
        this.constraintIterations = constraintIterations;

        this.position = Enumerable.Repeat(position, numNodes).ToArray();
        lastPosition = Enumerable.Repeat(position, numNodes).ToArray();

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
        nearCollision = new bool[numNodes];
        hadCollision = new bool[numNodes];
        lastCollisionNormal = new Vector2[numNodes];

        raycastDirections = new Vector2[]
        {
            new(0,-1),  new(0, 1), new(1, 0), new(-1, 0),
            new (MathTools.cos45, -MathTools.cos45), new(-MathTools.cos45, MathTools.cos45),
            new(-MathTools.cos45, -MathTools.cos45), new(MathTools.cos45, MathTools.cos45)
        };

        Enabled = true;
    }

    public void Respawn(Vector2 position, float length, int numNodes)
    {
        int terminusIndex = numNodes - 1;
        nodeSpacing = Mathf.Clamp(length / terminusIndex, minNodeSpacing, maxNodeSpacing);
        startIndex = terminusIndex - Mathf.Clamp((int)(length / nodeSpacing), 1, terminusIndex);
        terminusAnchorMode = TerminusAnchorMode.notAnchored;
        terminusAnchor = null;

        if (this.position.Length != numNodes)
        {
            this.position = Enumerable.Repeat(position, numNodes).ToArray();
            lastPosition = Enumerable.Repeat(position, numNodes).ToArray();
            positionBuffer = new Vector2[numNodes];
            lastPositionBuffer = new Vector2[numNodes];
            nearCollision = new bool[numNodes];
            hadCollision = new bool[numNodes];
            lastCollisionNormal = new Vector2[numNodes];
        }
        else
        {
            Array.Fill(this.position, position);
            Array.Fill(lastPosition, position);
            Array.Clear(positionBuffer, 0, numNodes);
            Array.Clear(lastPositionBuffer, 0, numNodes);
            Array.Clear(nearCollision, 0, numNodes);
            Array.Clear(hadCollision, 0, numNodes);
            Array.Clear(lastCollisionNormal, 0, numNodes);
        }

        Enabled = true;
    }

    public void Disable()
    {
        Enabled = false;
        terminusAnchorMode = TerminusAnchorMode.notAnchored;
        terminusAnchor = null;
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
        nodeSpacing = length / (TerminusIndex - startIndex);
        Rescale();
    }

    public void SetAnchorPosition(Vector2 position)
    {
        for (int i = 0; i < startIndex + 1; i++)
        {
            this.position[i] = position;
        }
    }

    public void Update(float dt, float dt2)
    {
        stopwatch.Restart();

        if (terminusAnchorMode != TerminusAnchorMode.notAnchored)
        {
            //check every frame in case someday we have an anchor that could get destroyed or change rigidbody type... for now sort of a waste
            if (terminusAnchor)
            {
                terminusAnchorMode = terminusAnchor.attachedRigidbody && terminusAnchor.attachedRigidbody.bodyType == RigidbodyType2D.Dynamic ?
                    TerminusAnchorMode.dynamicAnchor : TerminusAnchorMode.staticAnchor;
            }
            else
            {
                terminusAnchorMode = TerminusAnchorMode.notAnchored;
                terminusAnchor = null;
            }
        }

        switch (terminusAnchorMode)
        {
            case TerminusAnchorMode.notAnchored:
                UpdateInternal();
                break;
            case TerminusAnchorMode.staticAnchor:
                UpdateInternal();
                Vector2 p = terminusAnchor.transform.TransformPoint(terminusAnchorLocalPos);
                position[TerminusIndex] = p;
                lastPosition[TerminusIndex] = p;
                break;
            case TerminusAnchorMode.dynamicAnchor:
                UpdateWithDynamicAnchor();
                break;
        }

        void UpdateInternal()
        {
            UpdateVerletSimulation(dt2);
            ResolveCollisions();
            for (int i = 0; i < constraintIterations; i++)
            {
                SpacingConstraintIteration();
                if (stopwatch.Elapsed.TotalMilliseconds > MAX_UPDATE_TIME)
                {
                    return;
                }
            }
        }

        void UpdateWithDynamicAnchor()
        {
            UpdateVerletSimulation(dt2);

            Vector2 p = terminusAnchor.transform.TransformPoint(terminusAnchorLocalPos);
            position[TerminusIndex] = p;
            lastPosition[TerminusIndex] = p - dt * terminusAnchor.attachedRigidbody.linearVelocity;

            ResolveCollisions();
            for (int i = 0; i < constraintIterations; i++)
            {
                SpacingConstraintIteration();
                if (stopwatch.Elapsed.TotalMilliseconds > MAX_UPDATE_TIME)
                {
                    return;
                }
            }

            PullDynamicAnchor();
        }
    }

    public bool CollisionIsFailing(float threshold)
    {
        float distance = 0;
        bool chaining = false;
        
        for (int i = startIndex + 1; i < TerminusIndex; i++)
        {
            if (chaining)
            {
                distance += Vector2.Distance(position[i - 1], position[i]);
                if (distance > threshold)
                {
                    return true;
                }
            }

            //check if node is fully tunneled inside one collider
            var c = Physics2D.OverlapPoint(position[i], collisionMask);
            if (c && Physics2D.OverlapPoint(position[i] + collisionThreshold * Vector2.up, collisionMask) == c
                && Physics2D.OverlapPoint(position[i] - collisionThreshold * Vector2.up, collisionMask) == c
                && Physics2D.OverlapPoint(position[i] + collisionThreshold * Vector2.right, collisionMask) == c
                && Physics2D.OverlapPoint(position[i] - collisionThreshold * Vector2.right, collisionMask) == c)
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
        for (int i = startIndex + 2; i < TerminusIndex; i++)
        {
            ApplySpacingConstraint(i);
            //gives it a nice bouncy but stable feel (and can get away with ~1/3 of the iterations (for twice the work per iteration))
            if (i > startIndex + 2)
            {
                ApplySpacingConstraint(i - 1);
            }
        }
        LastConstraint();

        void ApplySpacingConstraint(int i)
        {
            var d = position[i] - position[i - 1];
            var l = d.magnitude;

            var error = l - nodeSpacing;

            if (error > CONSTRAINTS_TOLERANCE)
            {
                if (hadCollision[i - 1] && hadCollision[i])
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
                //and it's much, MUCH better when you resolve collisions immediately after each constraint
                ResolveCollision(i - 2);
                ResolveCollision(i - 1);
                ResolveCollision(i);
            }
        }

        void FirstConstraint()
        {
            var d = position[startIndex + 1] - position[startIndex];
            var l = d.magnitude;

            var error = l - nodeSpacing;

            if (error > CONSTRAINTS_TOLERANCE)
            {
                position[startIndex + 1] -= error / l * d;
                ResolveCollision(startIndex + 1);
            }
        }

        void LastConstraint()
        {
            var d = position[TerminusIndex] - position[TerminusIndex - 1];
            var l = d.magnitude;

            var error = l - nodeSpacing;

            if (error > CONSTRAINTS_TOLERANCE)
            {
                switch (terminusAnchorMode)
                {
                    case TerminusAnchorMode.staticAnchor:
                        position[TerminusIndex - 1] += error / l * d;
                        ResolveCollision(TerminusIndex - 1);
                        break;
                    case TerminusAnchorMode.dynamicAnchor:
                        {
                            var tMass = terminusAnchor.attachedRigidbody.mass /*+ terminusMass*/;
                            var c = 1 / (nodeMass + tMass) * error / l * d;
                            position[TerminusIndex - 1] += tMass * c;
                            ResolveCollision(TerminusIndex - 1);
                            break;
                        }
                    case TerminusAnchorMode.notAnchored:
                        {
                            var c = 1 / (nodeMass + terminusMass) * error / l * d;
                            position[TerminusIndex - 1] += terminusMass * c;
                            position[TerminusIndex] -= nodeMass * c;
                            ResolveCollision(TerminusIndex - 1);
                            ResolveCollision(TerminusIndex);
                            break;
                        }
                }
            }
        }
    }

    private void PullDynamicAnchor()
    {
        var d = position[TerminusIndex] - position[TerminusIndex - 1];
        var l = d.magnitude;

        var error = l - nodeSpacing;

        if (error > CONSTRAINTS_TOLERANCE)
        {
            terminusAnchor.attachedRigidbody.AddForceAtPosition(-dynamicAnchorPullForce / l * error * d, position[TerminusIndex]);
        }
    }

    private void UpdateVerletSimulation(float dt2)
    {
        for (int i = startIndex + 1; i < (terminusAnchorMode == TerminusAnchorMode.notAnchored ? position.Length : TerminusIndex); i++)
        {
            UpdateVerletSimulation(i, dt2);
        }
    }

    private void ResolveCollisions()
    {
        for (int i = startIndex + 1; i < position.Length; i++)
        {
            ResolveCollision(i);
        }
    }

    private void Rescale()
    {
        if (nodeSpacing < minNodeSpacing || nodeSpacing > maxNodeSpacing)
        {
            float goalSpacing = nodeSpacing < minNodeSpacing ? minNodeSpacing : maxNodeSpacing;
            int newStartIndex = TerminusIndex - Mathf.Clamp((int)(Length / goalSpacing), 1, TerminusIndex);
            //clamps anchor pointer to btwn 0 and lastIndex - 1
            //note: length / nodeSpacing = num nodes PAST anchor pointer

            if (startIndex != newStartIndex)
            {
                Reparametrize(newStartIndex);
            }
        }
    }

    private void Reparametrize(int newStartIndex)
    {
        float newNodeSpacing = Length / (TerminusIndex - newStartIndex);
        int i = startIndex;//start index of current segment we're copying from
        int j = newStartIndex + 1;//index in rescaleBuffer that we're copying to
        float dt = newNodeSpacing / nodeSpacing;//when we move one segment forward in new path, this is how many segments we cover in old path
        float t = dt;//time along current segment we're copying from (0 = nodes[i], 1 = nodes[i + 1])
        while (t > 1)
        {
            i++;
            t -= 1;
        }

        while (j < TerminusIndex)
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

        if (newStartIndex > startIndex)
        {
            for (int k = startIndex + 1; k < newStartIndex + 1; k++)
            {
                position[k] = position[startIndex];
                Anchor(k);
            }
        }
        else
        {
            for (int k = newStartIndex + 1; k < startIndex + 1; k++)
            {
                DeAnchor(k, 0, Vector2.zero);
            }
        }

        startIndex = newStartIndex;
        nodeSpacing = newNodeSpacing;
        int start = startIndex + 1;
        int count = TerminusIndex - startIndex - 1;
        Array.Copy(positionBuffer, start, position, start, count);
        Array.Copy(lastPositionBuffer, start, lastPosition, start, count);
    }


    //NODE FUNCTIONS

    private void Anchor(int i)
    {
        lastPosition[i] = position[i];
        nearCollision[i] = false;
        hadCollision[i] = false;
        lastCollisionNormal[i] = Vector2.zero;
    }

    private void DeAnchor(int i, float dt, Vector2 initialVelocity)
    {
        lastPosition[i] = position[i] - initialVelocity * dt;
    }

    private void UpdateVerletSimulation(int i, float dt2)
    {
        var p = position[i];
        var d = p - lastPosition[i];
        position[i] += (1 - drag * d.magnitude) * d + dt2 * Physics2D.gravity;//d + dt2 * (Physics2D.gravity - drag * v.magnitude * v);
        lastPosition[i] = p;
    }

    //private void ApplyForceToDynamicAnchor(float dt, float dt2)
    //{
    //    var p = position[TerminusIndex];
    //    var d = p - lastPosition[TerminusIndex];
    //    d = (1 - drag * d.magnitude) * d + dt2 * Physics2D.gravity;
    //    position[TerminusIndex] += d;
    //    lastPosition[TerminusIndex] = p;
    //    terminusAnchor.attachedRigidbody.AddForce((terminusAnchor.attachedRigidbody.mass /*+ terminusMass*/) * (1 / dt2 * (d - dt * terminusAnchor.attachedRigidbody.linearVelocity)));
    //}

    private void ResolveCollision(int i)
    {
        if (i == TerminusIndex)
        {
            if (!TerminusAnchored)
            {
                var p = position[TerminusIndex];
                ResolveCollisionInternal(TerminusIndex);

                //check if terminus made contact and should become anchored
                //unnecessary layer check -- the "terminusAnchorMask" and nearestCollider layers should both just be the collisionMask
                if (nearCollision[TerminusIndex]/*nearestCollider[terminusIndex] && (1 << nearestCollider[terminusIndex].gameObject.layer & terminusAnchorMask) != 0*/)
                {
                    var v = p - position[TerminusIndex];
                    var r = Physics2D.CircleCast(position[TerminusIndex], collisionThreshold, v, v.magnitude, collisionMask/*terminusAnchorMask*/);
                    if (r)
                    {
                        //anchor just outside collider, so that nodes near lastIndex don't get caught in perpetual collision
                        p = r.point + collisionThreshold * r.normal;
                        position[TerminusIndex] = p;
                        terminusAnchor = r.collider;
                        terminusAnchorLocalPos = terminusAnchor.transform.InverseTransformPoint(p);
                        if (terminusAnchor.attachedRigidbody && terminusAnchor.attachedRigidbody.bodyType == RigidbodyType2D.Dynamic)
                        {
                            terminusAnchorMode = TerminusAnchorMode.dynamicAnchor;
                        }
                        else
                        {
                            terminusAnchorMode = TerminusAnchorMode.staticAnchor;
                            Anchor(TerminusIndex);
                        }
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
        RaycastHit2D r;

        if (!nearCollision[i])
        {
            r = Physics2D.CircleCast(position[i], collisionSearchRadius, Vector2.zero, 0f, collisionMask);
            if (r)
            {
                nearCollision[i] = true;
            }
            else
            {
                hadCollision[i] = false;
                return;
            }
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
        else
        {
            nearCollision[i] = false;
            hadCollision[i] = false;
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
            hadCollision[i] = false;
        }
    }
}