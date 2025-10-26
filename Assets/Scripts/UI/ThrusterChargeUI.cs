using UnityEngine;
using UnityEngine.UI;

public class ThrusterChargeUI : MonoBehaviour
{
    [SerializeField] Image fillBar;
    [SerializeField] Color cooldownColor;
    [SerializeField] Color defaultColor;

    SpiderMovementController player;

    private void Start()
    {
        player = SpiderMovementController.Player;
    }

    private void Update()
    {
        fillBar.fillAmount = player.Thrusters.Charge;
        fillBar.color = player.Thrusters.Cooldown ? cooldownColor : defaultColor;
    }
}