using UnityEditor;
using UnityEngine;
using Unity.U2D.Physics;


public class PhysicsBodyStarter : MonoBehaviour
{
    public PhysicsBody body;
    public PhysicsQuery.QueryFilter queryFilter;//set ignore filter in inspector; categories will be set automatically from shapeDef

    [SerializeField] PhysicsBodyDefinition bodyDef;
    [SerializeField] PhysicsShapeDefinition shapeDef;
    [SerializeField] ShapeType geometryType;
    [SerializeField] Vector2 geometryVectorParam;
    [SerializeField] float geometryFloatParam;
    [SerializeField] bool unscaledGeometry;

    enum ShapeType { Circle, Capsule, Box, Polygon };

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
            case ShapeType.Circle:
                body = PhysicsCoreHelper.CreateCircleBody(PhysicsWorld.defaultWorld, bodyDef, shapeDef, geometryFloatParam, transform.localToWorldMatrix);
                break;
            case ShapeType.Capsule:
                Vector2 center = geometryVectorParam;
                float radius = geometryFloatParam;
                body = PhysicsCoreHelper.CreateCapsuleBody(PhysicsWorld.defaultWorld, bodyDef, shapeDef, center, -center, radius, transform.localToWorldMatrix);
                break;
            case ShapeType.Box:
                body = PhysicsCoreHelper.CreateBoxBody(PhysicsWorld.defaultWorld, bodyDef, shapeDef, geometryVectorParam, transform.localToWorldMatrix);
                break;
            case ShapeType.Polygon:
                body = PhysicsCoreHelper.CreatePolygonBody(PhysicsWorld.defaultWorld, bodyDef, shapeDef, transform.localToWorldMatrix,
                    GetComponent<PolygonPhysicsShape>().GetSubdividedPolygon());
                //2do: PolygonGeometry.CreatePolygons has to do some work to split up the polygon, so ideally we can cache that data in edit mode
                break;
        }

        //if body not valid error, check correct geometry type selected
        body.transformObject = transform;

#if UNITY_EDITOR
        SceneView.duringSceneGui += OnSceneGUI;
#endif
    }

    private void OnDestroy()
    {
        body.Destroy();//otherwise body stays in the simulation (even after transformObject is destroyed)

#if UNITY_EDITOR
        SceneView.duringSceneGui -= OnSceneGUI;
#endif
    }

#if UNITY_EDITOR

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