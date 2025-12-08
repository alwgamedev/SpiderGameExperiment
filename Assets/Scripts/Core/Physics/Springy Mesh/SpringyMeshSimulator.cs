using System;
using UnityEngine;

[Serializable]
public class SpringyMeshSimulator
{
    [SerializeField] protected float springConstant;
    [SerializeField] protected float damping;

    public SpringyMeshVertex[] vertices;
    public int[] quads;//indices of the vertices of the quads (like triangles array in a mesh)
    public Vector3[] edgesAtRest;//the displacements between adjacent vertices when the shape is at rest

    //2do: external forces

    public void UpdateSimulation(float dt)
    {
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i].acceleration = Physics2D.gravity;
        }

        //Set accelerations
        int k = -1;
        int numQuads = quads.Length / 4;
        for (int i = 0; i < numQuads; i++)
        {
            int j = 4 * i;
            AddSpringAcceleration(ref vertices[quads[j]], ref vertices[quads[j + 1]], edgesAtRest[++k], dt);
            AddSpringAcceleration(ref vertices[quads[j + 3]], ref vertices[quads[j + 2]], edgesAtRest[++k], dt);
            AddSpringAcceleration(ref vertices[quads[j]], ref vertices[quads[j + 3]], edgesAtRest[++k], dt);
            AddSpringAcceleration(ref vertices[quads[j + 1]], ref vertices[quads[j + 2]], edgesAtRest[++k], dt);
            AddSpringAcceleration(ref vertices[quads[j]], ref vertices[quads[j + 2]], edgesAtRest[++k], dt);
            AddSpringAcceleration(ref vertices[quads[j + 1]], ref vertices[quads[j + 3]], edgesAtRest[++k], dt);
        }

        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i].UpdateVerletSimulation(dt);
            //vertices[i].UpdateEulerSimulation(dt);
        }
    }

    private void AddSpringAcceleration(ref SpringyMeshVertex p0, ref SpringyMeshVertex p1, Vector3 displacementAtRest, float dt)
    {
        var displacement = 0.5f * (p1.position - p0.position - displacementAtRest);
        var relVelocity = /*p1.velocity - p0.velocity;*/(p1.position - p1.lastPosition - p0.position + p0.lastPosition) / dt;
        var u = displacement.normalized;
        var a = -springConstant * displacement - damping * Vector2.Dot(relVelocity, u) * u;
        p0.acceleration -= a;
        p1.acceleration += a;
    }
}