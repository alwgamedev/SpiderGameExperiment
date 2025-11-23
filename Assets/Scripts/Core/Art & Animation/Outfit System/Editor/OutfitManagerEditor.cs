using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(OutfitManager))]
public class OutfitManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("Apply Selected Outfit"))
        {
            ((OutfitManager)target).ApplySelectedOutfit();
        }

        if (GUILayout.Button("Apply Custom Outfit"))
        {
            ((OutfitManager)target).ApplyCustomOutfit();
        }
    }
}
