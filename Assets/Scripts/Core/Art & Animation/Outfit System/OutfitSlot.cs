using UnityEditor;
using UnityEngine;
using UnityEngine.U2D.Animation;

//bc you can't find the renderer from the resolver??
public class OutfitSlot : MonoBehaviour
{
    SpriteRenderer spriteRenderer;
    SpriteResolver spriteResolver;

    public SpriteRenderer SpriteRenderer
    {
        get
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }
            return spriteRenderer;
        }
    }

    public SpriteResolver SpriteResolver
    {
        get
        {
            if (spriteResolver == null)
            {
                spriteResolver = GetComponent<SpriteResolver>();
            }
            return spriteResolver;
        }
    }

    public SpriteLibraryAsset MainSpriteLibrary => SpriteResolver.spriteLibrary.spriteLibraryAsset;

    public void EquipOutfit(Outfit outfit)
    {
#if UNITY_EDITOR
        Undo.RecordObject(SpriteRenderer, "Modified Sprite Renderer");
        Undo.RecordObject(SpriteResolver, "Modified Sprite Resolver");
#endif
        var cat = SpriteResolver.GetCategory();
        if (cat == null)
        {
            Debug.Log($"{gameObject.name} is missing sprite resolver category.");
        }
        else if (outfit.TryGetCategoryLabel(cat, out var label) && label != "None")
        {
            //Debug.Log($"Setting {gameObject.name} label to {label}.");
            if (!SpriteRenderer.enabled)
            {
                SpriteRenderer.enabled = true;
            }
            SpriteResolver.SetCategoryAndLabel(cat, label);
        }
        else
        {
            SpriteRenderer.enabled = false;
        }
    }
}