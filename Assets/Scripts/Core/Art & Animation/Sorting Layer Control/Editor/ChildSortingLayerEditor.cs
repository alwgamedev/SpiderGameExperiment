using UnityEditor;
using UnityEngine;

[CanEditMultipleObjects]
[CustomEditor(typeof(ChildSortingLayer))]
public class ChildSortingLayerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (GUILayout.Button("Update Parent Subscription"))
        {
            foreach (var t in targets)
            {
                ((ChildSortingLayer)t).UpdateParentSubscription();
            }
        }
        if (GUILayout.Button("Update Sorting Data\n(including children)"))
        {
            foreach (var t in targets)
            {
                ((ChildSortingLayer)t).UpdateSortingData();
            }
        }
    }
}