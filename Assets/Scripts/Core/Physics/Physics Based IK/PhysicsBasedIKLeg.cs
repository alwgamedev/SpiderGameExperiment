using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public struct PhysicsLegSettings
{
    public float gravityStrength;
    public float reachForce;
    public float limpness;
    public float jointDamping;
    public float stepHeight;
    public bool enforceAngleBounds;

    public static PhysicsLegSettings Lerp(PhysicsLegSettings s1, PhysicsLegSettings s2, float t)
    {
        return new PhysicsLegSettings
        {
            gravityStrength = Mathf.Lerp(s1.gravityStrength, s2.gravityStrength, t),
            reachForce = Mathf.Lerp(s1.reachForce, s2.reachForce, t),
            limpness = Mathf.Lerp(s1.limpness, s2.limpness, t),
            jointDamping = Mathf.Lerp(s1.jointDamping, s2.jointDamping, t),
            stepHeight = Mathf.Lerp(s1.stepHeight, s2.stepHeight, t),
            enforceAngleBounds = s1.enforceAngleBounds,
        };
    }
}

[Serializable]
public class PhysicsBasedIKLeg
{
    //public PhysicsLegSettings contactSettings;
    //public PhysicsLegSettings noContactSettings;
    //public UnityEvent HitGround;

    //[SerializeField] Transform orientingTransform;
    //[SerializeField] Transform[] chain;
    //[SerializeField] Transform target;
    //[SerializeField] float stepMax;
    //[SerializeField] float[] minAngle;
    //[SerializeField] float[] maxAngle;
    //[SerializeField] bool[] angleBranch;
    ////angle branch:
    ////false = (-pi, pi), true = (0, 2pi)
    ////for (-pi,pi) branch, the angle bounds are bounds for the z-coordinate of the local rotation (sin(t/2), and we need to assume q.w > 0)
    ////for (0,2pi) branch, the angle bounds are bounds for the w-coordinate of the local rotation (cos(t/2), and we need to assume q.z > 0)

    //Vector2[] positionBuffer;
    //float[] angularVelocity;
    //float[] length;
    //float[] inverseLength;
    //float totalLength;
    //float totalLengthInverse;

    //public bool EffectorIsTouchingGround { get; private set; }
    //public Vector2 EffectorPosition => chain[^1].position;
    //public Vector2 LegExtensionVector => chain[^1].position - chain[0].position;
    //public float LegExtensionFraction => LegExtensionVector.magnitude * totalLengthInverse;

    //public void Initialize()
    //{
    //    totalLength = 0f;
    //    length = new float[chain.Length - 1];
    //    inverseLength = new float[chain.Length - 1];
    //    angularVelocity = new float[chain.Length - 1];
    //    positionBuffer = new Vector2[chain.Length];

    //    for (int i = 0; i < length.Length; i++)
    //    {
    //        var l = Vector2.Distance(chain[i].position, chain[i + 1].position);
    //        length[i] = l;
    //        inverseLength[i] = 1 / l;
    //        totalLength += l;
    //        positionBuffer[i] = chain[i].position;
    //    }
    //    positionBuffer[^1] = chain[^1].position;

    //    totalLengthInverse = 1 / totalLength;
    //    target.position = chain[^1].position;
    //}

    //public void UpdateJoints(GroundMap groundMap, float dt, float reachToleranceSqrd,
    //    float groundContactRadius, float collisionResponse, float simulateContactWeight)
    //{
    //    var settings = EffectorIsTouchingGround ? contactSettings
    //        : simulateContactWeight > 0 ? PhysicsLegSettings.Lerp(noContactSettings, contactSettings, simulateContactWeight)
    //        : noContactSettings;

    //    PhysicsBasedIK.IntegrateJoints(chain, angularVelocity, settings.jointDamping, dt);

    //    if (settings.limpness != 0)
    //    {
    //        Limp(settings.limpness, dt);
    //    }

    //    UpdateGroundContact(groundMap, groundContactRadius, collisionResponse, dt);

    //    if (!EffectorIsTouchingGround && settings.gravityStrength != 0)
    //    {
    //        PhysicsBasedIK.ApplyGravity(chain, inverseLength, angularVelocity, settings.gravityStrength, dt);
    //    }

    //    PhysicsBasedIK.ReachForTargetWithAngleBounds(chain, length, inverseLength, angularVelocity, minAngle, maxAngle, angleBranch, orientingTransform.localScale.x,
    //                target.position, settings.reachForce, reachToleranceSqrd, dt);

    //    for (int i = 0; i < positionBuffer.Length; i++)
    //    {
    //        //put current positions into buffer (for limpness to use next update)
    //        positionBuffer[i] = chain[i].position;
    //    }
    //}

    //public void UpdateTargetStepping(GroundMap map, Vector2 castDir,
    //    float stepHeightSpeedMultiplier, float stepHeightFraction, float stepProgress, float stepDistance,
    //    float restDistance)
    //{
    //    ref var settings = ref EffectorIsTouchingGround ? ref contactSettings : ref noContactSettings;
    //    var stepHeight = settings.stepHeight;
    //    var bodyFacingRight = orientingTransform.localScale.x > 0;

    //    var dot = Vector2.Dot((Vector2)chain[0].position - (Vector2)map.Origin, map.OriginRight);
    //    float h;
    //    if (!map.LineCastToGround((Vector2)chain[0].position, castDir, out var p) || Mathf.Abs(p.arcLengthPosition) < Mathf.Abs(dot))
    //    {
    //        h = dot;
    //    }
    //    else
    //    {
    //        h = p.arcLengthPosition;
    //    }

    //    var stepStart = GetStepStart(map, h, bodyFacingRight, stepProgress, stepDistance, restDistance/*, stepMax*/);
    //    var stepGoal = GetStepGoal(map, h, bodyFacingRight, 0, restDistance);
    //    var stepRight = stepGoal - stepStart;
    //    var stepUp = bodyFacingRight ? 0.5f * stepRight.CCWPerp() : 0.5f * stepRight.CWPerp();

    //    //parabola instead of trig fcts
    //    var newTargetPos = stepStart + stepProgress * stepRight + 4 * stepProgress * (1 - stepProgress) * stepHeightFraction * stepHeight * stepUp;

    //    if (stepHeightSpeedMultiplier < 1)
    //    {
    //        var g = map.ProjectOntoGroundByArcLength(newTargetPos, out _, out _);
    //        newTargetPos = Vector2.Lerp(g, newTargetPos, stepHeightSpeedMultiplier);
    //    }

    //    target.position = newTargetPos;
    //}

    //public void UpdateTargetResting(GroundMap map, Vector2 castDir,
    //    float restProgress, float restDistance)
    //{
    //    var dot = Vector2.Dot((Vector2)chain[0].position - (Vector2)map.Origin, map.OriginRight);

    //    float h;
    //    if (!map.LineCastToGround((Vector2)chain[0].position, castDir, out var p) || Mathf.Abs(p.arcLengthPosition) < Mathf.Abs(dot))
    //    {
    //        h = dot;
    //    }
    //    else
    //    {
    //        h = p.arcLengthPosition;
    //    }

    //    target.position = GetStepGoal(map, h, orientingTransform.localScale.x > 0, restProgress, restDistance);
    //}

    //public void ClampTargetPosition(GroundMap map, float maxExtensionFraction)
    //{
    //    var d = target.position - chain[0].position;
    //    var f = d.sqrMagnitude * totalLengthInverse * totalLengthInverse;
    //    if (f > maxExtensionFraction * maxExtensionFraction)
    //    {
    //        var p = chain[0].position + maxExtensionFraction / Mathf.Sqrt(f) * d;
    //        target.position = (Vector2)map.TrueClosestPoint(new float2(p.x, p.y), out _, out _, out _);
    //    }
    //}

    //public void OnBodyChangedDirection(Vector2 position0, Vector2 position1, Vector2 flipNormal)
    //{
    //    for (int i = 0; i < angularVelocity.Length; i++)
    //    {
    //        angularVelocity[i] = -angularVelocity[i];
    //        var v = positionBuffer[i] - position0;
    //        positionBuffer[i] = position1 + MathTools.ReflectAcrossHyperplane(v, flipNormal);
    //    }

    //    var w = positionBuffer[^1] - position0;
    //    positionBuffer[^1] = position1 + MathTools.ReflectAcrossHyperplane(w, flipNormal);
    //}

    //private void Limp(float limpness, float dt)
    //{
    //    limpness *= dt;
    //    for (int i = 1; i < positionBuffer.Length; i++)
    //    {
    //        chain[i - 1].ApplyCheapRotationalLerp(orientingTransform.localScale.x * (positionBuffer[i] - (Vector2)chain[i - 1].position).normalized, limpness, out _);
    //    }
    //}

    //private void UpdateGroundContact(GroundMap groundMap, float groundContactRadius, float collisionResponse, float dt)
    //{
    //    var wasTouchingGround = EffectorIsTouchingGround;
    //    Vector2 q = chain[^1].position;
    //    q -= (Vector2)groundMap.TrueClosestPoint(q, out _, out var n, out var hitGround);

    //    var l = Vector2.Dot(q, n);
    //    EffectorIsTouchingGround = hitGround && l < groundContactRadius;
    //    if (EffectorIsTouchingGround)
    //    {
    //        if (l < 0)
    //        {
    //            var a = -dt * collisionResponse * l * n;
    //            PhysicsBasedIK.ApplyForceUpChain(chain, inverseLength, angularVelocity, a, chain.Length - 1, null, true);
    //        }

    //        if (!wasTouchingGround)
    //        {
    //            HitGround.Invoke();
    //        }
    //    }
    //}

    //private Vector2 GetStepStart(GroundMap map, float h, bool bodyFacingRight, float stepProgress, float stepDistance, float restDistance/*, float stepMax*/)
    //{
    //    h = bodyFacingRight ? h + StepStartHorizontalOffset(stepProgress, stepDistance, restDistance/*, stepMax*/)
    //        : h - StepStartHorizontalOffset(stepProgress, stepDistance, restDistance/*, stepMax*/);
    //    return map.PointFromCenterByArcLength(h, out _, out _);
    //    //return map.PointFromCenterByIntervalWidth(h);
    //}

    //private Vector2 GetStepGoal(GroundMap map, float h, bool bodyFacingRight, float restProgress, float restDistance)
    //{
    //    h += bodyFacingRight ? StepGoalHorizontalOffset(restProgress, restDistance) : -StepGoalHorizontalOffset(restProgress, restDistance);
    //    return map.PointFromCenterByArcLength(h, out var n, out _);
    //}

    //private float StepGoalHorizontalOffset(float restProgress, float restDistance/*, float stepMax*/)
    //{
    //    return stepMax - restProgress * restDistance;
    //}

    //private float StepStartHorizontalOffset(float stepProgress, float stepDistance, float restDistance/*, float stepMax*/)
    //{
    //    return stepMax - restDistance - stepProgress * stepDistance;
    //}
}