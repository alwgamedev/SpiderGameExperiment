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

    public void EquipOutfit(Outfit outfit)
    {
        var cat = SpriteResolver.GetCategory();
        if (outfit.library == spriteResolver.spriteLibrary && outfit.dictionary.TryGetValue(cat, out var label))
        {
            if (!spriteRenderer.enabled)
            {
                spriteRenderer.enabled = true;
            }
            spriteResolver.SetCategoryAndLabel(cat, label);
        }
        else
        {
            spriteRenderer.enabled = false;
        }
    }
}