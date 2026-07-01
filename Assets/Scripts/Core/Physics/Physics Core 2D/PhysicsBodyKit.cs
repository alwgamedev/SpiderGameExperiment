using UnityEditor;
using UnityEngine;
using Unity.U2D.Physics;

public class PhysicsBodyKit : MonoBehaviour
{
    public PhysicsBody body;
    public PhysicsQuery.QueryFilter queryFilter;//set ignore filter in inspector; categories will be set automatically from shapeDef

    [SerializeField] PhysicsBodyDefinition bodyDef;
    [SerializeField] PhysicsShapeDefinition shapeDef;
    [SerializeField] PBFDynamicObstacleSO fluidObstacle;
    [SerializeField] BasicKitGeometry[] basicShape;
    [SerializeField] PolygonKitGeometry[] polygonShape;
    [SerializeField] bool drawGizmos;
    
    public void CreateBody()
    {
        if (body.isValid)
        {
            body.Destroy();
        }

        bodyDef.position = transform.position;
        bodyDef.rotation = new PhysicsRotate(transform.rotation, PhysicsWorld.TransformPlane.XY);

        queryFilter = shapeDef.contactFilter.ToQueryFilter(queryFilter.ignoreFilter);

        body = PhysicsWorld.defaultWorld.CreateBody(bodyDef);

        var transformMat = transform.localToWorldMatrix;
        for (int i = 0; i < basicShape.Length; i++)
        {
            var shape = basicShape[i];
            switch(shape.geometryType)
            {
                case BasicKitGeometry.BasicGeometryType.Circle:
                    body.AddShape(shape.Circle(), transformMat, shapeDef);
                    break;
                case BasicKitGeometry.BasicGeometryType.Capsule:
                    body.AddShape(shape.Capsule(), transformMat, shapeDef);
                    break;
                case BasicKitGeometry.BasicGeometryType.Box:
                    body.AddShape(shape.Box(), transformMat, shapeDef);
                    break;
            }
        }

        for (int i = 0; i < polygonShape.Length; i++)
        {
            var p = polygonShape[i];
            var t = p.leaveUntransformed ? transformMat : p.ppsc.transform.localToWorldMatrix;
            body.AddPolygonBatch(p.ppsc.pps.subdividedPolygon, t, shapeDef); 
        }

        //if body not valid error, check correct geometry type selected
        body.transformObject = transform;
        var shapeData = new PhysicsRegistry.ShapeData()
        {
            fluidObstacle = fluidObstacle ? fluidObstacle.settings : default
        };
        PhysicsRegistry.RegisterBodyAndShapes(body, shapeData);
    }

    private void OnValidate()
    {
        if (body.isValid)
        {
            body.SetBodyDefLive(bodyDef);//do it live
            body.SetShapeDef(shapeDef);
            queryFilter = shapeDef.contactFilter.ToQueryFilter(queryFilter.ignoreFilter);
        }

        if (basicShape != null)
        {
            for (int i = 0; i < basicShape.Length; i++)
            {
                basicShape[i].OnValidate();
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (drawGizmos && basicShape != null)
        {
            for (int i = 0; i < basicShape.Length; i++)
            {
                basicShape[i].DrawGizmo(Color.green, transform.localToWorldMatrix);
            }
        }
    }

    private void OnEnable()
    {
        if (body.isValid)
        {
            body.enabled = true;
        }
    }

    private void OnDisable()
    {
        if (body.isValid)
        {
            body.enabled = false;
        }
    }

    private void Start()
    {
        if (!body.isValid)
        {
            CreateBody();
        }

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