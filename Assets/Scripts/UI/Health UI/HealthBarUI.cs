using UnityEngine;

public class HealthBarUI : MonoBehaviour
{
    [SerializeField] HealthPodColors colors;
    [SerializeField] HealthPodUI[] pod;
    [SerializeField] float animationSpeed;

    Health health;
    float animatedHealthPoints;

    private void Start()
    {
        health = Spider.Player.health;
        animatedHealthPoints = 0;
        DisplayHealth(0);
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
}