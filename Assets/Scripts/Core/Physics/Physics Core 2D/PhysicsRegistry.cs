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

    //[Serializable]
    //public struct ShapeData
    //{
    //    public PBFDynamicObstacleSettings fluidObstacleSettings;

    //    //+ anything else we need
    //}

    //[Serializable]
    //public struct BodyData
    //{
    //    public bool reversed;
    //}

    //public static NativeHashMap<uint, BodyData> bodyData;
    //public static NativeHashMap<uint, ShapeData> shapeData;

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

    //public static void UpdateData(PhysicsBody body, BodyData data)
    //{
    //    var id = body.Id();
    //    if (bodyData.ContainsKey(id))
    //    {
    //        bodyData[id] = data;
    //    }
    //}

    //public static void UpdateData(PhysicsShape shape, ShapeData data)
    //{
    //    var id = shape.Id();
    //    if (shapeData.ContainsKey(id))
    //    {
    //        shapeData[id] = data;
    //    }
    //}

    public static void RegisterBodyAndShapes(PhysicsBody body)
    {
        RegisterBody(body);

        var shape = body.GetShapes();
        for (int i = 0; i < shape.Length; i++)
        {
            RegisterShape(shape[i]);
        }
    }

    public static void RegisterBody(PhysicsBody body/*, BodyData data*/)
    {
        if (body.Id() != 0)
        {
            Debug.Log($"Body already has id.");
            return;
        }

        if (nextBodyId < 0)
        {
            Debug.Log($"Out of body ids!");
            return;
        }

        var userData = body.userData;
        userData.intValue = nextBodyId;
        body.userData = userData;
        nextBodyId++;
    }

    public static void RegisterShape(PhysicsShape shape/*, ShapeData data*/)
    {
        if (shape.Id() != 0)
        {
            Debug.Log($"Shape already has id.");
            return;
        }

        if (nextShapeId < 0)
        {
            Debug.Log($"Out of shape ids!");
            return;
        }

        var userData = shape.userData;
        userData.intValue = nextShapeId;
        shape.userData = userData;
        nextShapeId++;
    }

    //public static void UnregisterBodyAndShapes(PhysicsBody body)
    //{
    //    var bodyId = (uint)body.userData.intValue;
    //    bodyData.Remove(bodyId);

    //    var shape = body.GetShapes();
    //    for (int i = 0; i <shape.Length; i++)
    //    {
    //        UnregisterShape(shape[i]);
    //    }
    //}

    //public static void UnregisterShape(PhysicsShape shape)
    //{
    //    var shapeId = (uint)shape.userData.intValue;
    //    shapeData.Remove(shapeId);
    //}

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        nextBodyId = 1;
        nextShapeId = 1;
        //if (!bodyData.IsCreated)
        //{
        //    bodyData = new NativeHashMap<uint, BodyData>(1024, Allocator.Persistent);
        //    nextBodyId = 1;
        //}
        //if (!shapeData.IsCreated)
        //{
        //    shapeData = new NativeHashMap<uint, ShapeData>(4096, Allocator.Persistent);
        //    nextShapeId = 1;
        //}

        //Application.quitting += OnApplicationQuit;
        //AppDomain.CurrentDomain.DomainUnload += OnDomainUnload;
    }

    //private static void OnApplicationQuit()
    //{
    //    Dispose();
    //    Application.quitting -= OnApplicationQuit;
    //    AppDomain.CurrentDomain.DomainUnload -= OnDomainUnload;
    //}

    //private static void OnDomainUnload(object sender, EventArgs e)
    //{
    //    Dispose();
    //    Application.quitting -= OnApplicationQuit;
    //    AppDomain.CurrentDomain.DomainUnload -= OnDomainUnload;
    //}

    //private static void Dispose()
    //{
    //    if (bodyData.IsCreated)
    //    {
    //        bodyData.Dispose();
    //    }

    //    if (shapeData.IsCreated)
    //    {
    //        shapeData.Dispose();
    //    }

    //    bodyData = default;
    //    shapeData = default;
    //}
}