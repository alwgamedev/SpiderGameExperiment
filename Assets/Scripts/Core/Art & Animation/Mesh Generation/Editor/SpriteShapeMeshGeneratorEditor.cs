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
                ((SpriteShapeMeshGenerator)t).GenerateMesh(false);
            }
        }

        if (GUILayout.Button("Generate Mesh and Collider"))
        {
            foreach (var t in targets)
            {
                ((SpriteShapeMeshGenerator)t).GenerateMesh(true);
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
            SaveMesh();
        }
    }

    private void SaveMesh()
    {
        var mesh = ((SpriteShapeMeshGenerator)target).mesh;
        if (!mesh)
        {
            return;
        }

        var path = EditorUtility.SaveFilePanel("Save Terrain Mesh", "Assets/", "New Terrain Mesh", "asset");
        //^and this gives you a warning if asset already exists at that path, which is nice
        if (string.IsNullOrWhiteSpace(path))//panel was closed without selecting path
        {
            return;
        }

        path = FileUtil.GetProjectRelativePath(path);
        AssetDatabase.CreateAsset(mesh, path);
        AssetDatabase.SaveAssets();
    }
}
