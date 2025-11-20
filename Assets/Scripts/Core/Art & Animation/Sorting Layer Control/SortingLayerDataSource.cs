using System;
using UnityEngine;

public abstract class SortingLayerDataSource : MonoBehaviour
{
    public abstract int? SortingLayerID { get; }
    public abstract int? SortingOrder { get; }

    protected SortingLayerDataSource parent;

    public SortingLayerDataSource Parent => parent;

    public event Action DataUpdated;
    public event Action Destroyed;

    public void InvokeDataUpdatedEvent()
    {
        Debug.Log($"{GetType().Name} {name} is broadcasting update to children");
        DataUpdated?.Invoke();
    }

    protected void InvokeDestroyedEvent()
    {
        Destroyed?.Invoke();
    }

    protected virtual void OnDestroy()
    {
        InvokeDestroyedEvent();
        DataUpdated = null;
        Destroyed = null;
    }
}