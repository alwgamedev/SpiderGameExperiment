using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SpriteGroup))]
public class SpriteGroupPropertyDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return 2 * (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        position.height = EditorGUIUtility.singleLineHeight;

        var groupingAssetProperty = property.FindPropertyRelative("groupingAsset");
        var groupingAsset = (SpriteGrouping)groupingAssetProperty.objectReferenceValue;

        var curGroupProperty = property.FindPropertyRelative("groupName");
        var curGroup = curGroupProperty.stringValue;

        EditorGUI.PropertyField(position, groupingAssetProperty, label);

        position.y += position.height + EditorGUIUtility.standardVerticalSpacing;

        if (groupingAsset)
        {
            var groupNames = groupingAsset && groupingAsset.groupNames ? groupingAsset.groupNames.names : null;
            if (groupNames != null)
            {
                IEnumerable<string> PopupOptions()
                {
                    yield return "Unassigned";
                    foreach (var n in groupNames)
                    {
                        yield return n;
                    }
                }

                var popupOptions = PopupOptions().ToArray();

                int selectedIndex = 0;
                for (int i = 0; i < popupOptions.Length; i++)
                {
                    if (popupOptions[i] == curGroup)
                    {
                        selectedIndex = i;
                        break;
                    }
                }


                curGroupProperty.stringValue = popupOptions[EditorGUI.Popup(position, string.Empty, selectedIndex, popupOptions)];
            }
        }
        else
        {
            EditorGUI.PropertyField(position, curGroupProperty, label);
        }
    }
}