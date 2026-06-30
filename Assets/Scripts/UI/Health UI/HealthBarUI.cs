using UnityEngine;

public class HealthBarUI : MonoBehaviour
{
    [SerializeField] HealthPodColors colors;
    [SerializeField] HealthPodUI[] pod;
    [SerializeField] float animationSpeed;

    SpiderHealth health;
    float animatedHealthPoints;

    private void Start()
    {
        health = Spider.Player.health;
        animatedHealthPoints = 0;
        UpdatePods(0);
    }

    private void Update()
    {
        Animate(animationSpeed, Time.deltaTime);
    }

    private void Animate(float speed, float dt)
    {
        if (animatedHealthPoints != health.currentHealth)
        {
            if (Mathf.Abs(animatedHealthPoints - health.currentHealth) < 0.01f)
            {
                animatedHealthPoints = health.currentHealth;
            }
            else
            {
                animatedHealthPoints = Mathf.Lerp(animatedHealthPoints, health.currentHealth, speed * dt);
            }
            UpdatePods(animatedHealthPoints);
        }
    }

    private void UpdatePods(float healthPoints)
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