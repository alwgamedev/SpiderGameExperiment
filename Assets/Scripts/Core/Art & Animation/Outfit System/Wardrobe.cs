using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Wardrobe", menuName = "Scriptable Objects/Wardrobe")]
public class Wardrobe : ScriptableObject//, ISerializationCallbackReceiver
{
    [SerializeField] List<TwoSidedOutfit> outfits;
    
    string[] outfitNames;
    Dictionary<string, TwoSidedOutfit> outfitByName = new();

    public string[] OutfitNames => outfitNames;

    private void OnValidate()
    {
        RebuildDictionary();
    }

    public bool TryGetOutfitByName(string name, out TwoSidedOutfit o)
    {
        if (outfitByName.Count == 0)
        {
            RebuildDictionary();
        }

        return outfitByName.TryGetValue(name, out o);
    }

    public void RebuildDictionary()
    {
        outfitByName.Clear();
        foreach (var o in outfits)
        {
            if (o != null)
            {
                if (string.IsNullOrEmpty(o.name))
                {
                    o.name = "Unnamed Outfit";
                }
                while (outfitByName.ContainsKey(o.name))
                {
                    o.name += "_1";
                }
                outfitByName[o.name] = o;
            }
        }

        if (outfitNames == null)
        {
            outfitNames = new string[outfitByName.Count];
        }
        else if (outfitNames.Length != outfitByName.Count)
        {
            Array.Resize(ref outfitNames, outfitByName.Count);
        }
        int i = 0;
        foreach (var n in outfitByName.Keys)
        {
            outfitNames[i++] = n;
        }
    }
}