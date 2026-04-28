using System;
using UnityEngine;
using Unity.U2D.Physics;

[Serializable]
public struct SGrabberAnchor
{
    PhysicsHingeJoint joint;
    Vector2 offPosition;//relative to joint.bodyA's transform
    Vector2 deployedPosition;

    [SerializeField] float moveSpeed;
    Target target;

    enum Target
    {
        none, offPos, deployedPos
    }

    public void OnDrawGizmos()
    {
        if (joint.isValid)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(joint.bodyA.transform.TransformPoint(joint.localAnchorA.position), 0.1f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(joint.bodyB.transform.TransformPoint(joint.localAnchorB.position), 0.1f);
        }
    }

    public void Initialize(PhysicsHingeJoint joint, Vector2 offWorldPosition, Vector2 deployedWorldPosition)
    {
        this.joint = joint;
        offPosition = joint.bodyA.transform.InverseTransformPoint(offWorldPosition);
        deployedPosition = joint.bodyA.transform.InverseTransformPoint(deployedWorldPosition);
        var anchorA = joint.localAnchorA;
        anchorA.position = offPosition;
        joint.localAnchorA = anchorA;
    }

    public void Disable()
    {
        target = Target.none;
    }

    public void BeginTargetingOffPosition()
    {
        target = Target.offPos;
    }

    public void BeginTargetingDeployedPosition()
    {
        target = Target.deployedPos;
    }

    public bool Update(float dt)
    {
        if (target == Target.none)
        {
            return false;
        }

        var targetPos = target == Target.offPos ? offPosition : deployedPosition;
        var anchor = joint.localAnchorA;
        var d = targetPos - anchor.position;
        var l = d.sqrMagnitude;

        if (l < MathTools.o91)
        {
            target = Target.none;
            return true;
        }

        l = Mathf.Sqrt(l);
        var move = Mathf.Min(moveSpeed * dt, l);
        anchor.position += move / l * d;
        joint.localAnchorA = anchor;
        return false;
    }
}