using UnityEditor;
using UnityEngine;

[CanEditMultipleObjects]
[CustomEditor(typeof(GeyserMesh))]
public class GeyserMeshEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("Generate Mesh"))
        {
            foreach (var t in targets)
            {
                ((GeyserMesh)t).GenerateMesh();
            }
        }
    }
}
