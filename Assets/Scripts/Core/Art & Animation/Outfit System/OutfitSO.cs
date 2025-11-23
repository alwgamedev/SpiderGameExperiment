using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Outfit", menuName = "Scriptable Objects/Outfit")]
public class OutfitSO : ScriptableObject
{
    public Outfit outfit;

    private void OnValidate()
    {
        outfit.Refresh();
    }
}

[Serializable]
public class Outfit
{
    public SpriteGrouping grouping;
    public OutfitComponent[] components;

    Dictionary<string, SpriteGroupSliceSO> componentsDictionary = new();//group => slice for that group (which is essentially a dictionary category => label)

    public void Refresh()
    {
        if (components != null)
        {
            foreach (var c in components)
            {
                if (c.slice)
                {
                    c.slice.Refresh();
                }
            }
        }
        RebuildDictionary();
        if (grouping)
        {
            RebuildArray();
        }
    }

    public bool TryGetCategoryLabel(string group, string category, out string label)
    {
        if (componentsDictionary.Count == 0)
        {
            RebuildDictionary();
        }

        if (componentsDictionary.ContainsKey(group))
        {
            return componentsDictionary[group].TryGetCategoryLabel(category, out label);
        }

        label = string.Empty;
        return false;
    }

    public bool TryGetCategoryLabel(string category, out string label)
    {
        if (componentsDictionary.Count == 0)
        {
            RebuildDictionary();
        }

        foreach (var entry in componentsDictionary)
        {
            if (entry.Value && entry.Value.TryGetCategoryLabel(category, out label))
            {
                return true;
            }
        }

        label = string.Empty;
        return false;
    }

    private void RebuildDictionary()
    {
        componentsDictionary.Clear();
        if (grouping)//won't have this issue in unity/SO classes
        {
            foreach (var g in grouping.groupNames.names)
            {
                componentsDictionary[g] = null;
            }

            if (components != null)
            {
                foreach (var p in components)
                {
                    if (componentsDictionary.ContainsKey(p.group))
                    {
                        componentsDictionary[p.group] = p.slice;
                    }
                }
            }
        }
    }

    private void RebuildArray()
    {
        if (components == null || components.Length != componentsDictionary.Count)
        {
            Array.Resize(ref components, componentsDictionary.Count);
        }

        int i = 0;
        foreach (var x in componentsDictionary)
        {
            components[i].group = x.Key;
            components[i].slice = x.Value;
            i++;
        }
    }
}

[Serializable]
public struct OutfitComponent
{
    public string group;
    public SpriteGroupSliceSO slice;
}