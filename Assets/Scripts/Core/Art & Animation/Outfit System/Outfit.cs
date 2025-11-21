using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.U2D.Animation;

[CreateAssetMenu(fileName = "New Outfit", menuName = "Scriptable Objects/Outfit")]
public class Outfit : ScriptableObject
{
    public SpriteLibraryAsset library;
    public List<OutfitPiece> outfitPieces = new();
    
    Dictionary<string, string> outfitPiecesDictionary = new();

    private void OnValidate()
    {
        //ensures list contains exactly one entry for each category in library (we could just do list = list.Distinct(by category), but then some categories can be missing)
        RebuildDictionary();
        RebuildList();
    }

    public bool TryGetCategoryLabel(string category, out string label)
    {
        if (outfitPiecesDictionary.Count == 0)
        {
            RebuildDictionary();
        }
        return outfitPiecesDictionary.TryGetValue(category, out label);
    }

    //mainly a time-saver when creating new outfit (invoked via button in inspector)
    public void SetAllToDefaults()
    {
        if (library != null)
        {
            for (int i = 0; i < outfitPieces.Count; i++)
            {
                //BOO LISTS!
                var p = outfitPieces[i];
                p.label = library.GetCategoryLabelNames(p.category).FirstOrDefault();
                outfitPieces[i] = p;
            }
        }
    }

    public void RebuildList()
    {
        outfitPieces.Clear();
        foreach (var x in outfitPiecesDictionary)
        {
            outfitPieces.Add(new(library, x.Key, x.Value));
        }
    }

    public void RebuildDictionary()
    {
        outfitPiecesDictionary.Clear();
        if (library != null)
        {
            foreach (var c in library.GetCategoryNames())
            {
                outfitPiecesDictionary[c] = null;
            }

            foreach (var p in outfitPieces)
            {
                if (outfitPiecesDictionary.ContainsKey(p.category))
                {
                    outfitPiecesDictionary[p.category] = p.label;
                }
            }
        }
    }
}

//has a custom property drawer that
//gives a drop down of available labels in category
//(also with option for "None" for the label -- which will mean hide that sprite renderer)
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