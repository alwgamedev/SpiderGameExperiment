using System.Linq;
using UnityEngine;

public class Rope
{
    public float width;
    public float nodeSpacing;
    public float spacingConstraintIterations;
    
    public readonly RopeNode[] nodes;
    public readonly Vector3[] renderPositions;

    bool renderPositionsNeedUpdate;

    public Rope(Vector2 position, float width, float nodeSpacing, int numNodes, 
        float nodeDrag, float collisionRadius, /*float collisionThreshold,*/ float collisionBounciness,/*int collisionIterations,*/ int spacingConstraintIterations)
    {
        this.width = width;
        this.nodeSpacing = nodeSpacing;
        this.spacingConstraintIterations = spacingConstraintIterations;
        var a = Physics2D.gravity;
        nodes = Enumerable.Range(0, numNodes).Select(i => new RopeNode(position, Vector2.zero, a, nodeDrag, collisionRadius, 
            /*collisionThreshold*/width, collisionBounciness,/*collisionIterations,*/ i == 0)).ToArray();
        renderPositions = nodes.Select(x => (Vector3)x.position).ToArray();
    }

    //this will be a very basic rope struct
    //a class SpiderSilkShooter will do functions like elongating/shooting the rope
    //^there are two ways to handle that -- fix node spacing and elongate rope by setting more nodes active
    //or have all nodes active and increase node spacing (leading to less consistent physics as rope stretches)
    //In SpiderSilkShooter, we would probably want a method like ElongateRope(Vector2 initialVelocity)
    //--the previous anchor will get released with given initial velocity, and (inactive) node before it will assume prev anchor's
    //position 

    //for now let's make our goal just creating a simple (free-hanging) rope with constraints and collision
    //that renders and can be dragged around screen

    public void FixedUpate(float dt)
    {
        var dt2 = dt * dt;
        UpdateVerletSimulation(dt, dt2);
        ResolveCollisions();
        //FloatyConstraints();
        //ResolveCollisions();
        for (int i = 0; i < spacingConstraintIterations; i++)
        {
            SpacingConstraintsIteration();
            ResolveCollisions();
        }

        renderPositionsNeedUpdate = true;
    }

    public void Render(LineRenderer lr)
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
                    //nodes[i].ResolveCollisions();
                }
                else if (nodes[i].Anchored)
                {
                    nodes[i - 1].position += c;
                    //nodes[i - 1].ResolveCollisions();
                }
                else
                {
                    c = 0.5f * c;
                    nodes[i - 1].position +=  c;
                    nodes[i].position -= c;
                    //nodes[i - 1].ResolveCollisions();
                    //nodes[i].ResolveCollisions();
                }
            }
        }
    }

    //different feel to the rope, but can get by with ONE constraint iteration!
    //(you could also probably blend it a bit with the other method (i.e. move nodes[i-1] a little) to make the feel more like original)
    //however for spider silk, i like the stretchy feel we get with the original constraints method
    private void FloatyConstraints()
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
                    nodes[i].lastPosition -= 0.95f * c;
                }
                else if (nodes[i].Anchored)
                {
                    nodes[i - 1].position += c;
                    nodes[i - 1].lastPosition += 0.95f * c;
                }
                else
                {
                    //var c = 0.5f * error * u;
                    //nodes[i - 1].position += 0.45f * c;//c;
                    nodes[i].position -= c;//c;
                    nodes[i].lastPosition -= 0.95f * c;
                }
            }
        }
    }

    private void ResolveCollisions()
    {
        for (int i = 0; i < nodes.Length; i++)
        {
            nodes[i].ResolveCollisions();
        }
    }
}