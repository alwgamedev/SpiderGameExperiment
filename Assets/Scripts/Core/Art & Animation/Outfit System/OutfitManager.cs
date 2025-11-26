using UnityEngine;

public class OutfitManager : MonoBehaviour
{
    [SerializeField] OutfittableModel fbModel;
    [SerializeField] OutfittableModel sideModel;
    [SerializeField] SortingLayerControl sideModelSLC;
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

    public void SetCurrentOutfit(bool customOutfit)
    {
        currentOutfit = customOutfit ? this.customOutfit : outfit;
        sideModel.ApplyOutfit(currentOutfit.Side);
        if (face == MathTools.OrientationXZ.front || face == MathTools.OrientationXZ.back)
        {
            fbModel.ApplyOutfit(currentOutfit.GetOutfit(face));
        }
        else
        {
            fbModel.ApplyOutfit(currentOutfit.Front);
        }
    }

    public void SetFace(MathTools.OrientationXZ face)
    {
        this.face = face;
        bool fb = face == MathTools.OrientationXZ.front || face == MathTools.OrientationXZ.back;
        fbModel.gameObject.SetActive(fb);
        sideModel.gameObject.SetActive(!fb);
        sideModelSLC.DataUpdated(face == MathTools.OrientationXZ.left);
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
        if (fb)
        {
            ApplyOutfit(currentOutfit, face);
        }
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
        return face == MathTools.OrientationXZ.front || face == MathTools.OrientationXZ.back ? fbModel : sideModel;
    }
}