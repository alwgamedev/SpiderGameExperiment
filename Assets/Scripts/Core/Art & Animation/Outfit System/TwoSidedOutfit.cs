using UnityEngine;

[CreateAssetMenu(fileName = "Outfit 3D", menuName = "Scriptable Objects/Outfit 3D")]
public class TwoSidedOutfit : ScriptableObject
{
    public Outfit front;
    public Outfit back;

    public enum OutfitFace
    {
        front, back
    }

    public Outfit GetOutfit(OutfitFace face)
    {
        return face switch
        {
            OutfitFace.back => back,
            _ => front
        };
    }
}