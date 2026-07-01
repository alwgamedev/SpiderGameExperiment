using System;
using Unity.Collections;
using Unity.U2D.Physics;
using UnityEngine;

public static class PhysicsRegistry
{
    //we could use this setup in the future, if we want a uniform way to attach additional data (like buoyancy settings) to physics objects
    //but for now don't really need it.
    //just keep in mind some data are "settings" that you would want to be able to set in OnValidate
    //some data are state (like body.reversed) that you don't want to mess with.

    //for now the only function of the registry is to give each body a unique identifier (e.g. if you cache a bunch of shapes found in a query,
    //and want to be able to recognize them if you find them again in a subsequent query, like for rope collision where you snapshot nearby shapes to send data into a job).
    //sadly you can't access the built-in body/shape id's, and their GetHashCode combines the id with the world generation (which expires frequently)

    [Serializable]
    public struct ShapeData
    {
        public PBFDynamicObstacle fluidObstacle;
        public int projectileTarget;
        //+ anything else we need
        //if gets large we can make a separate list for each item

        public readonly bool IsFluidObstacle => fluidObstacle.repulsionRadius != 0;
    }

    static NativeList<ShapeData> shapeDataList;

    static int nextBodyId;
    static int nextShapeId;

    public static int MaxBodyId => nextBodyId - 1;
    public static int MaxShapeId => nextShapeId - 1;

    public static bool CompareId(this PhysicsBody bodyA, PhysicsBody bodyB)
    {
        return bodyA.Id() > 0 && bodyA.Id() == bodyB.Id();
    }

    public static int Id(this PhysicsBody body)
    {
        return body.userData.intValue;
    }

    public static int Id(this PhysicsShape shape)
    {
        return shape.userData.intValue;
    }

    public static ShapeData GetShapeData(int id)
    {
        return shapeDataList[id];
    }

    public static ShapeData GetShapeData(this PhysicsShape shape)
    {
        return shapeDataList[shape.Id()];
    }

    /// <summary>
    /// Outputs default shape data if id not registered.
    /// </summary>
    public static bool TryGetShapeData(int id, out ShapeData sd)
    {
        if (id == 0 || !(id < shapeDataList.Length))
        {
            sd = default;
            return false;
        }

        sd = shapeDataList[id];
        return true;
    }

    /// <summary>
    /// Outputs default shape data if shape not registered.
    /// </summary>
    public static bool TryGetShapeData(this PhysicsShape shape, out ShapeData sd)
    {
        return TryGetShapeData(shape.Id(), out sd);
    }

    public static void SetShapeData(int id, ShapeData shapeData)
    {
        if (id == 0)
        {
            Debug.Log("Shape not registered.");
            return;
        }

        if (!(shapeDataList.Count > id))
        {
            shapeDataList.Resize(id + 1, NativeArrayOptions.ClearMemory);
        }

        shapeDataList[id] = shapeData;
    }

    public static void SetShapeData(this PhysicsShape shape, ShapeData shapeData)
    {
        SetShapeData(shape.Id(), shapeData);
    }

    public static void RegisterBodyAndShapes(PhysicsBody body)
    {
        RegisterBody(body);

        var shape = body.GetShapes();
        for (int i = 0; i < shape.Length; i++)
        {
            var s = shape[i];
            RegisterShape(s);
        }
    }

    /// <summary> Uses the same ShapeData for all shapes. </summary>
    public static void RegisterBodyAndShapes(PhysicsBody body, ShapeData shapeData)
    {
        RegisterBody(body);

        var shape = body.GetShapes();
        for (int i = 0; i < shape.Length; i++)
        {
            var s = shape[i];
            RegisterShape(s);
            s.SetShapeData(shapeData);
        }
    }

    public static void RegisterBody(PhysicsBody body)
    {
        if (body.Id() != 0)
        {
            Debug.Log($"Body already has id.");
            return;
        }

        if (!(nextBodyId > 0))
        {
            Debug.Log($"Out of body ids!");
            return;
        }

        var userData = body.userData;
        userData.intValue = nextBodyId;
        body.userData = userData;
        nextBodyId++;
    }

    public static void RegisterShape(PhysicsShape shape)
    {
        if (shape.Id() != 0)
        {
            Debug.Log($"Shape already has id.");
            return;
        }

        if (!(nextShapeId > 0))
        {
            Debug.Log($"Out of shape ids!");
            return;
        }

        var id = nextShapeId;
        var userData = shape.userData;
        userData.intValue = id;
        shape.userData = userData;

        nextShapeId++;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        nextBodyId = 1;
        nextShapeId = 1;
        
        if (!shapeDataList.IsCreated)
        {
            shapeDataList = new NativeList<ShapeData>(1024, Allocator.Persistent);
        }

        Application.quitting += OnApplicationQuit;
        AppDomain.CurrentDomain.DomainUnload += OnDomainUnload;
    }

    private static void OnApplicationQuit()
    {
        Dispose();
        Application.quitting -= OnApplicationQuit;
        AppDomain.CurrentDomain.DomainUnload -= OnDomainUnload;
    }

    private static void OnDomainUnload(object sender, EventArgs e)
    {
        OnApplicationQuit();
    }

    private static void Dispose()
    {
        if (shapeDataList.IsCreated)
        {
            shapeDataList.Dispose();
        }

        shapeDataList = default;
    }
}