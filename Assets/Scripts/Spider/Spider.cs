using UnityEngine;

public class Spider : MonoBehaviour
{
    public Health Health { get; private set; }
    public SpiderMovementController MovementController { get; private set; }
    public Collider2D TriggerCollider { get; private set; }

    public static Spider Player { get; private set; }

    private void Awake()
    {
        Health = GetComponent<Health>();
        MovementController = GetComponent<SpiderMovementController>();
        TriggerCollider = GetComponent<Collider2D>();
        Player = this;
    }
}