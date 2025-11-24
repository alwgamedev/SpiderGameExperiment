using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D.Animation;

[CreateAssetMenu(fileName = "New SpriteLibraryGroups", menuName = "Scriptable Objects/Sprite Grouping")]
public class SpriteGrouping : ScriptableObject
{
    const string unassigned = "Unassigned";

    public SpriteLibraryAsset library;
    public SpriteGroupNamesAsset groupNames;
    public SpriteGroupingEntry[] grouping;

    //used to make sure every category in the sprite library appears exactly once
    Dictionary<string, string> groupContainingCategory = new();

    private void OnValidate()
    {
        Refresh();
    }

#if UNITY_EDITOR
    public void GenerateGroupNamesAsset()
    {
        if (groupNames == null)
        {
            groupNames = ScriptableObject.CreateInstance<SpriteGroupNamesAsset>();
            groupNames.name = "Group Names";
            AssetDatabase.AddObjectToAsset(groupNames, this);
            AssetDatabase.SaveAssets();
        }
    }
#endif

    public IEnumerable<string> GetCategoryNames()
    {
        foreach (var x in grouping)
        {
            yield return x.category;
        }
    }

    public void Refresh()
    {
        RebuildDictionary();
        if (library)
        {
            RebuildArray();
        }
    }

    private void RebuildDictionary()
    {
        groupContainingCategory.Clear();
        if (library)
        {
            foreach (var c in library.GetCategoryNames())
            {
                groupContainingCategory[c] = unassigned;
            }

            for (int i = 0; i < grouping.Length; i++)
            {
                if (groupContainingCategory.ContainsKey(grouping[i].category))
                {
                    if (string.IsNullOrWhiteSpace(grouping[i].group))
                    {
                        grouping[i].group = unassigned;
                    }
                    groupContainingCategory[grouping[i].category] = grouping[i].group;
                }
            }
        }
    }

    private void RebuildArray()
    {
        if (grouping == null || grouping.Length != groupContainingCategory.Count)
        {
            Array.Resize(ref grouping, groupContainingCategory.Count);
        }

        int i = 0;
        foreach (var entry in groupContainingCategory)
        {
            grouping[i].availableGroups = groupNames;
            grouping[i].category = entry.Key;
            grouping[i].group = entry.Value;
            i++;
        }

        Array.Sort(grouping, (x, y) => (x.group + x.category).CompareTo(y.group + y.category));
    }
}

//property drawer will have label (not label field) for category
//and drop down for group
[Serializable]
public struct SpriteGroupingEntry
{
    public SpriteGroupNamesAsset availableGroups;
    public string category;
    public string group;
}
