using UnityEngine;
using UnityEngine.UI;

public class HealthBarUI : MonoBehaviour
{
    [SerializeField] Image fillBar;

    Health health;

    private void OnEnable()
    {
        if (!health && Spider.Player)
        {
            health = Spider.Player.Health;
        }
        if (health)
        {
            health.HealthChanged += UpdateHealthBar;
        }
    }

    private void Start()
    {
        if (!health)
        {
            health = Spider.Player.Health;
            if (health)
            {
                health.HealthChanged += UpdateHealthBar;
            }
        }
    }

    private void UpdateHealthBar()
    {
        fillBar.fillAmount = health.HealthFraction();
    }

    private void OnDisable()
    {
        if (health)
        {
            health.HealthChanged -= UpdateHealthBar;
        }
    }
}