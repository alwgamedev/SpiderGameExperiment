
using UnityEditor;
using UnityEngine;

[CanEditMultipleObjects]
[CustomEditor(typeof(AnyRendererSorting))]
public class AnyRendererSortingEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("Set Sorting Data"))
        {
            foreach (var t in targets)
            {
                ((AnyRendererSorting)t).SetSortingData();
            }
        }
    }
}