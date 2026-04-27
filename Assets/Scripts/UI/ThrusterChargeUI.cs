using UnityEngine;
using UnityEngine.UI;

public class ThrusterChargeUI : MonoBehaviour
{
    [SerializeField] CanvasGroup canvasGroup;
    [SerializeField] Image fillBar;
    [SerializeField] Color cooldownColor;
    [SerializeField] Color defaultColor;

    Thruster Thruster => Spider.Player.mover.Thruster;

    private void Update()
    {
        fillBar.fillAmount = Thruster.Charge;
        fillBar.color = Thruster.Cooldown ? cooldownColor : defaultColor;
    }
}