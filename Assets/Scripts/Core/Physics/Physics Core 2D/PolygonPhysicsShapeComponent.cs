using UnityEngine;

public class PolygonPhysicsShapeComponent : MonoBehaviour
{
    public PolygonPhysicsShape pps;

    [SerializeField] bool drawGizmo;

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