using System;
using UnityEngine;

[Serializable]
public struct SpringyMeshVertex
{
    public Vector3 position;
    public Vector3 lastPosition;
    public Vector3 acceleration;

    //public Vector3 position;
    //public Vector3 velocity;
    //public Vector3 acceleration;

    public bool freezeX;
    public bool freezeY;
    public bool freezeZ;

    //public void UpdateEulerSimulation(float dt)
    //{
    //    position += new Vector3(freezeX ? 0 : dt * velocity.x, freezeY ? 0 : dt * velocity.y, freezeZ ? 0 : dt * velocity.z);
    //    velocity += new Vector3(freezeX ? 0 : dt * acceleration.x, freezeY ? 0 : dt * acceleration.y, freezeZ ? 0 : dt * acceleration.z);
    //}

    public void FreezePosition()
    {
        freezeX = true;
        freezeY = true;
        freezeZ = true;
    }

    public void UpdateVerletSimulation(float dt)
    {
        var p = position;
        var q = 2 * position - lastPosition + dt * dt * acceleration;
        position = new Vector3(freezeX ? p.x : q.x, freezeY ? p.y : q.y, freezeZ ? p.z : q.z);
        lastPosition = p;
    }

    public SpringyMeshVertex(Vector3 position)
    {
        //this.position = position;
        //velocity = Vector3.zero;
        //acceleration = Vector3.zero;
        this.position = position;
        lastPosition = position;
        acceleration = Vector3.zero;
        freezeX = false;
        freezeY = false;
        freezeZ = false;
    }
}