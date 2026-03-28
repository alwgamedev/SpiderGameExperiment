using System;
using UnityEngine;

[Serializable]
public class Health
{
    [SerializeField] int maxHealth;

    int health;

    public void Start()
    {
        health = maxHealth;
    }

    public event Action HealthChanged;

    public float HealthFraction()
    {
        return (float)health / maxHealth;
    }

    public void AddHealth(int amount)
    {
        SetHealth(health + amount);
        if (health == 0)
        {
            Die();
        }
    }

    public void Die()
    {
        Debug.Log("you died!");
        RestoreHealthToMax();
    }

    public void RestoreHealthToMax()
    {
        Debug.Log("restoring health...");
        SetHealth(maxHealth);
    }

    private void SetHealth(int value)
    {
        health = Mathf.Clamp(value, 0, maxHealth);
        HealthChanged?.Invoke();
    }
}