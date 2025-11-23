using System.Collections.Generic;
using UnityEngine;
//silly but we need the list of group names to be a Unity Object so we can access it in property drawer
public class SpriteGroupNamesAsset : ScriptableObject
{
    public string[] names;

    HashSet<string> namesSet = new();

    private void OnValidate()
    {
        FixDuplicates();
    }

    private void FixDuplicates()
    {
        if (namesSet == null)
        {
            namesSet = new();
        }
        else
        {
            namesSet.Clear();
        }
        for (int i = 0; i < names.Length; i++)
        {
            while (namesSet.Contains(names[i]))
            {
                names[i] += "_1";
            }
            namesSet.Add(names[i]);
        }
    }
}
