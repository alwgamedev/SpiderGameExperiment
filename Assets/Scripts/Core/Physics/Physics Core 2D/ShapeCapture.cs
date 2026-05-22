using System;
using Unity.Collections;
using Unity.U2D.Physics;
using UnityEngine;

[Serializable]
public struct ShapeCapture
{
    public NativeList<PhysicsCoreHelper.ShapeProxyForJobs> list;
    PhysicsAABB box;
    [SerializeField] Vector2 extents;
    [SerializeField] Vector2 effectiveExtents;//size of box that we expect to actually use

    public void Initialize(int initialCapacity)
    {
        list = new(initialCapacity, Allocator.Persistent);
    }

    /// <summary> When the effective box does not fit inside the old box, it recaptures. Use for shapes that never change their local geometry.</summary>
    public void Update(Vector2 center, PhysicsWorld world, PhysicsQuery.QueryFilter filter)
    {
        var effectiveBox = new PhysicsAABB(center - effectiveExtents, center + effectiveExtents);
        if (!box.Contains(effectiveBox))
        {
            var newBox = new PhysicsAABB(center - extents, center + extents);
            Capture(newBox, world, filter);
            box = newBox;
        }
    }

    public void Dispose()
    {
        if (list.IsCreated)
        {
            list.Dispose();
        }
    }

    private void Capture(PhysicsAABB box, PhysicsWorld world, PhysicsQuery.QueryFilter filter)
    {
        var overlap = world.OverlapAABB(box, filter);

        for (int i = 0; i < overlap.Length; i++)
        {
            var shape = overlap[i].shape;
            var id = shape.Id();
            if (id > 0)
            {
                if (!(list.Length > id))
                {
                    list.Resize(id + 1, NativeArrayOptions.ClearMemory);
                }

                switch (shape.shapeType)
                {
                    case PhysicsShape.ShapeType.Circle:
                        list[id] = new(shape.circleGeometry);
                        break;
                    case PhysicsShape.ShapeType.Capsule:
                        list[id] = new(shape.capsuleGeometry);
                        break;
                    case PhysicsShape.ShapeType.Polygon:
                        list[id] = new(shape.polygonGeometry);
                        break;
                }
            }
        }
    }
}