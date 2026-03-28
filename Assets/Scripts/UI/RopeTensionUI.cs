using UnityEngine;
using UnityEngine.UI;

public class RopeTensionUI : MonoBehaviour
{
    [SerializeField] CanvasGroup canvasGroup;
    [SerializeField] Image fillBar;
    [SerializeField] float warningThreshold;
    [SerializeField] Color defaultColor;
    [SerializeField] Color warningColor;

    GrappleCannon Grapple => Spider.Player.Grapple;

    //2do: only active while grapple enabled

    private void Update()
    {
        var f = Grapple.GrappleMaxTensionFraction;
        fillBar.fillAmount = f;
        fillBar.color = f > warningThreshold ? warningColor : defaultColor;
    }

    public void Show()
    {
        canvasGroup.alpha = 1;
    }

    public void Hide()
    {
        canvasGroup.alpha = 0;
    }
}