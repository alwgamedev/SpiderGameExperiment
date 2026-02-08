using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SpriteShapeMeshGenerator))]
public class SpriteShapeMeshGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("Generate Mesh"))
        {
            ((SpriteShapeMeshGenerator)target).GenerateMesh();
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
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        path = FileUtil.GetProjectRelativePath(path);
        AssetDatabase.CreateAsset(mesh, path);
        AssetDatabase.SaveAssets();
    }
}
