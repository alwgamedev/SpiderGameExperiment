using UnityEngine;

[CreateAssetMenu(fileName = "New Outfit3D", menuName = "Scriptable Objects/Outfit3D")]
public class Outfit3D : ScriptableObject
{
    public Outfit front;
    public Outfit back;
    public Outfit right;//in case e.g. there is a watch on one arm not the other
    public Outfit left;

    public enum OutfitFace
    {
        front, back, right, left
    }

    public Outfit GetOutfit(OutfitFace face)
    {
        return face switch
        {
            OutfitFace.back => back,
            OutfitFace.right => right,
            OutfitFace.left => left,
            _ => front
        };
    }
}