using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Outfit))]
public class OutfitEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("Set All Categories to Default"))
        {
            ((Outfit)target).SetAllToDefaults();
        }
    }
}