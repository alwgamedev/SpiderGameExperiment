using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "New Wardrobe", menuName = "Scriptable Objects/Wardrobe")]
public class Wardrobe : ScriptableObject, ISerializationCallbackReceiver
{
    [SerializeField] List<Outfit> outfits = new();

    public Dictionary<string, Outfit> outfitByName = new();

    public void OnBeforeSerialize()
    {
        outfits = outfitByName.Values.ToList();
    }

    public void OnAfterDeserialize()
    {
        outfitByName.Clear();
        foreach (var o in outfits)
        {
            while (outfitByName.ContainsKey(o.name))
            {
                o.name = o.name + "_1";
            }
            outfitByName[o.name] = o;
        }
    }
}