using UnityEditor;
using UnityEngine;

[CanEditMultipleObjects]
[CustomEditor(typeof(ColorControl))]
public class ColorControlEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("Set Child Colors"))
        {
            foreach (var t in targets)
            {
                ((ColorControl)t).UpdateChildColors();
            }
        }
        if (GUILayout.Button("Auto Determine Child Shift & Multiplier"))
        {
            foreach (var t in targets)
            {
                ((ColorControl)t).AutoDetermineChildShiftAndMult();
            }
        }
    }
}