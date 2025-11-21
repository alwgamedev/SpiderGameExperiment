using System;
using UnityEngine;

public class SortingLayerControl : SortingLayerDataSource
{
    [SerializeField] SerializableSortingLayer sortingLayer;
    [SerializeField] int sortingOrder;

    public override int? SortingLayerID
    {
        get
        {
            var layers = SortingLayer.layers;
            var layerNumber = Mathf.Clamp(sortingLayer.layerNumber, 0, layers.Length - 1);
            return layers[layerNumber].id;

        }
    }

    public override int? SortingOrder => sortingOrder;
}