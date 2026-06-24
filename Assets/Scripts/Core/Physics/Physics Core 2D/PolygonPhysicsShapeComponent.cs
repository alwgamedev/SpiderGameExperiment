using UnityEngine;

public class PolygonPhysicsShapeComponent : MonoBehaviour
{
    public PolygonPhysicsShape pps;
    [SerializeField] GameObject source;
    [SerializeField] bool drawGizmo;

    public GameObject Source => source ? source : gameObject;

    enum ShapeSource
    {
        SpriteRenderer, SpriteShape, SpriteShapeMeshGenerator
    }

    private void OnDrawGizmos()
    {
        if (drawGizmo)
        {
            pps.OnDrawGizmos(transform);
        }
    }
}