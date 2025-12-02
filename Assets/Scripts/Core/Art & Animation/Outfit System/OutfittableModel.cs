using UnityEditor;
using UnityEngine;

public class OutfittableModel : MonoBehaviour
{
    [SerializeField] OutfitSlot[] slots;
    [SerializeField] SortingControl sortingControl;
    [SerializeField] SortingControl reversedSortingControl;

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

    public void UpdateSortingData(bool reversed)
    {
        if (reversed)
        {
            reversedSortingControl.DataUpdated(false);
        }
        else
        {
            sortingControl.DataUpdated(false);
        }
    }
}