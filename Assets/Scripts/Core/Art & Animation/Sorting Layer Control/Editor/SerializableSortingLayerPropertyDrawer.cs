using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SerializableSortingLayer))]
public class SerializableSortingLayerPropertyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var layerNumber = property.FindPropertyRelative("layerNumber");
        var sortingLayers = SortingLayer.layers.Select(x => x.name).ToArray();
        layerNumber.intValue = EditorGUI.Popup(position, label.text, layerNumber.intValue, sortingLayers);
    }
}
