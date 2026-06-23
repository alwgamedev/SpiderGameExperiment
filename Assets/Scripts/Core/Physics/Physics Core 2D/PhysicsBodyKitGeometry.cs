using System;
using Unity.U2D.Physics;
using UnityEngine;

[Serializable]
public struct PolygonKitGeometry
{
    public PolygonPhysicsShapeComponent ppsc;
    public bool leaveUntransformed;
    //^false means polygon attached to kit will have the same world space positions as on ppsc
    //true means use ppsc points as is
        //(i.e. point (2, 5) in ppsc's local space will map to the point (2, 5) in kit's local space
        //so if ppsc is upside down, the polygon will become right side up when attached to kit)
}

[Serializable]
public struct BasicKitGeometry
{
    public PhysicsTransform transform;//so you can move and rotate the shape within kit's local space
    public Vector2 vectorParameter;
    public float floatParameter;
    public BasicGeometryType geometryType;

    public enum BasicGeometryType
    {
        Circle, Capsule, Box
    }

    public void OnValidate()
    {
        if (!transform.rotation.isValid)
        {
            transform.rotation = new() { direction = Vector2.right };
        }
    }

    public readonly void DrawGizmo(Color color, Matrix4x4 transformMat)
    {
        switch (geometryType)
        {
            case BasicGeometryType.Circle:
                PhysicsCoreHelper.DrawCircleGizmo(color, Circle(), transformMat);
                break;
            case BasicGeometryType.Capsule:
                PhysicsCoreHelper.DrawCapsuleGizmo(color, Capsule(), transformMat);
                break;
            case BasicGeometryType.Box:
                PhysicsCoreHelper.DrawPolygonGizmo(color, Box(), transformMat);
                break; 
        }
    }

    public readonly CircleGeometry Circle() => CircleGeometry.Create(floatParameter).Transform(transform);

    public readonly CapsuleGeometry Capsule() => CapsuleGeometry.Create(-vectorParameter, vectorParameter, floatParameter).Transform(transform);

    public readonly PolygonGeometry Box() => PolygonGeometry.CreateBox(vectorParameter, floatParameter).Transform(transform);
}