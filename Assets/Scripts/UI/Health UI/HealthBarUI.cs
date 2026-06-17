using UnityEngine;

public class HealthBarUI : MonoBehaviour
{
    [SerializeField] HealthPodColors colors;
    [SerializeField] HealthPodUI[] pod;

    Health health;

    private void OnEnable()
    {
        if (Spider.Player)
        {
            health = Spider.Player.health;
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
            health = Spider.Player.health;
            if (health != null)
            {
                health.HealthChanged += UpdateHealthBar;
                UpdateHealthBar();
            }
        }
    }

    private void UpdateHealthBar()
    {
        var curHealth = health.currentHealth;

        for (int i = 0; i < health.numPods; i++)
        {
            if (!pod[i].Enabled)
            {
                pod[i].Enable();
            }

            var podHealth = Mathf.Clamp(curHealth, 0, 3);
            pod[i].UpdatePod(podHealth, colors);
            curHealth -= podHealth;
        }

        for (int i = health.numPods; i < pod.Length; i++)
        {
            if (pod[i].Enabled)
            {
                pod[i].Disable();
            }
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.HealthChanged -= UpdateHealthBar;
        }
    }
}