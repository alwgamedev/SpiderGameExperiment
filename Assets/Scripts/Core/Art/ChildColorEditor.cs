using UnityEditor;
using UnityEngine;

[CanEditMultipleObjects]
[CustomEditor(typeof(ChildColor))]
public class ChildColorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("Update Color"))
        {
            foreach (var t in targets)
            {
                ((ChildColor)t).UpdateColor();
            }
        }
        if (GUILayout.Button("Auto Determine Shift & Mult"))
        {
            foreach (var t in targets)
            {
                ((ChildColor)t).AutoDetermineShiftAndMult();
            }
        }
    }
}