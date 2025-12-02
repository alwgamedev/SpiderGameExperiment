using UnityEngine;

[ExecuteAlways]
public class SortingControl : SortingDataSource
{
    [SerializeField] SerializableSortingLayer sortingLayer;
    [SerializeField] int sortingOrder;

    public override int? SortingLayerID => sortingLayer.LayerID;
    //{
    //    get
    //    {
    //        var layers = SortingLayer.layers;
    //        var layerNumber = Mathf.Clamp(sortingLayer.layerNumber, 0, layers.Length - 1);
    //        return layers[layerNumber].id;

    //    }
    //}

    public override int? SortingOrder => sortingOrder;

    public override void OnParentDataUpdated(bool incrementUndoGroup = true) { }

    public override void OnParentDestroyed() { }
}