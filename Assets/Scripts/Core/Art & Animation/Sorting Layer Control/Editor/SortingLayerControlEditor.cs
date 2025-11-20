using UnityEditor;
using UnityEngine;

[CanEditMultipleObjects]
[CustomEditor(typeof(SortingLayerControl))]
public class SortingLayerControlEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("Update Children"))
        {
            foreach (var t in targets)
            {
                ((SortingLayerControl)t).InvokeDataUpdatedEvent();
            }
        }
    }
}