using UnityEngine;
using UnityEditor;

public static class EditorTools
{
    public static bool SaveAsset(Object obj)
    {
        var path = EditorUtility.SaveFilePanel("Save Asset", "Assets/", "Unnamed Asset", "asset");
        //^and this gives you a warning if asset already exists at that path, which is nice
        if (string.IsNullOrWhiteSpace(path))//panel was closed without selecting path
        {
            return false;
        }

        path = FileUtil.GetProjectRelativePath(path);
        AssetDatabase.CreateAsset(obj, path);
        AssetDatabase.SaveAssets();
        return true;
    }
}