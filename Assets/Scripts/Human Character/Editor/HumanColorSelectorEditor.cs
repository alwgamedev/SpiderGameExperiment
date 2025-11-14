using UnityEditor;
using UnityEngine;

[CanEditMultipleObjects]
[CustomEditor(typeof(HumanColorSelector))]
public class HumanColorSelectorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("Update Color"))
        {
            foreach (var t in targets)
            {
                ((HumanColorSelector)t).UpdateColors();
            }
        }

        if (GUILayout.Button("Set All Color Fields to White"))
        {
            foreach (var t in targets)
            {
                ((HumanColorSelector)t).SetAllColorFieldsToWhite();
            }
        }
    }
}
