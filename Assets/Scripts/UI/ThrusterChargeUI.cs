using UnityEngine;
using UnityEngine.UI;

public class ThrusterChargeUI : MonoBehaviour
{
    [SerializeField] Image fillBar;
    [SerializeField] Color cooldownColor;
    [SerializeField] Color defaultColor;

    SpiderMovementController spider;

    private void Start()
    {
         spider = GameObject.FindAnyObjectByType<SpiderMovementController>();
    }

    private void Update()
    {
        fillBar.fillAmount = spider.Thrusters.Charge;
        fillBar.color = spider.Thrusters.Cooldown ? cooldownColor : defaultColor;
    }
}