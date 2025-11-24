using UnityEngine;

public class OutfitManager : MonoBehaviour
{
    [SerializeField] OutfittableModel fbModel;
    [SerializeField] OutfittableModel lrModel;
    [SerializeField] Outfit3DSO outfit;
    [SerializeField] Outfit3D customOutfit;
    [SerializeField] MathTools.OrientationXZ face;

    IOutfit3D currentOutfit;

    //2do: option to save custom outfit to an SO (**EDITOR ONLY**)
    //+ option to (deep) copy outfit from SO to customOutfit for editing

    private void OnValidate()
    {
        customOutfit?.Refresh();
    }

    public void SetFace(MathTools.OrientationXZ face)
    {
        this.face = face;
        bool fb = face == MathTools.OrientationXZ.front || face == MathTools.OrientationXZ.back;
        fbModel.gameObject.SetActive(fb);
        lrModel.gameObject.SetActive(!fb);
        ApplyOutfit(currentOutfit, face);
    }

    public void ApplySelectedOutfit()
    {
        ApplyOutfit(outfit, face);
    }

    public void ApplyCustomOutfit()
    {
        ApplyOutfit(customOutfit, face);
    }

    public void ApplyOutfit(IOutfit3D outfit, MathTools.OrientationXZ face)
    {
        if (outfit != null)
        {
            currentOutfit = outfit;
            Model(face).ApplyOutfit(outfit.GetOutfit(face));
        }
    }

    private OutfittableModel Model(MathTools.OrientationXZ face)
    {
        return face == MathTools.OrientationXZ.front || face == MathTools.OrientationXZ.back ? fbModel : lrModel;
    }
}