using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SpriteGrouping))]
public class SpriteLibraryGroupsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var t = target as SpriteGrouping;
        DrawDefaultInspector();
        if (t.groupNames == null && GUILayout.Button("Generate Group Names Asset"))
        {
            t.GenerateGroupNamesAsset();
        }
    }
}