using System;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;

public abstract class SortingLayerDataSource : MonoBehaviour
{
    public abstract int? SortingLayerID { get; }
    public abstract int? SortingOrder { get; }

    protected SortingLayerDataSource parent;

    public SortingLayerDataSource Parent => parent;

    public UnityEvent DataUpdated;
    public UnityEvent Destroyed;

    public void InvokeDataUpdatedEvent()
    {
        Debug.Log($"{GetType().Name} {name} is broadcasting update to children");
        DataUpdated.Invoke();
    }

    public void RemoveAllListeners()
    {
        for (int i = DataUpdated.GetPersistentEventCount() - 1; i > -1; i--)
        {
            UnityEventTools.RemovePersistentListener(DataUpdated, i);
        }

        for (int i = Destroyed.GetPersistentEventCount() - 1; i > -1; i--)
        {
            UnityEventTools.RemovePersistentListener(Destroyed, i);
        }
    }

    protected void InvokeDestroyedEvent()
    {
        Destroyed.Invoke();
    }

    protected virtual void OnDestroy()
    {
        InvokeDestroyedEvent();
    }
}