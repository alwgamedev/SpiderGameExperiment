using UnityEditor;
using UnityEngine;

[CanEditMultipleObjects]
[CustomEditor(typeof(ChildSortingLayer))]
public class ChildSortingLayerEditor : SortingLayerDataSourceEditor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
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