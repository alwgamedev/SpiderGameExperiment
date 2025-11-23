using System;
using UnityEngine;
using static Outfit3D;

[CreateAssetMenu(fileName = "New Outfit3D", menuName = "Scriptable Objects/Outfit3D")]
public class Outfit3DSO : ScriptableObject, IOutfit3D
{
    [SerializeField] OutfitSO front;
    [SerializeField] OutfitSO back;
    [SerializeField] OutfitSO right;//in case e.g. there is a watch on one arm not the other
    [SerializeField] OutfitSO left;

    public Outfit Front => front.outfit;
    public Outfit Back => back.outfit;
    public Outfit Right => right.outfit;
    public Outfit Left => left.outfit;

    public OutfitSO GetOutfit(Outfit3D.OutfitFace face)
    {
        return face switch
        {
            Outfit3D.OutfitFace.back => back,
            Outfit3D.OutfitFace.right => right,
            Outfit3D.OutfitFace.left => left,
            _ => front
        };
    }
}

[Serializable]
public class Outfit3D : IOutfit3D
{
    [SerializeField] Outfit front;
    [SerializeField] Outfit back;
    [SerializeField] Outfit right;//in case e.g. there is a watch on one arm not the other
    [SerializeField] Outfit left;

    public Outfit Front => front;
    public Outfit Back => back;
    public Outfit Right => right;
    public Outfit Left => left;

    public enum OutfitFace
    {
        front, back, right, left
    }

    public void Refresh()
    {
        front?.Refresh();
        back?.Refresh();
        right?.Refresh();
        left?.Refresh();
    }
}

public interface IOutfit3D
{
    public Outfit Front { get; }
    public Outfit Back { get; }
    public Outfit Right { get; }
    public Outfit Left { get; }

    public Outfit GetOutfit(OutfitFace face)
    {
        return face switch
        {
            OutfitFace.back => Back,
            OutfitFace.right => Right,
            OutfitFace.left => Left,
            _ => Front
        };
    }
}