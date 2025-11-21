using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ColorControl))]
public class ColorControlEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("Set Child Colors"))
        {
            ((ColorControl)target).UpdateChildColors();
        }
        if (GUILayout.Button("Auto Determine Child Shift & Multiplier"))
        {
            ((ColorControl)target).AutoDetermineChildShiftAndMult();
        }
    }
}