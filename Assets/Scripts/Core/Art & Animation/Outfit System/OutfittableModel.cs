using UnityEditor;
using UnityEngine;

public class OutfittableModel : MonoBehaviour
{
    [SerializeField] OutfitSlot[] slots;

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