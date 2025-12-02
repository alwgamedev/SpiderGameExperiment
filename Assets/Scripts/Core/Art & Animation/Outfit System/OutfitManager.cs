using UnityEngine;

public class OutfitManager : MonoBehaviour
{
    [SerializeField] OutfittableModel fbModel;
    [SerializeField] OutfittableModel sideModel;
    [SerializeField] Outfit3DSO outfit;
    [SerializeField] Outfit3D customOutfit;
    [SerializeField] MathTools.OrientationXZ face;

    IOutfit3D currentOutfit;

    //2do: option to save custom outfit to an SO
    //+ option to (deep) copy outfit from SO to customOutfit for editing
    //(editor only)

    private void OnValidate()
    {
        customOutfit?.Refresh();
    }

    public void SetCurrentOutfit(bool customOutfit)
    {
        currentOutfit = customOutfit ? this.customOutfit : outfit;
    }

    public void SetFace(MathTools.OrientationXZ face)
    {
        this.face = face;
        var s = transform.localScale;

        if (face == MathTools.OrientationXZ.left)
        {
            s.x = -Mathf.Abs(s.x);
            transform.localScale = s;
        }
        else if (s.x < 0)
        {
            s.x = Mathf.Abs(s.x);
            transform.localScale = s;
        }

        var fb = face == MathTools.OrientationXZ.front || face == MathTools.OrientationXZ.back;
        fbModel.gameObject.SetActive(fb);
        sideModel.gameObject.SetActive(!fb);

        ApplyOutfit(fb ? fbModel : sideModel, currentOutfit, face);
    }

    public void ApplySelectedOutfit()
    {
        ApplyOutfit(Model(face), outfit, face);
    }

    public void ApplyCustomOutfit()
    {
        ApplyOutfit(Model(face), customOutfit, face);
    }

    public void ApplyOutfit(OutfittableModel model, IOutfit3D outfit, MathTools.OrientationXZ face)
    {
        model.ApplyOutfit(outfit.GetOutfit(face));
        model.UpdateSortingData(IsReverseSide(face));
    }

    private OutfittableModel Model(MathTools.OrientationXZ face)
    {
        return face == MathTools.OrientationXZ.front || face == MathTools.OrientationXZ.back ? fbModel : sideModel;
    }

    private bool IsReverseSide(MathTools.OrientationXZ face)
    {
        return face == MathTools.OrientationXZ.left || face == MathTools.OrientationXZ.back;
    }
}