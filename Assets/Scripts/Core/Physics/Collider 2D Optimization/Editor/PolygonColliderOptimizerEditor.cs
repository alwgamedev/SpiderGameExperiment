using UnityEngine;
using UnityEditor;
using Collider2DOptimization;

[CanEditMultipleObjects]
[CustomEditor(typeof(PolygonColliderOptimizer))]
public class PolygonColliderOptimizerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("Refresh"))
        {
            foreach (var t in targets)
            {
                ((PolygonColliderOptimizer)t).Refresh();
            }
        }
    }
}
