using System;
using UnityEngine;

[Serializable]
public class JumpPreviewArrow
{
    [SerializeField] SpriteRenderer arrowNeck;
    [SerializeField] SpriteRenderer arrowHead;
    [SerializeField] Transform arrowNeckAnchor;//for fixing position after scaling arrowNeck
    [SerializeField] Transform arrowHeadAnchor;
    [SerializeField] float stretchMin;
    [SerializeField] float stretchMax;
    [SerializeField] Color colorMin0;//min = crouchProgress == 0 (and then arrow fades from color0 to color1 along its body)
    [SerializeField] Color colorMin1;
    [SerializeField] Color colorMax0;//max = crouchProgress == 1
    [SerializeField] Color colorMax1;

    Material neckMaterial;
    //bool arrowActive;
    //bool hasReachedMax;
    float progress;//negative = arrow inactive
    int color0Property;
    int color1Property;

    public void Start()
    {
        arrowHeadAnchor.position = new(arrowNeck.bounds.center.x, arrowNeck.bounds.max.y, 0);//need to make sure it's right on the edge of bounding box, otherwise scales poorly

        neckMaterial = new Material(arrowNeck.sharedMaterial);
        arrowNeck.sharedMaterial = neckMaterial;

        color0Property = Shader.PropertyToID("_Color0");
        color1Property = Shader.PropertyToID("_Color1");

        HideArrow();
    }

    public void OnDestroy()
    {
        UnityEngine.Object.Destroy(neckMaterial);
    }

    public void LateUpdate(SpiderMover spider)
    {
        if (spider.ChargingJump)
        {
            if (progress < 0)
            {
                ShowArrow();
            }

            UpdateArrow(spider.CrouchProgress);
        }
        else if (!(progress < 0))
        {
            HideArrow();
        }
    }

    private void ShowArrow()
    {
        progress = 0;
        arrowNeckAnchor.gameObject.SetActive(true);
        arrowNeck.gameObject.SetActive(true);
        arrowHead.gameObject.SetActive(true);
        ResetArrow();
    }

    private void HideArrow()
    {
        arrowNeckAnchor.gameObject.SetActive(false);//and arrowHeadAnchor is a child of arrowNeck so gets set inactive automatically
        arrowNeck.gameObject.SetActive(false);
        arrowHead.gameObject.SetActive(false);
        progress = -1;
    }

    private void UpdateArrow(float crouchProgress)
    {
        if (progress < 1)
        {
            SetStretch(Mathf.Lerp(stretchMin, stretchMax, crouchProgress));
            SetColor(crouchProgress);
            progress = crouchProgress;
        }
    }

    private void SetStretch(float stretch)
    {
        var p = arrowNeckAnchor.position;
        var s = arrowNeck.transform.localScale;
        s.y = stretch;
        arrowNeck.transform.localScale = s;
        arrowNeck.transform.position += p - arrowNeckAnchor.position;
        arrowHead.transform.position = arrowHeadAnchor.position;
    }

    private void SetColor(float lerpParameter)
    {
        var c0 = Color.LerpUnclamped(colorMin0, colorMax0, lerpParameter);
        var c1 = Color.LerpUnclamped(colorMin1, colorMax1, lerpParameter);
        neckMaterial.SetColor(color0Property, c0);
        neckMaterial.SetColor(color1Property, c1);
        arrowHead.color = c1;
    }

    private void ResetArrow()
    {
        SetStretch(stretchMin);
        neckMaterial.SetColor(color0Property, colorMin0);
        neckMaterial.SetColor(color1Property, colorMin1);
        arrowHead.color = colorMin1;
    }
}