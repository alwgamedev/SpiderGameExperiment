using UnityEngine;
using UnityEngine.UI;

public class ThrusterChargeUI : MonoBehaviour
{
    [SerializeField] Image fillBar;
    [SerializeField] Color cooldownColor;
    [SerializeField] Color defaultColor;

    Thruster thruster;

    private void Start()
    {
        thruster = Spider.Player.MovementController.Thruster;
    }

    private void Update()
    {
        fillBar.fillAmount = thruster.Charge;
        fillBar.color = thruster.Cooldown ? cooldownColor : defaultColor;
    }
}