using UnityEngine;
using UnityEngine.LowLevelPhysics2D;

public class LL2Test : MonoBehaviour
{
    [SerializeField] PhysicsBodyDefinition bodyDef;
    [SerializeField] PhysicsShapeDefinition shapeDef;

    //PhysicsWorld world;
    PhysicsBody body;


    private void Start()
    {
        body = PhysicsWorld.defaultWorld.CreateBody(bodyDef);
        body.CreateShape(new CircleGeometry() { radius = 1f }, shapeDef);
        body.transformObject = transform;
        body.position = transform.position;
        Debug.Log(body.mass);
    }

    //private void FixedUpdate()
    //{
    //    body.ApplyForce(body.mass * Physics2D.gravity, body.position);
    //}
}