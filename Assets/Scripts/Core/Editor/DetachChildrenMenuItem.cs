using UnityEditor;
using UnityEngine;

public static class DetachChildrenMenuItem
{
    [MenuItem("GameObject/Detach Children")]
    private static void DetachChildren()
    {
        var t = Selection.activeTransform;

        if (t != null)
        {
            var parent = t.parent;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Detach children");
            var groupID = Undo.GetCurrentGroup();

            for (int i = t.childCount - 1; i > - 1; i--)
            {
                var child = t.GetChild(i);
                if (child != null)
                {
                    Undo.SetTransformParent(child, parent, "Detach child");
                    PrefabUtility.RecordPrefabInstancePropertyModifications(child);
                }
            }

            Undo.CollapseUndoOperations(groupID);
        }
    }

    [MenuItem("GameObject/Detach All Children")]
    private static void DetachAllChildren()
    {
        var t = Selection.activeTransform;
        
        if (t != null)
        {
            var parent = t.parent;
            
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Detach all children");
            var groupID = Undo.GetCurrentGroup();

            for (int i = t.childCount - 1; i > -1; i--)
            {
                SetParentForAllChildren(t.GetChild(i), parent);
            }

            Undo.CollapseUndoOperations(groupID);
        }

        static void SetParentForAllChildren(Transform child, Transform parent)
        {
            Undo.SetTransformParent(child, parent, "Detach child");
            PrefabUtility.RecordPrefabInstancePropertyModifications(child);

            for (int i = child.childCount - 1; i > -1; i--)
            {
                SetParentForAllChildren(child.GetChild(i), parent);
            }
        }
    }
}