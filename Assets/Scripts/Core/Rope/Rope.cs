using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class Rope
{
    public const int MAX_NUM_COLLISIONS = 1;
    public const float CONSTRAINTS_TOLERANCE = 0.001f;

    public float width;
    public float nodeSpacing;
    public float constraintIterations;
    
    public readonly RopeNode[] nodes;

    public readonly Vector3[] renderPositions;
    bool renderPositionsNeedUpdate;

    public float Length => nodeSpacing * nodes.Length;

    public Rope(Vector2 position, float width, float nodeSpacing, int numNodes, 
        float nodeDrag, int collisionMask, float collisionBounciness, 
        int constraintIterations)
    {
        this.width = width;
        this.nodeSpacing = nodeSpacing;
        this.constraintIterations = constraintIterations;
        var a = Physics2D.gravity;
        var collisionThreshold = 0.5f * width;
        nodes = Enumerable.Range(0, numNodes).Select(i => new RopeNode(position, Vector2.zero, a, 1, nodeDrag, 
            collisionMask, collisionThreshold, collisionBounciness, false)).ToArray();
        renderPositions = nodes.Select(x => (Vector3)x.position).ToArray();
    }

    //anchored rope with end given an initial velocity
    public Rope(Vector2 position, Vector2 velocity, float width, float nodeSpacing, int numNodes,
        float nodeDrag, int collisionMask, float collisionBounciness,
        int constraintIterations)
    {
        
        this.width = width;
        this.nodeSpacing = nodeSpacing;
        this.constraintIterations = constraintIterations;
        var a = Physics2D.gravity;
        var collisionThreshold = 0.5f * width;
        int end = numNodes - 1;
        nodes = Enumerable.Range(0, numNodes).Select(i => new RopeNode(position, i == end ? velocity : Vector2.zero, a, 1, nodeDrag,
            collisionMask, collisionThreshold, collisionBounciness, false)).ToArray();
        renderPositions = nodes.Select(x => (Vector3)x.position).ToArray();
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

    public void SetLineRendererPositions(LineRenderer lr)
    {
        if (renderPositionsNeedUpdate)
        {
            UpdateRenderPositions();
            renderPositionsNeedUpdate = false;
        }

        lr.SetPositions(renderPositions);
    }

    //public void GrowRopeFromBase(float lengthChange)
    //{
    //    var lengthPerSegment = lengthChange / (nodes.Length - 1);
    //    nodeSpacing += lengthPerSegment;
    //    for (int i = nodes.Length - 1; i > 0; i--)
    //    {
    //        var d = 0.25f * lengthPerSegment * (nodes[i].position - nodes[i - 1].position).normalized;
    //        for (int j = i; j < nodes.Length; j++)
    //        {
    //            nodes[j].lastPosition -= d;
    //        }
    //    }
    //}

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

    public void SpacingConstraintIteration()
    {
        for (int i = 1; i < nodes.Length; i++)
        {
            if (nodes[i - 1].Anchored && nodes[i].Anchored) continue;

            var d = nodes[i].position - nodes[i - 1].position;
            var l = d.magnitude;

            if (l <= 10E-05f)
                continue;

            var error = l - nodeSpacing;

            if (error > CONSTRAINTS_TOLERANCE || error < -CONSTRAINTS_TOLERANCE)
            {
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
                    //c = 0.5f * c;
                    var m1 = 1 / (nodes[i - 1].mass + nodes[i].mass);
                    nodes[i - 1].position += nodes[i].mass * m1 * c;
                    nodes[i].position -= nodes[i - 1].mass * m1 * c;
                }
            }
        }
    }

    private void ResolveCollisions(float dt)
    {
        for (int i = 0; i < nodes.Length; i++)
        {
            nodes[i].ResolveCollisions(dt/*, collisionContactFilter, collisionBuffer*/);
        }
    }

    //different feel to the rope, but can get by with ONE constraint iteration!
    //(you could also probably blend it a bit with the other method (i.e. move nodes[i-1] a little) to make the feel more like original)
    //however for spider silk, i like the stretchy feel we get with the original constraints method
    //although that method takes many more constraints iteration (with this semihard boner method, you don't even need to iterate)
    private void SemiHardConstraints()
    {
        for (int i = 1; i < nodes.Length; i++)
        {
            if (nodes[i - 1].Anchored && nodes[i].Anchored) continue;

            var d = nodes[i].position - nodes[i - 1].position;
            var l = d.magnitude;
            if (l > 10E-05f)
            {
                var u = d / l;
                var error = l - nodeSpacing;
                var c = error * u;
                if (!nodes[i].Anchored)
                {
                    nodes[i].position -= c;
                    nodes[i].lastPosition -= 0.99f * c;
                    //nodes[i].ResolveCollisions(collisionContactFilter, collisionBuffer);
                }
                //if (nodes[i - 1].Anchored)
                //{
                //    nodes[i].position -= c;
                //    nodes[i].lastPosition -= 0.95f * c;
                //    nodes[i].ResolveCollisions(collisionContactFilter, collisionBuffer);
                //}
                //else if (nodes[i].Anchored)
                //{
                //    nodes[i - 1].position += c;
                //    nodes[i - 1].lastPosition += 0.95f * c;
                //    nodes[i - 1].ResolveCollisions(collisionContactFilter, collisionBuffer);
                //}
                //else
                //{
                //    nodes[i].position -= c;
                //    nodes[i].lastPosition -= 0.95f * c;
                //    nodes[i].ResolveCollisions(collisionContactFilter, collisionBuffer);
                //}
            }
        }
    }
}