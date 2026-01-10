using UnityEngine;

public class PBFDynamicObstacle : MonoBehaviour
{
    [SerializeField] Collider2D coll;
    [SerializeField] Rigidbody2D rb;
    [SerializeField] float repulsionRadius;
    [SerializeField] float repulsionRadiusMax;

    public Collider2D Collider => coll;
    public Rigidbody2D Rigidbody => rb;
    public float RepulsionRadius => repulsionRadius;
    public float RepulsionRadiusMax => repulsionRadiusMax;
}