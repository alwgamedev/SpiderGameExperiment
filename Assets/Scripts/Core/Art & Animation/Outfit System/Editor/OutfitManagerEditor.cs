using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(OutfitManager))]
public class OutfitManagerEditor : Editor
{
    SerializedProperty wardrobe;
    SerializedProperty slots;

    private void OnEnable()
    {
        wardrobe = serializedObject.FindProperty("wardrobe");
        slots = serializedObject.FindProperty("slots");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(wardrobe);
        EditorGUILayout.PropertyField(slots);
        serializedObject.ApplyModifiedProperties();

        var oM = (OutfitManager)target;
        var w = oM.wardrobe;
        if (w != null)
        {
            var labels = w.outfitByName.Keys.ToArray();
            int selectedIndex = 0;
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] == oM.selectedOutfit)
                {
                    selectedIndex = i;
                    break;
                }
            }

            oM.selectedOutfit = labels[EditorGUILayout.Popup("Selected Outfit: ", selectedIndex, labels)];

            if (GUILayout.Button("Apply Selected Outfit"))
            {
                oM.ApplySelectedOutfit();
            }
        }
        else
        {
            Debug.Log("wardrobe null");
        }
    }
}
