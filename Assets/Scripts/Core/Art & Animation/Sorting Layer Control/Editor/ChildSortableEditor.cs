using UnityEditor;
using UnityEngine;

[CanEditMultipleObjects]
[CustomEditor(typeof(ChildSortable))]
public class ChildSortableEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if (GUILayout.Button("Update Parent Subscription"))
        {
            foreach (var t in targets)
            {
                ((ChildSortable)t).UpdateParentSubscription();
            }
        }
        if (GUILayout.Button("Update Sorting Data\n(including children)"))
        {
            foreach (var t in targets)
            {
                ((ChildSortable)t).OnParentDataUpdated();
            }
        }
        if (GUILayout.Button("Find Renderer in Parent"))
        {
            foreach (var t in targets)
            {
                ((ChildSortable)t).FindRendererInParent();
            }
        }
        if (GUILayout.Button("Find Renderer in Children"))
        {
            foreach (var t in targets)
            {
                ((ChildSortable)t).FindRendererInChildren();
            }
        }
    }
}