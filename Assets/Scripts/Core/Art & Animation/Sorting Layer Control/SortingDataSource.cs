using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[ExecuteAlways]
public abstract class SortingDataSource : MonoBehaviour
{
    [SerializeField] protected List<SortingDataSource> children = new();

    protected SortingDataSource parent;

    public SortingDataSource Parent => parent;

    public abstract int? SortingLayerID { get; }
    public abstract int? SortingOrder { get; }

    private void OnValidate()
    {
        children.Sort((x,y) => MiscTools.ComponentPathCompare(x, y));
    }

    public void AddChild(SortingDataSource c)
    {
        if (c && !children.Contains(c))
        {
            children.Add(c);
            children.Sort((x, y) => MiscTools.ComponentPathCompare(x, y));// <- to avoid fake prefab overrides caused by lists being in different orders
        }
    }

    public void RemoveChild(SortingDataSource c)
    {
        children.RemoveAll(x => x == c);
    }

    public void ClearChildren()
    {
        children.Clear();
    }

    public abstract void OnParentDataUpdated(bool incrementUndoGroup = true);

    public abstract void OnParentDestroyed();

    public void DataUpdated(bool incrementUndoGroup = true)
    {
#if UNITY_EDITOR
        if (incrementUndoGroup)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Update Child Sorting Data");
        }
#endif
        Debug.Log($"{GetType().Name} {name} is broadcasting update to children.");
        foreach (var c in children)
        {
            if (c)
            {
                c.OnParentDataUpdated(false);
            }
        }
    }

    protected void DestroyedEvent()
    {
        foreach (var c in children)
        {
            if (c)
            {
                c.OnParentDestroyed();
            }
        }
    }

    protected virtual void OnDestroy()
    {
        if (parent)
        {
            parent.RemoveChild(this);
        }
        DestroyedEvent();
    }
}