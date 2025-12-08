using System.Linq;
using UnityEngine;
using UnityEngine.Events;

public class Rope
{
    //public const int MAX_NUM_COLLISIONS = 4;
    public const float CONSTRAINTS_TOLERANCE = 0.00001f;//0.005f;

    public float width;
    public float nodeSpacing;
    public float minNodeSpacing;
    public float maxNodeSpacing;

    public float constraintIterations;
    public int constraintIterationsPerCollisionCheck;
    public int terminusAnchorMask;

    public readonly RopeNode[] nodes;
    public readonly int lastIndex;

    int anchorPointer;

    public readonly Vector3[] renderPositions;
    public readonly int nodesPerRendererPosition;
    public readonly float inverseNodesPerRendererPosition;
    bool renderPositionsNeedUpdate;

    public int AnchorPointer => anchorPointer;
    public float Length => nodeSpacing * (lastIndex - anchorPointer);

    public UnityEvent TerminusAnchored;

    public Rope(Vector2 position, float width, float length, int numNodes, float minNodeSpacing, float maxNodeSpacing,
        float nodeDrag, int collisionMask, float collisionSearchRadius, float tunnelingEscapeRadius, float collisionBounciness, int terminusAnchorMask,
        int constraintIterations, int constraintIterationsPerCollisionCheck, int nodesPerRendererPosition = 1)
    {
        lastIndex = numNodes - 1;
        this.width = width;
        nodeSpacing = Mathf.Clamp(length / lastIndex, minNodeSpacing, maxNodeSpacing);//0.5f * (minNodeSpacing + maxNodeSpacing);
        anchorPointer = lastIndex - Mathf.Clamp((int)(length / nodeSpacing), 1, lastIndex);
        this.minNodeSpacing = minNodeSpacing;
        this.maxNodeSpacing = maxNodeSpacing;
        this.constraintIterations = constraintIterations;
        this.constraintIterationsPerCollisionCheck = constraintIterationsPerCollisionCheck;
        this.terminusAnchorMask = terminusAnchorMask;
        var a = Physics2D.gravity;
        var collisionThreshold = 0.5f * width;
        nodes = Enumerable.Range(0, numNodes).Select(i => new RopeNode(position, Vector2.zero, a, 1, nodeDrag,
            collisionMask, collisionThreshold, collisionSearchRadius, tunnelingEscapeRadius, collisionBounciness, !(i > anchorPointer))).ToArray();
        this.nodesPerRendererPosition = nodesPerRendererPosition;
        inverseNodesPerRendererPosition = 1 / (float)nodesPerRendererPosition;
        int numRenderPositions = nodesPerRendererPosition == 1 ? nodes.Length : 1 + (nodes.Length / nodesPerRendererPosition);
        renderPositions = new Vector3[numRenderPositions];
        for (int i = 0; i < numRenderPositions; i++)
        {
            renderPositions[i] = nodes[nodesPerRendererPosition * i].position;
        }
        renderPositions[^1] = nodes[lastIndex].position;
            //nodes.Select(x => (Vector3)x.position).ToArray();
    }

    public void DrawGizmos()
    {
        Gizmos.color = Color.red;
        for (int i = 0; i < nodes.Length; i++)
        {
            Gizmos.DrawSphere(nodes[i].position, 0.5f * width);
        }
    }

    public void FixedUpate(float dt, float dt2)
    {
        UpdateRopePhysics(dt, dt2);

        renderPositionsNeedUpdate = true;
    }

    public void SetLength(float length, RopeNode[] rescaleBuffer)
    {
        nodeSpacing = length / (lastIndex - anchorPointer);
        Rescale(rescaleBuffer);
    }

    public void SetAnchorPosition(Vector2 position)
    {
        for (int i = 0; i < anchorPointer + 1; i++)
        {
            nodes[i].position = position;
        }
    }

    public void UpdateRopePhysics(float dt, float dt2)
    {
        UpdateVerletSimulation(dt, dt2);
        ResolveCollisions(dt);

        int i = 0;
        while (i < constraintIterations)
        {
            SpacingConstraintIteration();
            if (++i % constraintIterationsPerCollisionCheck == 0 || i == lastIndex)//allows us to do a lot of constraintIterations without nuking performance
            {
                ResolveCollisions(dt);
            }
        }
    }

    public void SpacingConstraintIteration()
    {
        for (int i = anchorPointer + 1; i < nodes.Length; i++)
        {
            var d = nodes[i].position - nodes[i - 1].position;
            var l = d.magnitude;

            var error = l - nodeSpacing;

            if (error > CONSTRAINTS_TOLERANCE)
            {

                if (nodes[i - 1].CurrentCollision && nodes[i].CurrentCollision
                    && Vector2.Dot(nodes[i - 1].lastCollisionNormal, nodes[i].lastCollisionNormal) < MathTools.cos15)
                {
                    var v0 = nodes[i - 1].lastCollisionNormal.CCWPerp();
                    var v1 = nodes[i].lastCollisionNormal.CCWPerp();
                    if (MathTools.TryIntersectLine(nodes[i - 1].position, v0, nodes[i].position, v1, out var p))
                    {
                        var l0 = Vector2.Dot(p - nodes[i - 1].position, v0);
                        var l1 = Vector2.Dot(p - nodes[i].position, v1);
                        //var l = Mathf.Abs(l0) + Mathf.Abs(l1);
                        //var error = l - nodeSpacing;
                        error *= 0.5f;// * (Mathf.Abs(l0) + Mathf.Abs(l1)) / l;
                        //nodes[i - 1].position += Mathf.Sign(l0) * error * v0;//move in the direction of p - node.pos
                        //nodes[i].position += Mathf.Sign(l1) * error * v1;
                        if (error > Mathf.Abs(l0))
                        {
                            nodes[i - 1].position = p - Mathf.Sign(l1) * (error - Mathf.Abs(l0)) * v1;
                            nodes[i - 1].lastCollisionNormal = MathTools.CheapRotationalLerp(nodes[i - 1].lastCollisionNormal, nodes[i].lastCollisionNormal,
                                error / (Mathf.Abs(l0) + Mathf.Abs(l1)), out _);//nodes[i].lastCollisionNormal;
                        }
                        else
                        {
                            nodes[i - 1].position += Mathf.Sign(l0) * error * v0;//move along v0 in the direction of p - node.pos
                        }
                        if (error > Mathf.Abs(l1))
                        {
                            nodes[i].position = p - Mathf.Sign(l0) * (error - Mathf.Abs(l1)) * v0;
                            //nodes[i].lastCollisionNormal = nodes[i - 1].lastCollisionNormal;
                            nodes[i - 1].lastCollisionNormal = MathTools.CheapRotationalLerp(nodes[i].lastCollisionNormal, nodes[i - 1].lastCollisionNormal,
                                error / (Mathf.Abs(l0) + Mathf.Abs(l1)), out _);
                        }
                        else
                        {
                            nodes[i].position += Mathf.Sign(l1) * error * v1;
                        }

                        //behaves a lot more stably when you also do a standard constraint pull afterwards
                        //(WITHOUT recomputing error!)
                    }
                }

                var c = (error / l) * d;

                //if (nodes[i - 1].CurrentCollision && nodes[i].CurrentCollision)
                //{
                //    //NOTE: neither node is anchored when this happens(because we clear CurrentCollision when set anchored)
                //    if (Vector2.Dot(nodes[i - 1].LastCollisionNormal, nodes[i].LastCollisionNormal) < MathTools.cos15)
                //    {
                //        var v0 = nodes[i - 1].LastCollisionNormal.CCWPerp();
                //        var v1 = nodes[i].LastCollisionNormal.CCWPerp();
                //        c = 0.5f * c;
                //        nodes[i - 1].position += Vector2.Dot(c, v0) * v0;
                //        nodes[i].position -= Vector2.Dot(c, v1) * v1;
                //        return;
                //    }
                //}

                if (nodes[i - 1].Anchored)
                {
                    nodes[i].position -= c;
                }
                else if (nodes[i].Anchored)
                {
                    nodes[i - 1].position += c;
                }
                else
                {
                    if (i == lastIndex)//because only the last index gets weighted for us
                    {
                        var m1 = 1 / (nodes[i - 1].mass + nodes[i].mass);
                        nodes[i - 1].position += nodes[i].mass * m1 * c;
                        nodes[i].position -= nodes[i - 1].mass * m1 * c;
                    }
                    else
                    {
                        c *= 0.5f;
                        nodes[i - 1].position += c;
                        nodes[i].position -= c;
                    }
                }
            }
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

    private void UpdateRenderPositions()
    {
        renderPositions[0] = nodes[0].position;
        Vector2 p = Vector2.zero;
        for (int i = 1; i < lastIndex; i++)
        {
            p += nodes[i].position;
            if (i % nodesPerRendererPosition == 0)
            {
                renderPositions[i / nodesPerRendererPosition] = inverseNodesPerRendererPosition * p;
                p = Vector2.zero;
            }
        }
        renderPositions[^1] = nodes[lastIndex].position;
        //for (int i = 0; i < nodes.Length; i++)
        //{
        //    renderPositions[i] = nodes[i].position;
        //}
    }

    private void UpdateVerletSimulation(float dt, float dt2)
    {
        for (int i = anchorPointer + 1; i < nodes.Length; i++)
        {
            nodes[i].UpdateVerletSimulation(dt, dt2);
        }
    }

    private void ResolveCollisions(float dt)
    {
        if (nodes[lastIndex].Anchored)
        {
            for (int i = anchorPointer + 1; i < lastIndex; i++)
            {
                nodes[i].ResolveCollisions(dt);
            }
        }
        else
        {
            var p = nodes[lastIndex].position;
            for (int i = anchorPointer + 1; i < nodes.Length; i++)
            {
                nodes[i].ResolveCollisions(dt);

            }

            //check if last node made contact and should become anchored
            if ((nodes[lastIndex].CurrentCollisionLayerMask & terminusAnchorMask) != 0)
            {
                var r = Physics2D.CircleCast(nodes[lastIndex].position, nodes[lastIndex].CollisionThreshold, Vector2.zero, 0, terminusAnchorMask);
                if (!r)
                {
                    r = Physics2D.CircleCast(p, nodes[lastIndex].CollisionThreshold, Vector2.zero, 0, terminusAnchorMask);
                }
                if (r)
                {
                    //anchor just outside collider, so that nodes near lastIndex don't get caught in perpetual collision
                    nodes[lastIndex].position = r.point + nodes[lastIndex].CollisionThreshold * r.normal;
                    nodes[lastIndex].Anchor();
                    TerminusAnchored.Invoke();
                }
            }
        }
    }

    private void Rescale(RopeNode[] rescaleBuffer)
    {
        if (nodeSpacing < minNodeSpacing || nodeSpacing > maxNodeSpacing)
        {
            float goalSpacing = nodeSpacing < minNodeSpacing ? minNodeSpacing : maxNodeSpacing;
            int newAnchorPointer = lastIndex - Mathf.Clamp((int)(Length / goalSpacing), 1, lastIndex);//clamps anchor pointer to btwn 0 and lastIndex - 1
            //^note: length / nodeSpacing = num nodes PAST anchor pointer

            if (anchorPointer != newAnchorPointer)
            {
                Reparametrize(newAnchorPointer, rescaleBuffer);
                renderPositionsNeedUpdate = true;
            }
        }
    }

    private void Reparametrize(int newAnchorPointer, RopeNode[] rescaleBuffer)
    {
        float newNodeSpacing = Length / (lastIndex - newAnchorPointer);
        int i = anchorPointer;//start index of current segment we're copying from
        int j = newAnchorPointer + 1;//index in rescaleBuffer that we're copying to
        float dt = newNodeSpacing / nodeSpacing;//when we move one segment forward in new path, this is how many segments we cover in old path
        float t = dt;//time along current segment we're copying from (0 = nodes[i], 1 = nodes[i + 1])
        while (t > 1)
        {
            i++;
            t -= 1;
        }

        while (j < lastIndex)
        {
            rescaleBuffer[j].position = Vector2.Lerp(nodes[i].position, nodes[i + 1].position, t);
            rescaleBuffer[j].lastPosition = Vector2.Lerp(nodes[i].lastPosition, nodes[i + 1].lastPosition, t);

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
                nodes[k].position = nodes[anchorPointer].position;
                nodes[k].Anchor();
            }
        }
        else
        {
            for (int k = newAnchorPointer + 1; k < anchorPointer + 1; k++)
            {
                nodes[k].DeAnchor(0, Vector2.zero);
            }
        }

        anchorPointer = newAnchorPointer;

        for (int k = anchorPointer + 1; k < lastIndex; k++)
        {
            nodes[k].position = rescaleBuffer[k].position;
            nodes[k].lastPosition = rescaleBuffer[k].lastPosition;
        }

        nodeSpacing = newNodeSpacing;
    }
}