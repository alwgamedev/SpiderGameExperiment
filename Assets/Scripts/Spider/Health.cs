using System;
using UnityEngine;

[Serializable]
public class Health
{
    public const int CELLS_PER_POD = 3;
    public const int MAX_NUM_PODS = 6;

    public int numPods;
    public int currentHealth;
    public event Action Hurt;
    public event Action Died;

    int MaxHealth => numPods * CELLS_PER_POD;

    public void Start()
    {
        SetHealth(MaxHealth);
    }

    public void AddHealth(int amount)
    {
        SetHealth(currentHealth + amount);
        if (amount < 0)
        {
            Hurt?.Invoke();
        }
        if (currentHealth == 0)
        {
            Die();
        }
    }

    public void Die()
    {
        Debug.Log("you died!");
        Died?.Invoke();

        RestoreHealthToMax();
    }

    public void RestoreHealthToMax()
    {
        Debug.Log("restoring health...");
        SetHealth(MaxHealth);
    }

    private void SetHealth(int value)
    {
        currentHealth = Mathf.Clamp(value, 0, MaxHealth);
    }
}