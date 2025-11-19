using UnityEditor;

public static class CopyPathMenuItem
{
    [MenuItem("GameObject/Copy Path")]
    private static void CopyPath()
    {
        var go = Selection.activeGameObject;

        if (go == null)
        {
            return;
        }

        var path = go.name;

        while (go.transform.parent != null)
        {
            go = go.transform.parent.gameObject;
            path = $"{go.name}/{path}";
        }

        EditorGUIUtility.systemCopyBuffer = path;
    }

    [MenuItem("GameObject/Copy Path", true)]
    private static bool CopyPathValidation()
    {
        //don't show copy path option when multiple game objects are selected
        return Selection.gameObjects.Length == 1;
    }
}