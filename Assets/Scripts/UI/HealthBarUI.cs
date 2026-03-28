using UnityEngine;
using UnityEngine.UI;

public class HealthBarUI : MonoBehaviour
{
    [SerializeField] Image fillBar;

    Health health;

    private void OnEnable()
    {
        if (Spider.Player)
        {
            health = Spider.Player.Health;
        }
        if (health != null)
        {
            health.HealthChanged += UpdateHealthBar;
        }
    }

    private void Start()
    {
        if (health == null)//in case we didn't get subscribed in OnEnable
        {
            health = Spider.Player.Health;
            if (health != null)
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
        if (health != null)
        {
            health.HealthChanged -= UpdateHealthBar;
        }
    }
}