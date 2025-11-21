using UnityEditor;
using UnityEngine;

[CanEditMultipleObjects]
[CustomEditor(typeof(ModelColorSelector))]
public class ModelColorSelectorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("Update Color"))
        {
            foreach (var t in targets)
            {
                ((ModelColorSelector)t).UpdateColors();
            }
        }

        if (GUILayout.Button("Auto Determine Child Shift & Multiplier"))
        {
            foreach (var t in targets)
            {
                ((ModelColorSelector)t).AutoDeterminateChildShiftAndMult();
            }
        }

        if (GUILayout.Button("Set All Color Fields to White"))
        {
            foreach (var t in targets)
            {
                ((ModelColorSelector)t).SetAllColorFieldsToWhite();
            }
        }
    }
}
