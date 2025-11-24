using UnityEngine;

public class OutfitManager : MonoBehaviour
{
    [SerializeField] OutfittableModel fbModel;
    [SerializeField] OutfittableModel lrModel;
    [SerializeField] Outfit3DSO outfit;
    [SerializeField] Outfit3D customOutfit;
    [SerializeField] Outfit3D.OutfitFace face;

    IOutfit3D currentOutfit;

    //2do: option to save custom outfit to an SO (**EDITOR ONLY**)
    //+ option to (deep) copy outfit from SO to customOutfit for editing

    OutfittableModel CurrentModel => face == Outfit3D.OutfitFace.front || face == Outfit3D.OutfitFace.back ? fbModel : lrModel;

    private void OnValidate()
    {
        customOutfit?.Refresh();
    }

    public void SetFace(Outfit3D.OutfitFace face)
    {
        this.face = face;
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

    public void ApplyOutfit(IOutfit3D outfit, Outfit3D.OutfitFace face)
    {
        if (outfit != null)
        {
            currentOutfit = outfit;
            ApplyOutfit(outfit.GetOutfit(face));
        }
    }

    private void ApplyOutfit(Outfit outfit)
    {
        if (CurrentModel)
        {
            CurrentModel.ApplyOutfit(outfit);
        }
    }
}