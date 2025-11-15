using UnityEditor;
using UnityEngine;

public class HumanColorSelector : MonoBehaviour
{
    [SerializeField] Color skinColor;
    [SerializeField] Color eyeWhiteColor;
    [SerializeField] Color irisColor;
    [SerializeField] Color hairColor;
    [SerializeField] Color lipColor;
    [SerializeField] Color lipCornerShadowColor;
    [SerializeField] Color shirtColor;
    [SerializeField] Color tieColor;
    [SerializeField] Color pantsColor;
    [SerializeField] Color pantsButtonColor;
    [SerializeField] Color beltColor;
    [SerializeField] Color beltBuckleColor;
    [SerializeField] Color shoeColor;
    [SerializeField] SpriteRenderer[] skinRenderers;
    [SerializeField] SpriteRenderer[] eyeWhiteRenderers;
    [SerializeField] SpriteRenderer[] irisRenderers;
    [SerializeField] SpriteRenderer[] hairRenderers;
    [SerializeField] SpriteRenderer[] lipRenderers;
    [SerializeField] SpriteRenderer[] lipCornerShadowRenderers;
    [SerializeField] SpriteRenderer[] shirtRenderers;
    [SerializeField] SpriteRenderer[] tieRenderers;
    [SerializeField] SpriteRenderer[] pantsRenderers;
    [SerializeField] SpriteRenderer[] pantsButtonRenderers;
    [SerializeField] SpriteRenderer[] beltRenderers;
    [SerializeField] SpriteRenderer[] beltBuckleRenderers;
    [SerializeField] SpriteRenderer[] shoeRenderers;

    public void UpdateColors()
    {
        SetColor(skinRenderers, skinColor);
        SetColor(eyeWhiteRenderers, eyeWhiteColor);
        SetColor(irisRenderers, irisColor);
        SetColor(hairRenderers, hairColor);
        SetColor(lipRenderers, lipColor);
        SetColor(lipCornerShadowRenderers, skinColor * lipCornerShadowColor);
        SetColor(shirtRenderers, shirtColor);
        SetColor(tieRenderers, tieColor);
        SetColor(pantsRenderers, pantsColor);
        SetColor(pantsButtonRenderers, pantsButtonColor);
        SetColor(beltRenderers, beltColor);
        SetColor(beltBuckleRenderers, beltBuckleColor);
        SetColor(shoeRenderers, shoeColor);

#if UNITY_EDITOR
        PrefabUtility.RecordPrefabInstancePropertyModifications(this);
#endif

        void SetColor(SpriteRenderer[] renderers, Color color)
        {
            foreach (var r in renderers)
            {
                if (r)
                {
                    r.color = color;
                }
            }
        }
    }

    public void SetAllColorFieldsToWhite()
    {
#if UNITY_EDITOR
        Undo.RecordObject(this, $"Set all color selector fields to white");
#endif
        skinColor = Color.white;
        eyeWhiteColor = Color.white;
        irisColor = Color.white;
        hairColor = Color.white;
        lipColor = Color.white;
        shirtColor = Color.white;
        tieColor = Color.white;
        pantsColor = Color.white;
        pantsButtonColor = Color.white;
        beltColor = Color.white;
        beltBuckleColor = Color.white;
        shoeColor = Color.white;
    }
}
