using UnityEditor;
using UnityEngine;

[CanEditMultipleObjects]
[CustomEditor(typeof(SpriteShapeMeshGenerator))]
public class SpriteShapeMeshGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("Generate Mesh"))
        {
            foreach (var t in targets)
            {
                ((SpriteShapeMeshGenerator)t).GenerateMesh();
            }
        }

        if (GUILayout.Button("Apply Mesh"))
        {
            foreach (var t in targets)
            {
                ((SpriteShapeMeshGenerator)t).ApplyMesh();
            }
        }

        if (GUILayout.Button("Save Mesh"))
        {
            SaveMesh();//only one target at a time (not multi-edit compatible)
        }
    }

    private void SaveMesh()
    {
        var mesh = ((SpriteShapeMeshGenerator)target).mesh;
        if (mesh)
        {
            EditorTools.CreateAndSaveAsset(mesh);
        }
    }
}
