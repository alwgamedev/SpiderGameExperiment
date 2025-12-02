using UnityEditor;
using UnityEngine;

[CanEditMultipleObjects]
[CustomEditor(typeof(SortingControl))]
public class SortingControlEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("Update Children"))
        {
            foreach (var t in targets)
            {
                ((SortingControl)t).DataUpdated();
            }
        }
    }
}