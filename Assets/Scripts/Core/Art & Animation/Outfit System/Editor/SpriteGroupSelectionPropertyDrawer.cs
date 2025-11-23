using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D.Animation;

[CustomPropertyDrawer(typeof(SpriteGroupSelection))]
public class SpriteGroupSelectionPropertyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var group = (SpriteGrouping)property.FindPropertyRelative("groupingAsset").objectReferenceValue;
        if (group != null)
        {
            var cat = property.FindPropertyRelative("category").stringValue;
            var labelProperty = property.FindPropertyRelative("label");

            var curLabel = labelProperty.stringValue;
            if (string.IsNullOrEmpty(cat))
            {
                curLabel = "None";
            }

            IEnumerable<string> Labels()
            {
                yield return "None";
                if (group != null)
                {
                    foreach (var l in group.library.GetCategoryLabelNames(cat))
                    {
                        yield return l;
                    }
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

            labelProperty.stringValue = labels[EditorGUI.Popup(position, cat, selectedIndex, labels)];
        }
    }
}