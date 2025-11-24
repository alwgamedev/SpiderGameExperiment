using UnityEditor;
using UnityEngine;

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

    public override void OnParentDataUpdated(bool incrementUndoGroup = true)
    {
        if (slds)
        {
#if UNITY_EDITOR
            if (incrementUndoGroup)
            {
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName("Update Child Sorting Data");
            }
#endif
            SetSortingData(slds.SortingLayerID, slds.SortingOrder);
            Debug.Log($"{name} updated sorting data");
            DataUpdated(false);
        }
    }

    public override void OnParentDestroyed()
    {
        parent = null;
    }

    public void UpdateParentSubscription()
    {
        if (parent)
        {
            parent.RemoveChild(this);
        }

        if (!slds || IsOurChild(slds))
        {
            slds = null;
        }
        parent = slds;

        if (parent != null)
        {
            parent.AddChild(this);
        }
    }

    private void SetSortingData(int? layerID, int? layerOrder)
    {
        if (!gameObject || !layerID.HasValue || !layerOrder.HasValue) return;

        if (TryGetComponent(out Renderer renderer))
        {
            Undo.RecordObject(renderer, "Set Renderer Sorting Data");
            renderer.sortingLayerID = layerID.Value;
            renderer.sortingOrder = layerOrder.Value + orderDelta;
        }
    }

    //could get caught in a loop before makes it to us,
    //but it's impossible to ever create a loop now, since we check IsOurChild before setting parent
    private bool IsOurChild(SortingLayerDataSource s)
    {
        while (s)
        {
            if (s == this) return true;
            s = s.Parent;
        }

        return false;
    }
}