using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(OutfitManager))]
public class OutfitManagerEditor : Editor
{
    SerializedProperty wardrobe;
    SerializedProperty face;
    SerializedProperty slots;

    private void OnEnable()
    {
        wardrobe = serializedObject.FindProperty("wardrobe");
        face = serializedObject.FindProperty("face");
        slots = serializedObject.FindProperty("slots");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(wardrobe);
        EditorGUILayout.PropertyField(slots);
        EditorGUILayout.PropertyField(face);
        serializedObject.ApplyModifiedProperties();

        var oM = (OutfitManager)target;
        var w = oM.wardrobe;
        if (w != null && w.OutfitNames.Length != 0) 
        {
            int selectedIndex = 0;
            for (int i = 0; i < w.OutfitNames.Length; i++)
            {
                if (w.OutfitNames[i] == oM.selectedOutfit)
                {
                    selectedIndex = i;
                    break;
                }
            }

            oM.selectedOutfit = w.OutfitNames[EditorGUILayout.Popup("Selected Outfit: ", selectedIndex, w.OutfitNames)];

            if (GUILayout.Button("Apply Selected Outfit"))
            {
                oM.ApplySelectedOutfit();
            }
        }
    }
}
