using UnityEngine;

public class PolygonPhysicsShapeComponent : MonoBehaviour
{
    public PolygonPhysicsShape pps;

    enum ShapeSource
    {
        SpriteRenderer, SpriteShape, SpriteShapeMeshGenerator
    }

    private void OnDrawGizmos()
    {
        pps.OnDrawGizmos(transform);
    }
}