using UnityEngine;

[ExecuteAlways]
public class ChildSortingLayer : SortingLayerDataSource
{
    public int orderDelta;
    public SortingLayerDataSource slds;

    public override int? SortingLayerID
    {
        get
        {
            if (TryGetComponent(out Renderer renderer))
            {
                return renderer.sortingLayerID;
            }
            if (slds != null)
            {
                return slds.SortingLayerID;
            }
            return null;
        }
    }

    public override int? SortingOrder
    {
        get
        {
            if (TryGetComponent(out Renderer renderer))
            {
                return renderer.sortingOrder;
            }
            if (slds != null && slds.SortingOrder.HasValue)
            {
                return slds.SortingOrder.Value + orderDelta;
            }
            return null;
        }
    }

    private void OnValidate()
    {
        UpdateParentSubscription();
    }


    public void UpdateSortingData()
    {
        if (slds != null)
        {
            SetSortingData(slds.SortingLayerID, slds.SortingOrder);
        }

        InvokeDataUpdatedEvent();
    }

    public void UpdateParentSubscription()
    {
        if (parent != null)
        {
            parent.DataUpdated -= UpdateSortingData;
            parent.Destroyed -= UnhookCurrentSLDS;
        }

        if (IsOurChild(slds))
        {
            slds = null;
        }
        parent = slds;

        if (parent != null)
        {
            parent.DataUpdated += UpdateSortingData;
            parent.Destroyed += UnhookCurrentSLDS;
        }
    }

    private void SetSortingData(int? layerID, int? layerOrder)
    {
        if (!gameObject || !layerID.HasValue || !layerOrder.HasValue) return;

        if (TryGetComponent(out Renderer renderer))
        {
            renderer.sortingLayerID = layerID.Value;
            renderer.sortingOrder = layerOrder.Value + orderDelta;
        }
    }

    private void UnhookCurrentSLDS()
    {
        if (parent != null)
        {
            parent.DataUpdated -= UpdateSortingData;
            parent.Destroyed -= UnhookCurrentSLDS;
            parent = null;
        }
    }

    //could theoretically stack overflow if one of our descendants has a circular parent-child relationship and the loop never gets to us,
    //but it's impossible to ever create a circular relationship now because we check IsOurChild before we set Parent
    private bool IsOurChild(SortingLayerDataSource s)
    {
        while (s != null)
        {
            if (s == this) return true;
            s = s.Parent;
        }

        return false;
    }

    protected override void OnDestroy()
    {
        UnhookCurrentSLDS();
        base.OnDestroy();
    }
}