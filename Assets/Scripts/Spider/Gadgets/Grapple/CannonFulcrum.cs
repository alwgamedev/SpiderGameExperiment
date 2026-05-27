using System;
using System.Runtime.CompilerServices;
using Unity.U2D.Physics;
using UnityEngine;

[Serializable]
public class CannonFulcrum
{
    [SerializeField] Transform lever;
    [SerializeField] Transform leveragePoint;
    [SerializeField] Transform fulcrumAnchor;
    [SerializeField] Transform fulcrum;//in case you don't want this to just be lever.position (i.e. not nec. at the center of the lever)
    [SerializeField] Transform baseAnchor;
    [SerializeField] float floppiness;//what fraction of force at leveragePoint is lost to spin (rest gets transferred to fulcrum)
    [SerializeField] float angularDamping;
    [SerializeField] float linearDamping;
    [SerializeField] Vector2 kinematicRotationMax;
    [SerializeField] float kinematicRotationSpeed;
    [SerializeField] float kinematicRotationCatchUpMultiplier;

    float angularAcceleration;
    float angularVelocity;

    PhysicsRotate kinematicRotation;//rotation relative to owner
    PhysicsRotate kinematicDeltaRotation;

    float leverLength;
    float inverseLeverLength;

    public Vector2 LeverDirection => inverseLeverLength * (leveragePoint.position - fulcrum.position);
    public Vector2 LeveragePoint => leveragePoint.position;
    public Vector2 FulcrumPosition => fulcrum.position;

    public void Initialize()
    {
        leverLength = Vector2.Distance(fulcrum.position, leveragePoint.position);
        inverseLeverLength = 1 / leverLength;
        kinematicRotation = PhysicsRotate.identity;
        kinematicDeltaRotation = PhysicsRotate.FromRadians(Time.fixedDeltaTime * kinematicRotationSpeed);
        RecenterLever();
    }

    public void OnValidate()
    {
        kinematicDeltaRotation = PhysicsRotate.FromRadians(Time.fixedDeltaTime * kinematicRotationSpeed);
    }

    public void UpdateDynamic(float dt)
    {
        angularVelocity += (angularAcceleration - angularDamping * angularVelocity) * dt;
        angularAcceleration = 0;
        lever.ApplyCheapRotationBySpeed(angularVelocity, dt, out var changed);
        if (changed)
        {
            RecenterLever();
        }
    }

    //aimInput > 0 is clockwise
    public void UpdateKinematic(float dt, float aimInput, PhysicsRotate shooterRotation, bool facingRight)
    {
        if (aimInput != 0)
        {
            var rotCCW = facingRight ^ aimInput > 0;
            var target = rotCCW ? kinematicRotationMax : new(-kinematicRotationMax.x, kinematicRotationMax.y);
            var rel = kinematicRotation.InverseRotateVector(target);//target rotation relative to current rotation
            var canRotate = rel.x < 0 || (rotCCW ^ rel.y < 0) ;
            if (canRotate)
            {
                kinematicRotation = rotCCW ? 
                    kinematicDeltaRotation.MultiplyRotation(kinematicRotation) : kinematicDeltaRotation.InverseMultiplyRotation(kinematicRotation);
            }
        }

        var g = facingRight ? kinematicRotation.MultiplyRotation(shooterRotation) : kinematicRotation.InverseMultiplyRotation(shooterRotation);
        lever.ApplyCheapRotationalLerpClamped(g, kinematicRotationCatchUpMultiplier * kinematicRotationSpeed * dt, out var changed);
        //^the reason to not just set the lever rotation, is that when we change from "dynamic" mode back to kinematic (i.e. when the grapple is destroyed and we regain control of the lever)
        //the rotation may be off from goal by a significant amount and we need to smoothly transition it back
        if (changed)
        {
            RecenterLever();
        }
    }

    public void ApplyForce(Vector2 force, PhysicsBody shooterBody, bool freeHanging)
    {
        Vector2 u = LeverDirection;
        var forceDirection = force.normalized;
        var fNormal = floppiness * Vector2.Dot(force, u.CCWPerp());
        angularAcceleration += inverseLeverLength * fNormal / shooterBody.mass;
        //^yes, dividing by leverLength is correct (as checked by computing theta'' from theta = arctan(y/x)) -- we're dealing with accelerations instead of force/torque
        if (freeHanging)
        {
            shooterBody.ApplyForce(force - fNormal * u - 
                shooterBody.mass * linearDamping * Vector2.Dot(shooterBody.linearVelocity, forceDirection) * forceDirection, baseAnchor.position);
        }
        else
        {
            shooterBody.ApplyForceToCenter(force - fNormal * u - 
                shooterBody.mass * linearDamping * Vector2.Dot(shooterBody.linearVelocity, forceDirection) * forceDirection);
        }
    }

    public void ResetPhysics()
    {
        angularAcceleration = 0;
        angularVelocity = 0;
    }

    private void RecenterLever()
    {
        lever.position = fulcrum.position + lever.position - fulcrumAnchor.position;
    }
}