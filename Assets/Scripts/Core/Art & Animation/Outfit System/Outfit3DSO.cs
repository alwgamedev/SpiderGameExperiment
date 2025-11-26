using System;
using UnityEngine;

[CreateAssetMenu(fileName = "New Outfit3D", menuName = "Scriptable Objects/Outfit3D")]
public class Outfit3DSO : ScriptableObject, IOutfit3D
{
    [SerializeField] OutfitSO front;
    [SerializeField] OutfitSO back;
    [SerializeField] OutfitSO side;
    //[SerializeField] OutfitSO right;//in case e.g. there is a watch on one arm not the other
    //[SerializeField] OutfitSO left;

    public Outfit Front => front.outfit;
    public Outfit Back => back.outfit;
    public Outfit Side => side.outfit;
    //public Outfit Right => right.outfit;
    //public Outfit Left => left.outfit;

    public OutfitSO GetOutfitSO(MathTools.OrientationXZ face)
    {
        return face switch
        {
            MathTools.OrientationXZ.back => back,
            MathTools.OrientationXZ.right => side,
            MathTools.OrientationXZ.left => side,
            _ => front
        };
    }
}

[Serializable]
public class Outfit3D : IOutfit3D
{
    [SerializeField] Outfit front;
    [SerializeField] Outfit back;
    [SerializeField] Outfit side;
    //[SerializeField] Outfit right;//in case e.g. there is a watch on one arm not the other
    //[SerializeField] Outfit left;

    public Outfit Front => front;
    public Outfit Back => back;
    public Outfit Side => side;
    //public Outfit Right => right;
    //public Outfit Left => left;

    //public enum OutfitFace
    //{
    //    front, back, right, left
    //}

    public void Refresh()
    {
        front?.Refresh();
        back?.Refresh();
        side?.Refresh();
        //right?.Refresh();
        //left?.Refresh();
    }
}

public interface IOutfit3D
{
    public Outfit Front { get; }
    public Outfit Back { get; }
    public Outfit Side { get; }
    //public Outfit Right { get; }
    //public Outfit Left { get; }

    public Outfit GetOutfit(MathTools.OrientationXZ face)
    {
        return face switch
        {
            MathTools.OrientationXZ.back => Back,
            MathTools.OrientationXZ.right => Side,
            MathTools.OrientationXZ.left => Side,
            _ => Front
        };
    }
}