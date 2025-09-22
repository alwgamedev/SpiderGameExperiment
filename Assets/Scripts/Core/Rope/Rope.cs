using System.Linq;
using UnityEngine;

public class Rope
{
    public float width;
    public float nodeSpacing;
    public float spacingConstraintIterations;
    
    public readonly RopeNode[] nodes;

    public Rope(Vector2 position, float width, float nodeSpacing, int numNodes, 
        float nodeDrag, float nodeBounciness, float collisionRadius, int collisionIterations, int spacingConstraintIterations)
    {
        this.width = width;
        this.nodeSpacing = nodeSpacing;
        this.spacingConstraintIterations = spacingConstraintIterations;
        var a = Physics2D.gravity;
        nodes = Enumerable.Range(0, numNodes).Select(i => new RopeNode(position, Vector2.zero, a, nodeDrag, collisionRadius, 
            nodeBounciness, collisionIterations, i == 0)).ToArray();
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
        UpdateVerletSimulation(dt);
        for (int i = 0; i < spacingConstraintIterations; i++)
        {
            SpacingConstraintsIteration();
        }
    }

    private void UpdateVerletSimulation(float dt)
    {
        var dt2 = dt * dt;
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

            var d = nodes[i].Position - nodes[i - 1].Position;
            var l = d.magnitude;
            if (l > 10E-05f)
            {
                var u = d / l;
                var error = l - nodeSpacing;
                if (nodes[i - 1].Anchored)
                {
                    nodes[i].Position -= error * u;
                }
                else if (nodes[i].Anchored)
                {
                    nodes[i - 1].Position += error * u;
                }
                else
                {
                    var c = 0.5f * error * u;
                    nodes[i - 1].Position += c;
                    nodes[i].Position -= c;
                }
            }
        }
    }
}