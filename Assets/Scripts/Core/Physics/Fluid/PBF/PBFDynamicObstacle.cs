using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.U2D.Physics;
using UnityEngine;
using UnityEngine.Rendering;

[Serializable]
[StructLayout(LayoutKind.Sequential)]
public struct PBFDynamicObstacle
{
    public Vector2 center;
    public Vector2 extents;
    public Vector2 xDirection;
    public float speedScaledRadius;
    public float repulsionMultiplier;
}

[CreateAssetMenu(fileName = "New Dynamic Fluid Obstacle", menuName = "Scriptable Objects/Physics/Dynamic Fluid Obstacle")]
public class PBFDynamicObstacleSO : ScriptableObject
{
    public float repulsionRadius;
    public float repulsionRadiusMax;
    public float extentsMultiplier;
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