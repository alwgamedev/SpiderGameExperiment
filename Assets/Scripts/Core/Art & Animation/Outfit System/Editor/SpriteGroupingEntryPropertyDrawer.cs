using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SpriteGroupingEntry))]
public class SpriteGroupingEntryPropertyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var groupProperty = property.FindPropertyRelative("group");
        var curGroup = groupProperty.stringValue;
        var availableGroups = (SpriteGroupNamesAsset)(property.FindPropertyRelative("availableGroups").objectReferenceValue);

        IEnumerable<string> Groups()
        {
            yield return "Unassigned";
            if (availableGroups)
            {
                foreach (var g in availableGroups.names)
                {
                    yield return g;
                }
            }
        }
        var groups = Groups().ToArray();// :'(

        int selectedIndex = 0;
        for (int i = 0; i < groups.Length; i++)
        {
            if (groups[i] == curGroup)
            {
                selectedIndex = i;
                break;
            }
        }

        var cat = property.FindPropertyRelative("category").stringValue;
        groupProperty.stringValue = groups[EditorGUI.Popup(position, cat, selectedIndex, groups)];
    }
}