using UnityEngine;
using UnityEngine.UI;

public class ThrusterChargeUI : MonoBehaviour
{
    [SerializeField] Image fillBar;
    
    SpiderMovementController spider;

    private void Start()
    {
         spider = GameObject.FindAnyObjectByType<SpiderMovementController>();
    }

    private void Update()
    {
        fillBar.fillAmount = spider.ThrusterCharge;
    }
}