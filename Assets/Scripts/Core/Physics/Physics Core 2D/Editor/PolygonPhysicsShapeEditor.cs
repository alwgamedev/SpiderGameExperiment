using UnityEditor;
using UnityEngine;

[CanEditMultipleObjects]
[CustomEditor(typeof(PolygonPhysicsShape))]
public class PolygonPhysicsShapeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("Get Shape"))
        {
            foreach (var t in targets)
            {
                ((PolygonPhysicsShape)t).GetShape();
            }
        }

        if (GUILayout.Button("Optimize Shape"))
        {
            foreach (var t in targets)
            {
                ((PolygonPhysicsShape)t).OptimizeShape();
            }
        }

        if (GUILayout.Button("Subdivide"))
        {
            foreach (var t in targets)
            {
                ((PolygonPhysicsShape)t).SubdividePolygon();
            }
        }

        if (GUILayout.Button("Set Polygon Collider Points"))
        {
            foreach (var t in targets)
            {
                ((PolygonPhysicsShape)t).SetPolygonColliderPoints();
            }
        }
    }
}
