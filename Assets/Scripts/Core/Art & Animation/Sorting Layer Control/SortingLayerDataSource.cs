using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[ExecuteAlways]
public abstract class SortingLayerDataSource : MonoBehaviour
{
    [SerializeField] List<SortingLayerDataSource> children = new();

    protected SortingLayerDataSource parent;

    public SortingLayerDataSource Parent => parent;

    public abstract int? SortingLayerID { get; }
    public abstract int? SortingOrder { get; }

    private void OnValidate()
    {
        children.Sort(MiscTools.MbPathComparer);
    }

    public void AddChild(SortingLayerDataSource c)
    {
        if (c && !children.Contains(c))
        {
            children.Add(c);
            children.Sort(MiscTools.MbPathComparer);// <- to avoid fake prefab overrides caused by lists being in different orders
        }
    }

    public void RemoveChild(SortingLayerDataSource c)
    {
        children.RemoveAll(x => x == c);
    }

    public abstract void OnParentDataUpdated(bool invertDelta = false, bool incrementUndoGroup = true);

    public abstract void OnParentDestroyed();

    public void DataUpdated(bool invertDelta = false, bool incrementUndoGroup = true)
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
                c.OnParentDataUpdated(invertDelta, false);
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