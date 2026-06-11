using UnityEngine;
using Unity.U2D.Physics;

public class FixedJointKit : MonoBehaviour
{
    public PhysicsBodyKit bodyA;
    public PhysicsBodyKit bodyB;
    public PhysicsFixedJoint joint;

    [SerializeField] PhysicsFixedJointDefinition jointDef;

    //2do: validate, gizmos, anchor positioning (well you can just use the joint def)

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
        if (bodyA && bodyB)
        {
            var anchorA = jointDef.localAnchorA;
            var anchorB = jointDef.localAnchorB;
            var pA = bodyA.transform.TransformPoint(anchorA.position);
            var pB = bodyB.transform.TransformPoint(anchorB.position);
            Gizmos.color = Color.red;
            Gizmos.DrawLine(pA, pA + bodyA.transform.rotation * anchorA.rotation.direction);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(pA, pB);
            Gizmos.color = Color.green;
            Gizmos.DrawLine(pB, pB + bodyB.transform.rotation * anchorB.rotation.direction);
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