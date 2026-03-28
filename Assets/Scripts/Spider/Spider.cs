using UnityEngine;

public class Spider : MonoBehaviour
{
    [SerializeField] Health health;
    [SerializeField] SpiderMover mover;
    [SerializeField] GrappleCannon grapple;

    public Health Health => health;
    public SpiderMover Mover => mover;
    public GrappleCannon Grapple => grapple;
    public Collider2D TriggerCollider { get; private set; }//was for fluid interaction; needs to be updated to new physics system

    public static Spider Player { get; private set; }

    private void Awake()
    {
        Player = this;
    }

    private void Start()
    {
        health.Start();
    }
}