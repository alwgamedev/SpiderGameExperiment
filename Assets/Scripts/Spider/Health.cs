using System;
using UnityEngine;

[Serializable]
public class Health
{
    public const int CELLS_PER_POD = 3;
    public const int MAX_NUM_PODS = 6;

    public int numPods;
    public int health;

    int MaxHealth => numPods * CELLS_PER_POD;

    public void Start()
    {
        health = MaxHealth;
    }

    public event Action HealthChanged;

    // public float HealthFraction()
    // {
    //     return (float)health / maxHealth;
    // }

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
        SetHealth(MaxHealth);
    }

    private void SetHealth(int value)
    {
        health = Mathf.Clamp(value, 0, MaxHealth);
        HealthChanged?.Invoke();
    }
}