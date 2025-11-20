using System;

[Serializable]
public struct SerializableSortingLayer
{
    public int layerNumber;
    //position in the array SortingLayer.layers
    //note that this may be different from layer id

    //point of this is so we can get a drop down for available sorting layers using a custom property drawer for this struct
}