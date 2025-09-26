using System.Linq;
using UnityEngine;

public class Rope
{
    public const int MAX_NUM_COLLISIONS = 4;
    //public const int PHYSICS_SUBSTEPS = 1;

    public float width;
    public float nodeSpacing;
    public float spacingConstraintIterations;
    //public float spacingConstraintWeightRight;
    //public float spacingConstraintWeightLeft;
    //public float spacingConstraintSmoothing;
    
    public readonly RopeNode[] nodes;

    public readonly Vector3[] renderPositions;
    bool renderPositionsNeedUpdate;

    Collider2D[] collisionBuffer;
    ContactFilter2D collisionContactFilter;

    public Rope(Vector3 position, float width, float nodeSpacing, int numNodes, 
        float nodeDrag, /*CircleCollider2D prefab,*/ float collisionRadius, float collisionBounciness, 
        int spacingConstraintIterations/*, float spacingConstraintWeighting, float spacingConstraintSmoothing*/)
    {
        this.width = width;
        this.nodeSpacing = nodeSpacing;
        this.spacingConstraintIterations = spacingConstraintIterations;
        //spacingConstraintWeightRight = spacingConstraintWeighting;
        //spacingConstraintWeightLeft = 1 - spacingConstraintWeightRight;
        //this.spacingConstraintSmoothing = spacingConstraintSmoothing;
        var a = Physics2D.gravity;
        var collisionThreshold = 0.5f * width;
        nodes = Enumerable.Range(0, numNodes).Select(i => new RopeNode(position, Vector2.zero, a, nodeDrag, 
            /*prefab,*/ collisionRadius, collisionThreshold, collisionBounciness, i == 0)).ToArray();
        renderPositions = nodes.Select(x => (Vector3)x.position).ToArray();
        collisionBuffer = new Collider2D[MAX_NUM_COLLISIONS];
        collisionContactFilter = new();
        collisionContactFilter.NoFilter();
    }

    public void FixedUpate(float dt)
    {
        //dt /= PHYSICS_SUBSTEPS;
        var dt2 = dt * dt;
        UpdateRopePhysics(dt, dt2);
        //for (int i = 0; i < PHYSICS_SUBSTEPS; i++)
        //{
        //    UpdateRopePhysics(dt, dt2);
        //}

        renderPositionsNeedUpdate = true;
    }

    public void UpdateRopePhysics(float dt, float dt2)
    {
        UpdateVerletSimulation(dt, dt2);
        ResolveCollisions(dt);
        //SemiHardConstraints();
        //ResolveCollisions(dt);
        for (int i = 0; i < spacingConstraintIterations; i++)
        {
            SpacingConstraintsIteration();
            ResolveCollisions(dt);
        }

        //MoveRigidbodies();

        //ResolveCollisions();
    }

    //public void MoveRigidbodies()
    //{
    //    for (int i = 0; i < nodes.Length; i++)
    //    {
    //        nodes[i].MoveRigidbody();
    //    }
    //}

    public void UpdateRenderer(LineRenderer lr)
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
            nodes[i].UpdateVerletSimulation(dt, dt2, collisionContactFilter, collisionBuffer);
        }
    }

    private void SpacingConstraintsIteration()
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
                    c = 0.5f * c;
                    nodes[i - 1].position +=  c;
                    nodes[i].position -= c;
                }
            }
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

    private void ResolveCollisions(float dt)
    {
        for (int i = 0; i < nodes.Length; i++)
        {
            nodes[i].ResolveCollisions(dt, collisionContactFilter, collisionBuffer);
        }
    }
}