using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[ExecuteAlways]
public class ColorControl : MonoBehaviour
{
    [SerializeField] Color color;
    [SerializeField] List<ChildColor> children = new();

    public Color Color => color;

    private void OnValidate()
    {
        children.Sort(MiscTools.MbPathComparer);
    }

    public void SetColorAndUpdateChildren(Color c, bool incrementUndoGroup = true)
    {
        color = c;
        UpdateChildColors(incrementUndoGroup);
    }

    public void UpdateChildColors(bool incrementUndoGroup = true)
    {
#if UNITY_EDITOR
        if (incrementUndoGroup)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Update Child Colors");
        }
#endif
        foreach (var c in children)
        {
            if (c)
            {
                c.UpdateColor();
            }
        }
    }

    public void AutoDetermineChildShiftAndMult(bool incrementUndoGroup = true)
    {
#if UNITY_EDITOR
        if (incrementUndoGroup)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Set Child Colors' Shift & Multiplier");
        }
#endif
        foreach (var c in children)
        {
            if (c)
            {
                c.AutoDetermineShiftAndMult();
            }
        }
    }

    public void AddChild(ChildColor c)
    {
        if (c && !children.Contains(c))
        {
            children.Add(c);
            children.Sort(MiscTools.MbPathComparer);// <- to avoid fake prefab overrides caused by lists being in different order
        }
    }

    public void RemoveChild(ChildColor c)
    {
        children.RemoveAll(x => x == c);
    }

    private void OnDestroy()
    {
        foreach (var c in children)
        {
            if (c)
            {
                c.OnControlDestroyed(this);
            }
        }
    }
}
