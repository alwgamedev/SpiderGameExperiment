using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D.Animation;

[Serializable]
public class Outfit : ISerializationCallbackReceiver
{
    public string name;
    public SpriteLibraryAsset library;
    public List<OutfitPiece> outfitPieces = new();
    public Dictionary<string, string> dictionary = new();

    //translate non-serialized data to serialized fields
    public void OnBeforeSerialize()
    {
        outfitPieces.Clear();
        if (library != null)
        { 
            foreach (var x in dictionary)
            {
                outfitPieces.Add(new(library, x.Key, x.Value));
            }
        }
    }

    //used serialized field to update non-serialized data
    public void OnAfterDeserialize()
    {
        dictionary.Clear();
        if (library != null)
        {
            foreach (var c in library.GetCategoryNames())
            {
                dictionary[c] = null;
            }

            foreach (var p in outfitPieces)
            {
                if (dictionary.ContainsKey(p.category))
                {
                    dictionary[p.category] = p.label;
                }
            }
        }
    }
}

//will have custom property drawer that gives a drop down of available labels in category (also with option for "None" -- which will mean hide that sprite renderer)
[Serializable]
public struct OutfitPiece
{
    public SpriteLibraryAsset library;
    public string category;
    public string label;

    public OutfitPiece(SpriteLibraryAsset library, string category, string label )
    {
        this.library = library;
        this.category = category;
        this.label = label;
    }
}