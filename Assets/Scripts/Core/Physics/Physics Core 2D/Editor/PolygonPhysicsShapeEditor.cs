using UnityEditor;
using UnityEngine;

[CanEditMultipleObjects]
[CustomEditor(typeof(PolygonPhysicsShapeComponent))]
public class PolygonPhysicsShapeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("Get Shape"))
        {
            foreach (var t in targets)
            {
                var comp = (PolygonPhysicsShapeComponent)t;
                comp.pps.GetShape(comp.gameObject);
            }
        }

        if (GUILayout.Button("Optimize Shape"))
        {
            foreach (var t in targets)
            {
                var comp = (PolygonPhysicsShapeComponent)t;
                comp.pps.OptimizeShape(comp);
            }
        }

        if (GUILayout.Button("Subdivide"))
        {
            foreach (var t in targets)
            {
                var comp = (PolygonPhysicsShapeComponent)t;
                comp.pps.SubdividePolygon(comp);
            }
        }

        if (GUILayout.Button("Set Polygon Collider Points"))
        {
            foreach (var t in targets)
            {
                var comp = (PolygonPhysicsShapeComponent)t;
                comp.pps.SetPolygonColliderPoints(comp.gameObject);
            }
        }
    }
}
