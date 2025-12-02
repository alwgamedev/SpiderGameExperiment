using System;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

[ExecuteAlways]//needed for OnDestroy to get called when component is removed
public class ChildSortable : SortingDataSource
{
    [SerializeField] Renderer _renderer;
    [SerializeField] int orderDelta;
    [SerializeField] SortingDataSource slds;

    public override int? SortingLayerID
    {
        get
        {
            if (_renderer)
            {
                return _renderer.sortingLayerID;
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
            if (_renderer)
            {
                return _renderer.sortingOrder;
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

    public void FindRendererInParent()
    {
        _renderer = GetComponentInParent<Renderer>();
    }

    public void FindRendererInChildren()
    {
        _renderer = GetComponentInChildren<Renderer>();
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

        if (_renderer)
        {
#if UNITY_EDITOR
            Undo.RecordObject(_renderer, "Set Renderer Sorting Data");
#endif
            _renderer.sortingLayerID = layerID.Value;
            _renderer.sortingOrder = layerOrder.Value + orderDelta;
        }
        else
        {
            Debug.Log($"Unable to set sorting data. No renderer found.");
        }
    }

    //could get caught in a loop before makes it to us,
    //but it's impossible to ever create a loop now, since we check IsOurChild before setting parent
    private bool IsOurChild(SortingDataSource s)
    {
        while (s)
        {
            if (s == this) return true;
            s = s.Parent;
        }

        return false;
    }
}