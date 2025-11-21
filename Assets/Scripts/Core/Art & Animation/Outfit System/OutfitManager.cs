using UnityEngine;

[ExecuteAlways]
public class OutfitManager : MonoBehaviour
{
    public Wardrobe wardrobe;
    public string selectedOutfit;

    [SerializeField] OutfitSlot[] slots;

    public void ApplySelectedOutfit()
    {
        ApplyOutfit(selectedOutfit);
    }

    public void ApplyOutfit(string name)
    {
        if (wardrobe != null && wardrobe.outfitByName.TryGetValue(name, out var outfit))
        {
            ApplyOutfit(outfit);
        }
        else
        {
            Debug.LogWarning($"{gameObject.name} unable to find outfit named \"{name}\"");
        }
    }

    public void ApplyOutfit(Outfit outfit)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i].EquipOutfit(outfit);
        }
    }
}