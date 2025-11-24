using UnityEditor;
using UnityEngine;

[CanEditMultipleObjects]
[CustomEditor(typeof(SortingLayerControl))]
public class SortingLayerControlEditor : SortingLayerDataSourceEditor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("Update Children"))
        {
            foreach (var t in targets)
            {
                ((SortingLayerControl)t).InvokeDataUpdatedEvent();
            }
        }
    }
}