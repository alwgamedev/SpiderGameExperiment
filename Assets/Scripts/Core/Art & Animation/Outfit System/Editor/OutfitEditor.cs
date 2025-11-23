using UnityEditor;
using UnityEngine;

[CanEditMultipleObjects]
[CustomEditor(typeof(OutfitSO))]
public class OutfitEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        //if (GUILayout.Button("Set All Categories to Default"))
        //{
        //    ((Outfit)target).SetAllToDefaults();
        //}

        if (GUILayout.Button("Refresh Categories"))
        {
            ((OutfitSO)target).outfit.Refresh();
        }
    }
}