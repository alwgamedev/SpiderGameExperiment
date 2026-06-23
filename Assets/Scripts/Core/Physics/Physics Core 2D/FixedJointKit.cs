using UnityEngine;
using Unity.U2D.Physics;

public class FixedJointKit : MonoBehaviour
{
    public PhysicsBodyKit bodyA;
    public PhysicsBodyKit bodyB;
    public PhysicsFixedJoint joint;

    [SerializeField] PhysicsFixedJointDefinition jointDef;
    [SerializeField] bool drawGizmos;

    public void CreateJoint()
    {
        if (!bodyA.body.isValid)
        {
            bodyA.CreateBody();
        }

        if (!bodyB.body.isValid)
        {
            bodyB.CreateBody();
        }

        jointDef.bodyA = bodyA.body;
        jointDef.bodyB = bodyB.body;
        joint = PhysicsFixedJoint.Create(PhysicsWorld.defaultWorld, jointDef);
    }

    void OnValidate()
    {
        if (joint.isValid)
        {
            joint.UpdateSettings(jointDef);
        }
    }

    void OnDrawGizmos()
    {
        if (drawGizmos && bodyA && bodyB)
        {
            var anchorA = joint.isValid ? joint.localAnchorA : jointDef.localAnchorA;
            var anchorB = joint.isValid ? joint.localAnchorB : jointDef.localAnchorB;
            var transformA = joint.isValid ? joint.bodyA.transform
                : new PhysicsTransform(bodyA.transform.position, new PhysicsRotate(bodyA.transform.rotation, PhysicsWorld.TransformPlane.XY));
            var transformB = joint.isValid ? joint.bodyB.transform
                : new PhysicsTransform(bodyB.transform.position, new PhysicsRotate(bodyB.transform.rotation, PhysicsWorld.TransformPlane.XY));
            var pA = transformA.TransformPoint(anchorA.position);
            var pB = transformB.TransformPoint(anchorB.position);
            var dirA = transformA.rotation.RotateVector(anchorA.rotation.direction);
            var dirB = transformB.rotation.RotateVector(anchorB.rotation.direction);
            Gizmos.color = Color.red;
            Gizmos.DrawLine(pA, pA + dirA);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(pA, pB);
            Gizmos.color = Color.green;
            Gizmos.DrawLine(pB, pB + dirB);
        }
    }

    private void Start()
    {
        if (!joint.isValid)
        {
            CreateJoint();
        }
    }
}