using System;
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
    [SerializeField] float kinematicRotationMin;
    [SerializeField] float kinematicRotationMax;
    [SerializeField] float kinematicRotationSpeed;

    float angularAcceleration;
    float angularVelocity;

    float kinematicRotation0;
    float kinematicRotation;

    float leverLength;
    float inverseLeverLength;

    public Vector2 LeverDirection => inverseLeverLength * (leveragePoint.position - fulcrum.position);
    public Vector2 LeveragePoint => leveragePoint.position;
    public Vector2 FulcrumPosition => fulcrum.position;

    //2do: when you destroy grapple, lerp back to rotation 

    public void Initialize()
    {
        leverLength = Vector2.Distance(fulcrum.position, leveragePoint.position);
        inverseLeverLength = 1 / leverLength;
        kinematicRotation0 = lever.eulerAngles.z * Mathf.Deg2Rad;
        RecenterLever();
    }

    public void UpdateDynamic(float dt)
    {
        angularVelocity += (angularAcceleration - angularDamping * angularVelocity) * dt;
        angularAcceleration = 0;
        lever.ApplyCheapRotationBySpeed(angularVelocity, dt);
        RecenterLever();
    }

    public void UpdateKinematic(float dt, int rotationInput, Transform shooterTransform)
    {
        if (rotationInput != 0)
        {
            kinematicRotation = Mathf.Clamp(kinematicRotation + rotationInput * kinematicRotationSpeed * dt, kinematicRotationMin, kinematicRotationMax);
        }
        var a = kinematicRotation0 + kinematicRotation;
        var g = Mathf.Cos(a) * shooterTransform.right + (shooterTransform.localScale.x > 0 ? Mathf.Sin(a) : -Mathf.Sin(a)) * shooterTransform.transform.up;
        lever.ApplyCheapRotationBySpeedClamped(g, 2 * kinematicRotationSpeed, dt);
        RecenterLever();
    }

    public void ApplyForce(Vector2 force, Vector2 forceDirection, Rigidbody2D shooterRb, bool freeHanging)
    {
        Vector2 u = LeverDirection;
        var fNormal = floppiness * Vector2.Dot(force, u.CCWPerp());
        angularAcceleration += inverseLeverLength * fNormal / shooterRb.mass;
        //^yes, dividing by leverLength is correct (as checked by computing theta'' from theta = arctan(y/x)) -- we're dealing with accelerations instead of force/torque
        if (freeHanging)
        {
            //var fPar = Vector2.Dot(force, u);
            shooterRb.AddForceAtPosition(force - fNormal * u - shooterRb.mass * linearDamping * Vector2.Dot(shooterRb.linearVelocity, forceDirection) * forceDirection, baseAnchor.position);
        }
        else
        {
            shooterRb.AddForce(force - fNormal * u - shooterRb.mass * linearDamping * Vector2.Dot(shooterRb.linearVelocity, forceDirection) * forceDirection);
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