using UnityEngine;

public class OutfitManager : MonoBehaviour
{
    public Wardrobe wardrobe;
    public string selectedOutfit;
    public Outfit3D.OutfitFace face;

    [SerializeField] OutfitSlot[] slots;

    public void ApplySelectedOutfit()
    {
        ApplyOutfit(selectedOutfit);
    }

    public void ApplyOutfit(string name)
    {
        if (wardrobe != null && wardrobe.TryGetOutfitByName(name, out var outfit))
        {
            ApplyOutfit(outfit?.GetOutfit(face));
        }
        else
        {
            Debug.LogWarning($"{gameObject.name} unable to find outfit named \"{name}\"");
        }
    }

    public void ApplyOutfit(Outfit outfit)
    {
        if (outfit != null)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].EquipOutfit(outfit);
            }
        }
    }
}