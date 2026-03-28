using UnityEngine;

public class HUD : MonoBehaviour
{
    [SerializeField] HealthBarUI healthBarUI;
    [SerializeField] ThrusterChargeUI thrusterChargeUI;

    public static HealthBarUI HealthBarUI;
    public static ThrusterChargeUI ThrusterChargeUI;

    private void Awake()
    {
        HealthBarUI = healthBarUI;
        ThrusterChargeUI = thrusterChargeUI;
    }
}