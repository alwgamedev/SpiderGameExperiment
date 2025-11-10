using Mono.Cecil.Cil;
using System.Linq;
using UnityEngine;

public class Rope
{
    public const int MAX_NUM_COLLISIONS = 4;
    public const float CONSTRAINTS_TOLERANCE = 0.005f;

    public float width;
    public float nodeSpacing;
    public float constraintIterations;
    public int terminusAnchorMask;
    
    public readonly RopeNode[] nodes;
    public readonly int lastIndex;

    public readonly Vector3[] renderPositions;
    bool renderPositionsNeedUpdate;

    public float Length
    {
        get => nodeSpacing * lastIndex;
        set
        {
            nodeSpacing = value / lastIndex;
        }
    }

    public Rope(Vector2 position, float width, float nodeSpacing, int numNodes,
        float nodeDrag, int collisionMask, float collisionSearchRadiusBuffer, float collisionBounciness, int terminusAnchorMask,
        int constraintIterations)
    {
        this.width = width;
        //length = (numNodes - 1) * nodeSpacing;
        this.nodeSpacing = nodeSpacing;
        this.constraintIterations = constraintIterations;
        this.terminusAnchorMask = terminusAnchorMask;
        var a = Physics2D.gravity;
        var collisionThreshold = 0.5f * width;
        nodes = Enumerable.Range(0, numNodes).Select(i => new RopeNode(position, Vector2.zero, a, 1, nodeDrag, 
            collisionMask, collisionThreshold, collisionSearchRadiusBuffer, collisionBounciness, false)).ToArray();
        renderPositions = nodes.Select(x=> (Vector3)x.position).ToArray();
        lastIndex = numNodes - 1;
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

    public void UpdateRopePhysics(float dt, float dt2)
    {
        UpdateVerletSimulation(dt, dt2);
        ResolveCollisions(dt);

        for (int i = 0; i < constraintIterations; i++)
        {
            SpacingConstraintIteration();
            ResolveCollisions(dt);
        }
    }

    public void SpacingConstraintIteration()
    {
        for (int i = 1; i < nodes.Length; i++)
        {

            var d = nodes[i].position - nodes[i - 1].position;
            var l = d.magnitude;

            //if (!(l > MathTools.o51))
            //{
            //    return;
            //}

            var error = l - nodeSpacing;

            if (error > CONSTRAINTS_TOLERANCE /*|| error < -CONSTRAINTS_TOLERANCE*/)
            {
                if (nodes[i - 1].CurrentCollision && nodes[i].CurrentCollision)
                {
                    //NOTE: neither node is anchored when this happens (because we clear CurrentCollision when set anchored)
                    if (Vector2.Dot(nodes[i - 1].LastCollisionNormal, nodes[i].LastCollisionNormal) < MathTools.cos15)
                    {
                        var v0 = nodes[i - 1].LastCollisionNormal.CCWPerp();
                        var v1 = nodes[i].LastCollisionNormal.CCWPerp();
                        if (MathTools.TryIntersectLine(nodes[i - 1].position, v0, nodes[i].position, v1, out var p))
                        {
                            //p = 0.25f * p + 0.75f * (nodes[i - 1].position + nodes[i].position);//round the corner a little
                            var d0 = p - nodes[i - 1].position;
                            var d1 = p - nodes[i].position;
                            var l0 = Vector2.Dot(d0, v0);
                            var l1 = Vector2.Dot(d1, v1);
                            nodes[i - 1].position += error * Mathf.Sign(l0) * v0;//error * d0.normalized;
                            nodes[i].position += error * Mathf.Sign(l1) * v1;//error * d1.normalized;
                            //2do: should move less along tangent directions based on dot products
                            //even better: move nodes along circular arc connecting them (with radius determined by change in angle btwn collision normals)
                            //and compute error along this circular arc too
                            return;
                        }
                    }
                }

                var c = (error / l) * d;
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
        for (int i = 0; i < nodes.Length; i++)
        {
            renderPositions[i] = nodes[i].position;
        }
    }

    private void UpdateVerletSimulation(float dt, float dt2)
    {
        for (int i = 0; i < nodes.Length; i++)
        {
            nodes[i].UpdateVerletSimulation(dt, dt2);
        }
    }

    private void ResolveCollisions(float dt)
    {
        if (nodes[lastIndex].Anchored)
        {
            for (int i = 0; i < lastIndex; i++)
            {
                nodes[i].ResolveCollisions(dt);
            }
        }
        else
        {
            var p = nodes[lastIndex].position;
            for (int i = 0; i < nodes.Length; i++)
            {
                nodes[i].ResolveCollisions(dt);
                
            }
            //check if last node made contact and should become anchored
            //(but we need to record its position before any collision resolution, so that it doesn't bounce and anchor to a weird position)
            if ((nodes[lastIndex].CurrentCollisionLayerMask & terminusAnchorMask) != 0)
            {
                if (!Physics2D.OverlapCircle(nodes[lastIndex].position, nodes[lastIndex].CollisionThreshold, terminusAnchorMask))
                {
                    nodes[lastIndex].position = p;
                }
                nodes[lastIndex].Anchor();
            }
        }
    }
}