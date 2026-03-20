using UnityEditor;
using UnityEngine;
using Unity.U2D.Physics;


public class PhysicsBodyStarter : MonoBehaviour
{
    public PhysicsBody body;
    public PhysicsShape shape;
    public PhysicsQuery.QueryFilter queryFilter;//set ignore filter in inspector; categories will be set automatically from shapeDef

    [SerializeField] PhysicsBodyDefinition bodyDef;
    [SerializeField] PhysicsShapeDefinition shapeDef;
    [SerializeField] PhysicsGeometryType geometryType;
    [SerializeField] Vector2 geometryVectorParam;
    [SerializeField] float geometryFloatParam;
    [SerializeField] bool unscaledGeometry;

    enum PhysicsGeometryType { Circle, Capsule, Box, Polygon };

    private void OnValidate()
    {
        if (body.isValid)
        {
            body.SetBodyDefLive(bodyDef);//do it live
            body.SetShapeDef(shapeDef);
            queryFilter = shapeDef.contactFilter.ToQueryFilter(queryFilter.ignoreFilter);
        }
    }

    private void Start()
    {
        bodyDef.position = transform.position;
        bodyDef.rotation = new PhysicsRotate(transform.rotation, PhysicsWorld.TransformPlane.XY);

        queryFilter = shapeDef.contactFilter.ToQueryFilter(queryFilter.ignoreFilter);

        switch (geometryType)
        {
            case PhysicsGeometryType.Circle:
                body = PhysicsCoreHelper.CreateCirceBody(PhysicsWorld.defaultWorld, bodyDef, shapeDef, 
                    unscaledGeometry ? geometryFloatParam : transform.localScale.x * geometryFloatParam, out shape);
                break;
            case PhysicsGeometryType.Capsule:
                Vector2 center = geometryVectorParam;
                float radius = geometryFloatParam;
                if (unscaledGeometry)
                {
                    center = geometryVectorParam;//still will be used in body local space
                    radius = geometryFloatParam;
                }
                else
                {
                    var edge = center + radius * center.normalized;
                    center = body.transform.InverseTransformPoint(transform.TransformPoint(center));
                    edge = body.transform.InverseTransformPoint(transform.TransformPoint(edge));
                    radius = Vector2.Distance(center, edge);
                }
                body = PhysicsCoreHelper.CreateCapsuleBody(PhysicsWorld.defaultWorld, bodyDef, shapeDef, center, -center, radius, out shape);
                break;
            case PhysicsGeometryType.Box:
                body = PhysicsCoreHelper.CreateBoxBody(PhysicsWorld.defaultWorld, bodyDef, shapeDef, 
                    unscaledGeometry ? geometryVectorParam : transform.localScale * geometryVectorParam, out shape);
                break;
            case PhysicsGeometryType.Polygon:
                body = PhysicsCoreHelper.CreatePolygonBody(PhysicsWorld.defaultWorld, bodyDef, shapeDef,
                    unscaledGeometry ? Vector2.one : transform.localScale, GetComponent<PolygonPhysicsShape>().GetSubdividedPolygon());
                //2do: PolygonGeometry.CreatePolygons has to do some work to split up the polygon, so ideally we can cache that data in edit mode
                break;
        }

        if (!body.isValid)
        {
            Debug.Log($"Physics body not valid on {gameObject.name} (check the correct geometry type is selected).");
        }
        else
        {
            body.transformObject = transform;
        }

#if UNITY_EDITOR
        SceneView.duringSceneGui += OnSceneGUI;
#endif
    }

#if UNITY_EDITOR
    private void OnDestroy()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    bool dragging;

    //make body draggable in scene view
    private void OnSceneGUI(SceneView sceneView)
    {
        if (dragging && (Event.current.type == EventType.MouseUp || Selection.activeTransform != transform))
        {
            body.transformWriteMode = bodyDef.transformWriteMode;
            dragging = false;
        }
        else if (dragging || (Event.current.type == EventType.MouseDrag && Selection.activeTransform == transform))
        {
            if (!dragging)
            {
                body.transformWriteMode = PhysicsBody.TransformWriteMode.Off;
                //but we continue to interact with physics world, so e.g. dragging a piece of ground won't make supported objects suddenly fall into oblivion
                dragging = true;
            }
            body.position = transform.position;
            body.rotation = new PhysicsRotate(transform.rotation, PhysicsWorld.TransformPlane.XY);
        }
    }
#endif
}