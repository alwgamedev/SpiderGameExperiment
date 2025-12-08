using UnityEditor;
using UnityEngine;

[CanEditMultipleObjects]
[CustomEditor(typeof(WaterMeshManager))]
public class WaterMeshManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("Generate Mesh"))
        {
            foreach (var t in targets)
            {
                ((WaterMeshManager)t).Initialize();
            }
        }

        if (GUILayout.Button("Resize Collider"))
        {
            foreach (var t in targets)
            {
                ((WaterMeshManager)t).ResizeCollider();
            }
        }
    }
}