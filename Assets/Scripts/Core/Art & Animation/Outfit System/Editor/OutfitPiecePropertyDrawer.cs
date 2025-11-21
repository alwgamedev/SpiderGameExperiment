using UnityEngine;
using UnityEditor;
using UnityEngine.U2D.Animation;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using System;

[CustomPropertyDrawer(typeof(OutfitPiece))]
public class OutfitPiecePropertyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var lib = (SpriteLibraryAsset)property.FindPropertyRelative("library").objectReferenceValue;
        var cat = property.FindPropertyRelative("category").stringValue;

        var curLabel = property.FindPropertyRelative("label").stringValue;
        if (string.IsNullOrEmpty(cat))
        {
            curLabel = "None";
        }

        IEnumerable<string> Labels()
        {
            yield return "None";
            foreach (var l in lib.GetCategoryLabelNames(cat))
            {
                yield return l;
            }
        }
        var labels = Labels().ToArray();// :'(

        int selectedIndex = 0;
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i] == curLabel)
            {
                selectedIndex = i;
                break;
            }
        }

        property.FindPropertyRelative("label").stringValue = labels[EditorGUI.Popup(position, cat, selectedIndex, labels)];
    }
}
