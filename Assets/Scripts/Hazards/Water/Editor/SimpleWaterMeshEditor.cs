using UnityEngine;
using UnityEditor;

[CanEditMultipleObjects]
[CustomEditor(typeof(SimpleWaterMesh))]
public class SimpleWaterMeshEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("Generate Mesh"))
        {
            foreach (var t in targets)
            {
                ((SimpleWaterMesh)t).GenerateMesh();
            }
        }

        if (GUILayout.Button("Resize Collider"))
        {
            foreach (var t in targets)
            {
                ((SimpleWaterMesh)t).ResizeBoxCollider();
            }
        }
    }
}
