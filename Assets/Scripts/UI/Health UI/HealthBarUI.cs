using UnityEngine;

public class HealthBarUI : MonoBehaviour
{
    [SerializeField] HealthPodColors colors;
    [SerializeField] HealthPodUI[] pod;
    [SerializeField] float animationSpeed;

    Health health;
    float animatedHealthPoints;

    private void OnEnable()
    {
        if (Spider.Player)
        {
            health = Spider.Player.health;
        }
        // if (health != null)
        // {
        //     health.HealthChanged += UpdateHealthBar;
        // }
    }

    private void Start()
    {
        if (health == null)//in case we didn't get subscribed in OnEnable
        {
            health = Spider.Player.health;
            animatedHealthPoints = 0;
            DisplayHealth(0);
            // if (health != null)
            // {
            //     health.HealthChanged += UpdateHealthBar;
            //     UpdateHealthBar();
            // }
        }
    }

    private void Update()
    {
        Animate();
    }

    private void Animate()
    {
        if (animatedHealthPoints != health.currentHealth)
        {
            animatedHealthPoints = Mathf.Lerp(animatedHealthPoints, health.currentHealth, Time.deltaTime * animationSpeed);
            DisplayHealth(animatedHealthPoints);
        }
    }

    private void DisplayHealth(float healthPoints)
    {
        for (int i = 0; i < health.numPods; i++)
        {
            if (!pod[i].Enabled)
            {
                pod[i].Enable();
            }

            var podHealth = Mathf.Clamp(healthPoints, 0, 3);
            pod[i].UpdatePod(podHealth, colors);
            healthPoints -= podHealth;
        }

        for (int i = health.numPods; i < pod.Length; i++)
        {
            if (pod[i].Enabled)
            {
                pod[i].Disable();
            }
        }
    }

    // private void OnDisable()
    // {
    //     if (health != null)
    //     {
    //         health.HealthChanged -= UpdateHealthBar;
    //     }
    // }
}