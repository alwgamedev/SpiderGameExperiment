using UnityEditor;
using UnityEngine;

public class HumanColorSelector : MonoBehaviour
{
    [SerializeField] Color skinColor;
    [SerializeField] Color eyeWhiteColor;
    [SerializeField] Color irisColor;
    [SerializeField] Color hairColor;
    [SerializeField] Color lipColor;
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
    [SerializeField] SpriteRenderer[] shirtRenderers;
    [SerializeField] SpriteRenderer[] tieRenderers;
    [SerializeField] SpriteRenderer[] pantsRenderers;
    [SerializeField] SpriteRenderer[] pantsButtonRenderers;
    [SerializeField] SpriteRenderer[] beltRenderers;
    [SerializeField] SpriteRenderer[] beltBuckleRenderers;
    [SerializeField] SpriteRenderer[] shoeRenderers;

    public void UpdateColors()
    {
        foreach (var r in skinRenderers)
        {
            if (r)
            {
                r.color = skinColor;
            }
        }
        foreach (var r in eyeWhiteRenderers)
        {
            if (r)
            {
                r.color = eyeWhiteColor;
            }
        }
        foreach (var r in irisRenderers)
        {
            if (r)
            {
                r.color = irisColor;
            }
        }
        foreach (var r in hairRenderers)
        {
            if (r)
            {
                r.color = hairColor;
            }
        }
        foreach (var r in lipRenderers)
        {
            if (r)
            {
                r.color = lipColor;
            }
        }
        foreach (var r in shirtRenderers)
        {
            if (r)
            {
                r.color = shirtColor;
            }
        }
        foreach (var r in tieRenderers)
        {
            if (r)
            {
                r.color = tieColor;
            }
        }
        foreach (var r in pantsRenderers)
        {
            if (r)
            {
                r.color = pantsColor;
            }
        }
        foreach (var r in pantsButtonRenderers)
        {
            if (r)
            {
                r.color = pantsButtonColor;
            }
        }
        foreach (var r in beltRenderers)
        {
            if (r)
            {
                r.color = beltColor;
            }
        }
        foreach (var r in beltBuckleRenderers)
        {
            if (r)
            {
                r.color = beltBuckleColor;
            }
        }
        foreach (var r in shoeRenderers)
        {
            if (r)
            {
                r.color = shoeColor;
            }
        }
    }

    public void SetAllColorFieldsToWhite()
    {
#if UNITY_EDITOR
        Undo.RecordObject(this, $"Set all {typeof(HumanColorSelector).Name} color fields to white");
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
