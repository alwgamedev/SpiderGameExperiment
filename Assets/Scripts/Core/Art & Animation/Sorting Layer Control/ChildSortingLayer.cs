using UnityEditor.Events;
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


    public void UpdateSortingData()
    {
        if (slds != null)
        {
            SetSortingData(slds.SortingLayerID, slds.SortingOrder);
            Debug.Log($"{name} updated sorting data");
        }

        InvokeDataUpdatedEvent();
    }

    public void UpdateParentSubscription()
    {
        if (parent)
        {
            //parent.DataUpdated -= UpdateSortingData;
            //parent.Destroyed -= UnhookCurrentSLDS;
            UnityEventTools.RemovePersistentListener(parent.DataUpdated, UpdateSortingData);
            UnityEventTools.RemovePersistentListener(parent.Destroyed, UnhookCurrentSLDS);
        }

        if (!slds || IsOurChild(slds))
        {
            slds = null;
        }
        parent = slds;

        if (parent != null)
        {
            //parent.DataUpdated += UpdateSortingData;
            //parent.Destroyed += UnhookCurrentSLDS;
            UnityEventTools.AddPersistentListener(parent.DataUpdated, UpdateSortingData);
            parent.DataUpdated.SetPersistentListenerState(parent.DataUpdated.GetPersistentEventCount() - 1, UnityEngine.Events.UnityEventCallState.EditorAndRuntime);
            UnityEventTools.AddPersistentListener(parent.Destroyed, UnhookCurrentSLDS);
            parent.Destroyed.SetPersistentListenerState(parent.Destroyed.GetPersistentEventCount() - 1, UnityEngine.Events.UnityEventCallState.EditorAndRuntime);
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
        if (parent)
        {
            UnityEventTools.RemovePersistentListener(parent.DataUpdated, UpdateSortingData);
            UnityEventTools.RemovePersistentListener(parent.Destroyed, UnhookCurrentSLDS);
            parent = null;
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

    protected override void OnDestroy()
    {
        UnhookCurrentSLDS();
        base.OnDestroy();
    }
}