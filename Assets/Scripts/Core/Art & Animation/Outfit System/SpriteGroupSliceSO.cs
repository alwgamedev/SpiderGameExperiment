using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New SpriteGroupSlice", menuName = "Scriptable Objects/Sprite Group Slice")]
public class SpriteGroupSliceSO : ScriptableObject
{
    public SpriteGroup group;
    public SpriteGroupLabel[] sprites;

    Dictionary<string, string> categoryLabels = new();

    private void OnValidate()
    {
        Refresh();
    }

    public void ClearAllLabels()
    {
        categoryLabels.Clear();

        for (int i = 0; i < sprites.Length; i++)
        {
            sprites[i].label = "None";
            categoryLabels[sprites[i].category] = "None";
        }
    }

    public void SetCategoryLabel(string category, string label)
    {
        if (categoryLabels.Count == 0)
        {
            RebuildDictionary();
        }

        if (categoryLabels.ContainsKey(category))
        {
            categoryLabels[category] = label;

            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i].category == category)
                {
                    sprites[i].label = label;
                    break;
                }
            }
        }
    }

    public bool TryGetCategoryLabel(string category, out string label)
    {
        if (categoryLabels.Count == 0)
        {
            RebuildDictionary();
        }

        return categoryLabels.TryGetValue(category, out label);
    }

    //will be called in OnValidate of SO version
    public void Refresh()
    {
        if (group.groupingAsset)
        {
            group.groupingAsset.Refresh();
        }
        RebuildDictionary();
        if (group.groupingAsset)
        {
            RebuildArray();
        }
    }

    private void RebuildDictionary()
    {
        categoryLabels.Clear();

        if (group.groupingAsset != null)
        {
            foreach (var c in group.groupingAsset.grouping)
            {
                if (c.group == group.groupName)
                {
                    categoryLabels[c.category] = "None";
                }
            }

            foreach (var s in sprites)
            {
                if (categoryLabels.ContainsKey(s.category))
                {
                    categoryLabels[s.category] = s.label;
                }
            }
        }
    }

    private void RebuildArray()
    {
        if (sprites == null || sprites.Length != categoryLabels.Count)
        {
            Array.Resize(ref sprites, categoryLabels.Count);
        }

        int i = 0;
        foreach (var entry in categoryLabels)
        {
            sprites[i].groupingAsset = group.groupingAsset;
            sprites[i].category = entry.Key;
            sprites[i].label = entry.Value;
            i++;
        }
    }
}

//custom property drawer with group drop down
[Serializable]
public struct SpriteGroup
{
    public SpriteGrouping groupingAsset;
    public string groupName;
}

[Serializable]
public struct SpriteGroupLabel
{
    public SpriteGrouping groupingAsset;
    public string category;
    public string label;
}