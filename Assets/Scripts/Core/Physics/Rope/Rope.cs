using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.U2D;

public class Rope
{
    const float CONSTRAINTS_TOLERANCE = MathTools.o41;
    const float SHARP_CORNER_THRESHOLD = MathTools.sin30;

    //Rope Data
    readonly float width;
    readonly float minNodeSpacing;
    readonly float maxNodeSpacing;
    float nodeSpacing;

    //all nodes have same drag, and all nodes have same mass except possibly the last one
    readonly float drag;
    readonly float nodeMass;
    readonly float terminusMass;

    readonly float constraintIterations;
    readonly int terminusAnchorMask;

    int anchorPointer;
    bool terminusAnchored;
    readonly int terminusIndex;

    readonly Vector3[] renderPositions;
    bool renderPositionsNeedUpdate;

    //Node Data
    public readonly Vector2[] position;//if we were really hardcore we could do separate arrays for x & y
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
            new (MathTools.cos45, MathTools.cos45), new(-MathTools.cos45, MathTools.cos45),
            new(-MathTools.cos45, -MathTools.cos45), new(MathTools.cos45, -MathTools.cos45)
        };

        renderPositions = Enumerable.Repeat((Vector3)position, numNodes).ToArray();
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

    public void SetLineRendererPositions(LineRenderer lr)
    {
        if (renderPositionsNeedUpdate)
        {
            UpdateRenderPositions();
            renderPositionsNeedUpdate = false;
        }

        lr.SetPositions(renderPositions);
    }

    public void FixedUpate(float dt, float dt2)
    {
        UpdateRopePhysics(dt, dt2);
        renderPositionsNeedUpdate = true;
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

    private void UpdateRopePhysics(float dt, float dt2)
    {
        UpdateVerletSimulation(dt, dt2);
        ResolveCollisions(dt);

        for (int i = 0; i < constraintIterations; i++)
        {
            SpacingConstraintIteration();
            ResolveCollisions(dt);
        }
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
                if (currentCollision[i - 1] && currentCollision[i]
                    && Vector2.Dot(lastCollisionNormal[i - 1], lastCollisionNormal[i]) < SHARP_CORNER_THRESHOLD)
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
                    v0 = 0.25f * l * v0 + position[i - 1];//this seems to give a reasonable scale for the tangent
                    v1 = position[i] - 0.25f * l * v1;
                    var p0 = BezierUtility.BezierPoint(v0, position[i - 1], position[i], v1, t);
                    var p1 = BezierUtility.BezierPoint(v0, position[i - 1], position[i], v1, 1 - t);
                    position[i - 1] = p0;
                    position[i] = p1;
                }
                else
                {
                    var c = 0.5f * error / l * d;
                    position[i - 1] += c;
                    position[i] -= c;
                }
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
                }
                else
                {
                    var c = (1 / (nodeMass + terminusMass)) * error / l * d;
                    position[terminusIndex - 1] += terminusMass * c;
                    position[terminusIndex] -= nodeMass * c;
                }
            }
        }
    }

    private void UpdateRenderPositions()
    {
        for (int i = 0; i < position.Length; i++)
        {
            renderPositions[i] = position[i];
        }
    }

    private void UpdateVerletSimulation(float dt, float dt2)
    {
        for (int i = anchorPointer + 1; i < (terminusAnchored ? terminusIndex : position.Length); i++)
        {
            UpdateVerletSimulation(i, dt, dt2);
        }
    }

    private void ResolveCollisions(float dt)
    {
        if (terminusAnchored)
        {
            for (int i = anchorPointer + 1; i < terminusIndex; i++)
            {
                ResolveCollision(i, dt);
            }
        }
        else
        {
            var p = position[terminusIndex];
            for (int i = anchorPointer + 1; i < position.Length; i++)
            {
                ResolveCollision(i, dt);

            }

            //check if last node made contact and should become anchored
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

    private void Rescale()
    {
        if (nodeSpacing < minNodeSpacing || nodeSpacing > maxNodeSpacing)
        {
            float goalSpacing = nodeSpacing < minNodeSpacing ? minNodeSpacing : maxNodeSpacing;
            int newAnchorPointer = terminusIndex - Mathf.Clamp((int)(Length / goalSpacing), 1, terminusIndex);//clamps anchor pointer to btwn 0 and lastIndex - 1
                                                                                                      //^note: length / nodeSpacing = num nodes PAST anchor pointer

            if (anchorPointer != newAnchorPointer)
            {
                Reparametrize(newAnchorPointer);
                renderPositionsNeedUpdate = true;
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

    //2do: lot of retardation in this method. why not use the circle cast hit (and then refine hit if needed)? does it give point inside collider?
    //let's test the circle cast (e.g. make a script that circle casts at mouse click, then log/draw hit data)
    //also we were thinking about alternative raycasting schemes (like "hairs" normal to rope segment -- just 2 directions; or maybe an --X-- (x pattern) diagonal to rope seg)
    private void ResolveCollision(int i, float dt)
    {
        var circleCast = Physics2D.CircleCast(position[i], collisionSearchRadius, Vector2.zero, 0f, collisionMask);
        if (!circleCast)
        {
            currentCollision[i] = null;
            return;
        }

        bool tunneling = false;
        RaycastHit2D r = default;

        for (int j = 0; j < raycastDirections.Length; j++)
        {
            r = Physics2D.Raycast(position[i], raycastDirections[j], collisionSearchRadius, collisionMask);
            if (r)
            {
                break;
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
            HandlePotentialCollision(i, dt, ref r, tunneling ? tunnelEscapeRadius : collisionThreshold, collisionBounciness);
        }
    }

    private void HandlePotentialCollision(int i, float dt, ref RaycastHit2D r, float collisionThreshold, float collisionBounciness)
    {
        if (r.distance != 0)
        {
            lastCollisionNormal[i] = r.normal;
        }
        else
        {
            r.normal = lastCollisionNormal[i];
        }


        if (r.distance < collisionThreshold)
        {
            currentCollision[i] = r.collider;
            var diff = Mathf.Min(collisionThreshold - r.distance, this.collisionThreshold);
            var velocity = (position[i] - lastPosition[i]) / dt;
            var tang = r.normal.CWPerp();
            var a = Vector2.Dot(velocity, tang);
            var b = Vector2.Dot(velocity, r.normal);
            var newVelocity = collisionBounciness * Mathf.Sign(b) * (velocity - 2 * a * tang);
            position[i] += diff * r.normal;
            lastPosition[i] = position[i] - newVelocity * dt;
        }
        else
        {
            currentCollision[i] = null;
        }
    }
}