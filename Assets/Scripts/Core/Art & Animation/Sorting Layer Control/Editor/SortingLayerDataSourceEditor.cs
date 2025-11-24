using UnityEditor;
using UnityEngine;

[CanEditMultipleObjects]
[CustomEditor(typeof(SortingLayerDataSource))]
public class SortingLayerDataSourceEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (GUILayout.Button("Remove All Listeners"))
        {
            foreach (var t in targets)
            {
                ((SortingLayerDataSource)t).RemoveAllListeners();
            }
        }
    }
}