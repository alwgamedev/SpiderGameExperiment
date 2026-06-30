using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.U2D.Physics;
using UnityEngine;
using UnityEngine.Rendering;

[Serializable]
public struct PBFDynamicObstacle
{
    public float repulsionRadius;
    public float repulsionRadiusMax;
    public float extentsMultiplier;
}

[Serializable]//was for debugging purposes only
[StructLayout(LayoutKind.Sequential)]
public struct PBFDynamicObstacleState
{
    public Vector2 center;
    public Vector2 extents;
    public Vector2 xDirection;
    public float speedScaledRadius;
    public float repulsionMultiplier;
}

public class PBFDisplacementReadback
{
    public UnityEngine.Object owner;
    public Action<AsyncGPUReadbackRequest> callback;
    public NativeArray<PhysicsShape> obstacle;
    public NativeArray<int> displacement;
    public AsyncGPUReadbackRequest request;
    public int numObstacles;

    public PBFDisplacementReadback(int maxNumObstacles, UnityEngine.Object owner)
    {
        this.owner = owner;

        obstacle = new(maxNumObstacles, Allocator.Persistent);
        displacement = new(maxNumObstacles, Allocator.Persistent);

        callback = req =>
        {
            if (!this.owner)
            {
                Dispose();
            }
        };
    }

    public void Dispose()
    {
        owner = null;//yeet the reference so owner can be gc'd

        if (obstacle.IsCreated)
        {
            obstacle.Dispose();
        }

        if (displacement.IsCreated)
        {
            displacement.Dispose();
        }
    }
}