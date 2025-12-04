using UnityEngine;

public class DamageTest : MonoBehaviour
{
    [SerializeField] int damage;
    [SerializeField] float cooldownTime;

    float cooldownTimer;

    bool OnCooldown => cooldownTimer > 0;

    private void Update()
    {
        if (cooldownTimer > 0)
        {
            cooldownTimer -= Time.deltaTime;
        }
    }

    private void OnTriggerStay2D(Collider2D collider)
    {
        if (gameObject.activeInHierarchy && !OnCooldown && collider.gameObject.CompareTag("Player"))
        {
            ApplyDamage();
        }
    }

    private void ApplyDamage()
    {
        Spider.Player.Health.AddHealth(-damage);
        cooldownTimer = cooldownTime;
    }
}
