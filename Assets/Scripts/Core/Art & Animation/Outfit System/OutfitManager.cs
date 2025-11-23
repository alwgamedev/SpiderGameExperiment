using UnityEditor;
using UnityEngine;

public class OutfitManager : MonoBehaviour
{
    [SerializeField] Outfit3DSO outfit;
    [SerializeField] Outfit3D customOutfit;
    [SerializeField] Outfit3D.OutfitFace face;
    [SerializeField] OutfitSlot[] slots;

    IOutfit3D currentOutfit;

    //2do: option to save custom outfit to an SO (**EDITOR ONLY**)
    //+ option to (deep) copy outfit from SO to customOutfit for editing

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

    public void ApplyOutfit(Outfit outfit)
    {
        if (outfit != null)
        {
#if UNITY_EDITOR
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Apply Outfit");
#endif
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].EquipOutfit(outfit);
            }
        }
    }
}