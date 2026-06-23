using UnityEditor;
using UnityEngine;

[ExecuteAlways]
public class AnyRendererSorting : MonoBehaviour
{
    [SerializeField] SerializableSortingLayer sortingLayer;
    [SerializeField] int sortingOrder;
    [SerializeField] Renderer renderer;

    public void SetSortingData()
    {
        #if UNITY_EDITOR
        Undo.RecordObject(renderer, "Set Renderer Sorting Data");
        #endif
        renderer.sortingLayerID = sortingLayer.LayerID;
        renderer.sortingOrder = sortingOrder;

        #if UNITY_EDITOR
        EditorUtility.SetDirty(renderer);
        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        #endif
    }
}