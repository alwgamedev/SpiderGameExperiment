using UnityEngine;
using UnityEditor;

[CanEditMultipleObjects]
[CustomEditor(typeof(WaterMesh))]
public class WaterMeshEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("Generate Mesh"))
        {
            foreach (var t in targets)
            {
                ((WaterMesh)t).GenerateMesh();
            }
        }

        if (GUILayout.Button("Resize Collider"))
        {
            foreach (var t in targets)
            {
                ((WaterMesh)t).ResizeBoxCollider();
            }
        }
    }
}
