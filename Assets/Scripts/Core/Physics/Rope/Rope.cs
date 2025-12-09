using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Splines;
using UnityEngine.U2D;

public class Rope
{
    //public const int MAX_NUM_COLLISIONS = 4;
    public const float CONSTRAINTS_TOLERANCE = MathTools.o41;//0.005f;

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
                    && Vector2.Dot(nodes[i - 1].lastCollisionNormal, nodes[i].lastCollisionNormal) < MathTools.sin30)
                {
                    var v0 = nodes[i - 1].lastCollisionNormal.CCWPerp();
                    var v1 = nodes[i].lastCollisionNormal.CCWPerp();
                    if (Vector2.Dot(v0, d) < 0)
                    {
                        v0 = -v0;
                    }
                    if (Vector2.Dot(v1, d) < 0)
                    {
                        v1 = -v1;
                    }

                    var t = 0.5f * error / l;
                    v0 *= 0.6f * l;
                    v1 *= 0.6f * l;
                    v0 = (v0 + 3 * nodes[i - 1].position) / 3;//retarded
                    v1 = (3 * nodes[i].position - v1) / 3;
                    var p0 = BezierUtility.BezierPoint(v0, nodes[i - 1].position, nodes[i].position, v1, t);
                    var p1 = BezierUtility.BezierPoint(v0, nodes[i - 1].position, nodes[i].position, v1, 1 - t);
                    nodes[i - 1].position = p0;
                    nodes[i].position = p1;
                    return;

                    //d = nodes[i].position - nodes[i - 1].position;
                    //l = d.magnitude;
                    //error = l - nodeSpacing;
                    //if (!(error > CONSTRAINTS_TOLERANCE))
                    //{
                    //    return;
                    //}

                    //var v0 = nodes[i - 1].lastCollisionNormal.CCWPerp();
                    //var v1 = nodes[i].lastCollisionNormal.CCWPerp();
                    //if (Vector2.Dot(v0, d) < 0)
                    //{
                    //    v0 = -v0;
                    //}
                    //if (Vector2.Dot(v1, d) < 0)
                    //{
                    //    v1 = -v1;
                    //}

                    //var m1 = 1 / (nodes[i - 1].mass + nodes[i].mass);
                    //nodes[i - 1].position += nodes[i].mass * m1 * error * v0;
                    //nodes[i].position -= nodes[i - 1].mass * m1 * error * v1;

                    ////it's a lot more stable if you combine with a regular constraint pull after
                    ////so we recompute error instead of returning
                    //d = nodes[i].position - nodes[i - 1].position;
                    //l = d.magnitude;
                    //error = l - nodeSpacing;
                    //if (!(error > CONSTRAINTS_TOLERANCE))
                    //{
                    //    return;
                    //}

                    //if (MathTools.TryIntersectLine(nodes[i - 1].position, nodes[i - 1].lastCollisionNormal, nodes[i].position, nodes[i].lastCollisionNormal, out var o))
                    //{

                    //    var v0 = nodes[i - 1].position - o;
                    //    var v1 = nodes[i].position - o;
                    //    var r = Mathf.Abs(Vector2.Dot(v1, nodes[i].lastCollisionNormal));
                    //    v0 /= r;
                    //    v1 /= r;
                    //    var t = 0.5f * error / l;
                    //    nodes[i - 1].position = o + r * MathTools.CheapRotationalLerp(v0, v1, t, out _);
                    //    nodes[i].position = o + r * MathTools.CheapRotationalLerp(v1, v0, t, out _);

                    //    //it seems to be a lot more stable if you also do a normal constraints pull afterwards
                    //    d = nodes[i].position - nodes[i - 1].position;
                    //    l = d.magnitude;

                    //    error = l - nodeSpacing;
                    //    if (!(error > CONSTRAINTS_TOLERANCE))
                    //    {
                    //        return;
                    //    }
                    //}

                    //if (MathTools.TryIntersectLine(nodes[i - 1].position, v0, nodes[i].position, v1, out var p))
                    //{
                    //    var l0 = Vector2.Dot(p - nodes[i - 1].position, v0);
                    //    var l1 = Vector2.Dot(p - nodes[i].position, v1);
                    //    //var l = Mathf.Abs(l0) + Mathf.Abs(l1);
                    //    //var error = l - nodeSpacing;
                    //    error *= 0.5f;// * (Mathf.Abs(l0) + Mathf.Abs(l1)) / l;
                    //    //nodes[i - 1].position += Mathf.Sign(l0) * error * v0;//move in the direction of p - node.pos
                    //    //nodes[i].position += Mathf.Sign(l1) * error * v1;
                    //    if (error > Mathf.Abs(l0))
                    //    {
                    //        nodes[i - 1].position = p - Mathf.Sign(l1) * (error - Mathf.Abs(l0)) * v1;
                    //        nodes[i - 1].lastCollisionNormal = nodes[i].lastCollisionNormal;
                    //        //nodes[i - 1].lastCollisionNormal = MathTools.CheapRotationalLerp(nodes[i - 1].lastCollisionNormal, nodes[i].lastCollisionNormal,
                    //        //    error / (Mathf.Abs(l0) + Mathf.Abs(l1)), out _);
                    //    }
                    //    else
                    //    {
                    //        nodes[i - 1].position += Mathf.Sign(l0) * error * v0;//move along v0 in the direction of p - node.pos
                    //    }
                    //    if (error > Mathf.Abs(l1))
                    //    {
                    //        nodes[i].position = p - Mathf.Sign(l0) * (error - Mathf.Abs(l1)) * v0;
                    //        nodes[i].lastCollisionNormal = nodes[i - 1].lastCollisionNormal;
                    //        //nodes[i - 1].lastCollisionNormal = MathTools.CheapRotationalLerp(nodes[i].lastCollisionNormal, nodes[i - 1].lastCollisionNormal,
                    //        //    error / (Mathf.Abs(l0) + Mathf.Abs(l1)), out _);
                    //    }
                    //    else
                    //    {
                    //        nodes[i].position += Mathf.Sign(l1) * error * v1;
                    //    }
                    //return;

                    //behaves more stably when you also do a standard constraint pull afterwards
                    //(and without recomputing error -- bit of a fudge bc im not sure why but we'll revisit this at some point)
                    //d = nodes[i].position - nodes[i - 1].position;
                    //l = d.magnitude;

                    //error = l - nodeSpacing;
                    //if (!(error > CONSTRAINTS_TOLERANCE))
                    //{
                    //    return;
                    //}
                }

                var c = error / l * d;

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
                    var m1 = 1 / (nodes[i - 1].mass + nodes[i].mass);
                    nodes[i - 1].position += nodes[i].mass * m1 * c;
                    nodes[i].position -= nodes[i - 1].mass * m1 * c;
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