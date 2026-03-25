using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public struct PhysicsLegSettings
{
    public float gravityStrength;
    public float poseForce;
    public float reachForce;
    public float limpness;
    public float jointDamping;
    public float stepHeight;
    public bool enforceAngleBounds;
    public bool nonSimulatedPose;

    public static PhysicsLegSettings Lerp(PhysicsLegSettings s1, PhysicsLegSettings s2, float t)
    {
        return new PhysicsLegSettings
        {
            gravityStrength = Mathf.Lerp(s1.gravityStrength, s2.gravityStrength, t),
            poseForce = Mathf.Lerp(s1.poseForce, s2.poseForce, t),
            reachForce = Mathf.Lerp(s1.reachForce, s2.reachForce, t),
            limpness = Mathf.Lerp(s1.limpness, s2.limpness, t),
            jointDamping = Mathf.Lerp(s1.jointDamping, s2.jointDamping, t),
            stepHeight = Mathf.Lerp(s1.stepHeight, s2.stepHeight, t),
            enforceAngleBounds = s1.enforceAngleBounds,
            nonSimulatedPose = s1.nonSimulatedPose
        };
    }
}

[Serializable]
public class PhysicsBasedIKLeg
{
    public PhysicsLegSettings contactSettings;
    public PhysicsLegSettings noContactSettings;
    public UnityEvent HitGround;

    [SerializeField] Transform orientingTransform;
    [SerializeField] Transform[] chain;
    [SerializeField] Transform target;
    [SerializeField] float stepMax;
    [SerializeField] float[] minAngle;
    [SerializeField] float[] maxAngle;
    [SerializeField] bool[] angleBranch;
    //angle branch:
    //false = (-pi, pi), true = (0, 2pi)
    //for (-pi,pi) branch, the angle bounds are bounds for the z-coordinate of the local rotation (sin(t/2), and we need to assume q.w > 0)
    //for (0,2pi) branch, the angle bounds are bounds for the w-coordinate of the local rotation (cos(t/2), and we need to assume q.z > 0)

    Vector2[] defaultPose;
    Vector2[] positionBuffer;
    float[] angularVelocity;
    float[] length;
    float[] inverseLength;
    float totalLength;
    float totalLengthInverse;

    public bool EffectorIsTouchingGround { get; private set; }
    public Vector2 EffectorPosition => chain[^1].position;
    public Vector2 LegExtensionVector => chain[^1].position - chain[0].position;
    public float LegExtensionFraction => LegExtensionVector.magnitude * totalLengthInverse;

    public void Initialize()
    {
        totalLength = 0f;
        length = new float[chain.Length - 1];
        inverseLength = new float[chain.Length - 1];
        angularVelocity = new float[chain.Length - 1];
        defaultPose = new Vector2[chain.Length - 1];
        positionBuffer = new Vector2[chain.Length];

        var sign = orientingTransform.localScale.x;

        for (int i = 0; i < length.Length; i++)
        {
            Vector2 v = chain[i + 1].position - chain[i].position;
            var l = v.magnitude;
            length[i] = l;
            inverseLength[i] = 1 / l;
            totalLength += l;
            defaultPose[i] = new(Vector2.Dot(v, sign * orientingTransform.right) / l, Vector2.Dot(v, orientingTransform.up) / l);
            positionBuffer[i] = chain[i].position;
        }
        positionBuffer[^1] = chain[^1].position;

        totalLengthInverse = 1 / totalLength;
        target.position = chain[^1].position;
    }

    public void UpdateJoints(GroundMap groundMap, float dt, int fabrikIterations, float fabrikToleranceSqrd, float reachToleranceSqrd,
        float groundContactRadius, float collisionResponse, /*float maxAngularVelocity,*/ float simulateContactWeight)
    {
        var settings = EffectorIsTouchingGround ? contactSettings
            : simulateContactWeight > 0 ? PhysicsLegSettings.Lerp(noContactSettings, contactSettings, simulateContactWeight)
            : noContactSettings;

        PhysicsBasedIK.IntegrateJoints(chain, angularVelocity, settings.jointDamping, dt);

        if (settings.limpness != 0)
        {
            Limp(settings.limpness, dt);
        }

        UpdateGroundContact(groundMap, groundContactRadius, collisionResponse, dt, !settings.nonSimulatedPose);

        if (!EffectorIsTouchingGround && settings.gravityStrength != 0)
        {
            PhysicsBasedIK.ApplyGravity(chain, inverseLength, angularVelocity, settings.gravityStrength, dt);
        }

        if (settings.poseForce != 0)
        {
            SolveForTargetPose(target.position, fabrikIterations, fabrikToleranceSqrd);
            if (settings.nonSimulatedPose)
            {
                LerpTowardsTargetPose(settings.poseForce, dt);
            }
            else
            {
                PushTowardsTargetPose(settings.poseForce, dt);
            }
        }

        if (settings.reachForce != 0)
        {
            PhysicsBasedIK.ReachForTargetWithAngleBounds(chain, length, inverseLength, angularVelocity, minAngle, maxAngle, angleBranch, orientingTransform.localScale.x,
                    target.position, settings.reachForce, reachToleranceSqrd, dt);
        }

        //if (settings.enforceAngleBounds)
        //{
        //    EnforceAngleBounds(dt, maxAngularVelocity);
        //}
        //else
        //{
        //    EnforceAngleBranch(dt, maxAngularVelocity);
        //}

        for (int i = 0; i < positionBuffer.Length; i++)
        {
            //put current positions into buffer (for limpness to use next update)
            positionBuffer[i] = chain[i].position;
        }
    }

    public void UpdateTargetStepping(GroundMap map, Vector2 castDir,
        float stepHeightSpeedMultiplier, float stepHeightFraction, float stepProgress, float stepDistance,
        float restDistance)
    {
        ref var settings = ref EffectorIsTouchingGround ? ref contactSettings : ref noContactSettings;
        var stepHeight = settings.stepHeight;
        var bodyFacingRight = orientingTransform.localScale.x > 0;

        var dot = Vector2.Dot((Vector2)chain[0].position - (Vector2)map.Origin, map.OriginRight);
        float h;
        if (!map.LineCastToGround((Vector2)chain[0].position, castDir, out var p) || Mathf.Abs(p.arcLengthPosition) < Mathf.Abs(dot))
        {
            h = dot;
        }
        else
        {
            h = p.arcLengthPosition;
        }

        var stepStart = GetStepStart(map, h, bodyFacingRight, stepProgress, stepDistance, restDistance/*, stepMax*/);
        var stepGoal = GetStepGoal(map, h, bodyFacingRight, 0, restDistance);
        var stepRight = stepGoal - stepStart;
        var stepUp = bodyFacingRight ? 0.5f * stepRight.CCWPerp() : 0.5f * stepRight.CWPerp();

        //parabola instead of trig fcts
        var newTargetPos = stepStart + stepProgress * stepRight + 4 * stepProgress * (1 - stepProgress) * stepHeightFraction * stepHeight * stepUp;

        if (stepHeightSpeedMultiplier < 1)
        {
            var g = map.ProjectOntoGroundByArcLength(newTargetPos, out _, out _);
            newTargetPos = Vector2.Lerp(g, newTargetPos, stepHeightSpeedMultiplier);
        }

        target.position = newTargetPos;
    }

    public void UpdateTargetResting(GroundMap map, Vector2 castDir,
        float restProgress, float restDistance)
    {
        var dot = Vector2.Dot((Vector2)chain[0].position - (Vector2)map.Origin, map.OriginRight);

        float h;
        if (!map.LineCastToGround((Vector2)chain[0].position, castDir, out var p) || Mathf.Abs(p.arcLengthPosition) < Mathf.Abs(dot))
        {
            h = dot;
        }
        else
        {
            h = p.arcLengthPosition;
        }

        target.position = GetStepGoal(map, h, orientingTransform.localScale.x > 0, restProgress, restDistance);
    }

    public void ClampTargetPosition(GroundMap map, float maxExtensionFraction)
    {
        var d = target.position - chain[0].position;
        var f = d.sqrMagnitude * totalLengthInverse * totalLengthInverse;
        if (f > maxExtensionFraction * maxExtensionFraction)
        {
            var p = chain[0].position + maxExtensionFraction / Mathf.Sqrt(f) * d;
            target.position = (Vector2)map.TrueClosestPoint(new float2(p.x, p.y), out _, out _, out _);
        }
    }

    public void OnBodyChangedDirection(Vector2 position0, Vector2 position1, Vector2 flipNormal)
    {
        for (int i = 0; i < angularVelocity.Length; i++)
        {
            angularVelocity[i] = -angularVelocity[i];
            var v = positionBuffer[i] - position0;
            positionBuffer[i] = position1 + MathTools.ReflectAcrossHyperplane(v, flipNormal);
        }

        var w = positionBuffer[^1] - position0;
        positionBuffer[^1] = position1 + MathTools.ReflectAcrossHyperplane(w, flipNormal);
    }

    private void SolveForTargetPose(Vector2 targetPosition, int fabrikIterations, float fabrikToleranceSqrd)
    {
        Vector2 right = orientingTransform.localScale.x * orientingTransform.right;//localScale should only be +/- 1 for this to work
        Vector2 up = orientingTransform.localScale.y * orientingTransform.up;
        positionBuffer[0] = chain[0].position;
        for (int i = 1; i < positionBuffer.Length; i++)
        {
            positionBuffer[i] = positionBuffer[i - 1] + length[i - 1] * (defaultPose[i - 1].x * right + defaultPose[i - 1].y * up);
        }

        for (int i = 0; i < fabrikIterations; i++)
        {
            if (!FABRIKSolver.RunFABRIKIteration(positionBuffer, length, totalLength, targetPosition, fabrikToleranceSqrd))
            {
                break;
            }
        }
    }

    //non-simulated version
    private void LerpTowardsTargetPose(float lerpRate, float dt)
    {
        for (int i = 0; i < angularVelocity.Length; i++)
        {
            //Debug.DrawLine(positionBuffer[i], positionBuffer[i + 1], Color.red);
            var v = orientingTransform.localScale.x * inverseLength[i] * (positionBuffer[i + 1] - positionBuffer[i]);
            chain[i].ApplyCheapRotationalLerp(v, lerpRate * dt, out _);
        }
    }

    private void PushTowardsTargetPose(float accelFactor, float dt)
    {
        accelFactor *= dt;
        for (int i = 0; i < angularVelocity.Length; i++)
        {
            var v = orientingTransform.localScale.x * inverseLength[i] * (positionBuffer[i + 1] - positionBuffer[i]);
            var q = MathTools.QuaternionFrom2DUnitVector(v);
            q *= MathTools.InverseOfUnitQuaternion(chain[i].rotation);
            angularVelocity[i] += accelFactor * Mathf.Sin(q.w) * q.z;//note q.z = sin(t/2) gives a nice angle function on (-pi,pi)
        }
    }

    private void Limp(float limpness, float dt)
    {
        limpness *= dt;
        for (int i = 1; i < positionBuffer.Length; i++)
        {
            chain[i - 1].ApplyCheapRotationalLerp(orientingTransform.localScale.x * (positionBuffer[i] - (Vector2)chain[i - 1].position).normalized, limpness, out _);
        }
    }

    private void UpdateGroundContact(GroundMap groundMap, float groundContactRadius, float collisionResponse, float dt, bool applyContactForces)
    {
        var wasTouchingGround = EffectorIsTouchingGround;
        Vector2 q = chain[^1].position;
        q -= (Vector2)groundMap.TrueClosestPoint(q, out _, out var n, out var hitGround);

        var l = Vector2.Dot(q, n);
        EffectorIsTouchingGround = hitGround && l < groundContactRadius;
        if (EffectorIsTouchingGround)
        {
            if (applyContactForces && l < 0)
            {
                var a = -dt * collisionResponse * l * n;
                PhysicsBasedIK.ApplyForceUpChain(chain, inverseLength, angularVelocity, a, chain.Length - 1, null, true);
            }

            if (!wasTouchingGround)
            {
                HitGround.Invoke();
            }
        }
    }

    private Vector2 GetStepStart(GroundMap map, bool bodyFacingRight, float stepProgress, float stepDistance, float restDistance/*, float stepMax*/)
    {
        var h = Vector2.Dot((Vector2)chain[0].position - (Vector2)map.Origin, map.OriginRight);
        h = bodyFacingRight ? h + StepStartHorizontalOffset(stepProgress, stepDistance, restDistance/*, stepMax*/)
            : h - StepStartHorizontalOffset(stepProgress, stepDistance, restDistance/*, stepMax*/);
        return map.PointFromCenterByArcLength(h, out _, out _);
        //return map.PointFromCenterByIntervalWidth(h);
    }

    private Vector2 GetStepStart(GroundMap map, float h, bool bodyFacingRight, float stepProgress, float stepDistance, float restDistance/*, float stepMax*/)
    {
        h = bodyFacingRight ? h + StepStartHorizontalOffset(stepProgress, stepDistance, restDistance/*, stepMax*/)
            : h - StepStartHorizontalOffset(stepProgress, stepDistance, restDistance/*, stepMax*/);
        return map.PointFromCenterByArcLength(h, out _, out _);
        //return map.PointFromCenterByIntervalWidth(h);
    }

    private Vector2 GetStepGoal(GroundMap map, bool bodyFacingRight, float restProgress, float restDistance/*, float stepMax*/)
    {
        var h = Vector2.Dot((Vector2)chain[0].position - (Vector2)map.Origin, map.OriginRight);
        h = bodyFacingRight ? h + StepGoalHorizontalOffset(restProgress, restDistance/*, stepMax*/)
            : h - StepGoalHorizontalOffset(restProgress, restDistance/*, stepMax*/);
        return map.PointFromCenterByArcLength(h, out _, out _);
        //return map.PointFromCenterByIntervalWidth(h);
    }

    private Vector2 GetStepGoal(GroundMap map, float h, bool bodyFacingRight, float restProgress, float restDistance)
    {
        h += bodyFacingRight ? StepGoalHorizontalOffset(restProgress, restDistance) : -StepGoalHorizontalOffset(restProgress, restDistance);
        return map.PointFromCenterByArcLength(h, out var n, out _);
    }

    private float StepGoalHorizontalOffset(float restProgress, float restDistance/*, float stepMax*/)
    {
        return stepMax - restProgress * restDistance;
    }

    private float StepStartHorizontalOffset(float stepProgress, float stepDistance, float restDistance/*, float stepMax*/)
    {
        return stepMax - restDistance - stepProgress * stepDistance;
    }

    private void PushAwayFromAngleBounds(float dt, float accel)
    {
        accel *= dt;
        var sign = Mathf.Sign(orientingTransform.localScale.x);
        for (int i = 1; i < minAngle.Length; i++)
        {
            var z = Mathf.Sign(chain[i].localRotation.w) * chain[i].localRotation.z;

            if (z < minAngle[i])
            {
                angularVelocity[i] += sign * accel * (minAngle[i] - z);
            }
            else if (z > maxAngle[i])
            {
                angularVelocity[i] -= sign * accel * (z - maxAngle[i]);
            }
        }
    }

    private void EnforceAngleBounds(float dt, float maxAngularVelocity)
    {
        var sign = Mathf.Sign(orientingTransform.localScale.x);
        var c0 = 0.5f * sign * dt;

        for (int i = 0; i < minAngle.Length; i++)
        {
            var z0 = angleBranch[i] ? Mathf.Sign(chain[i].localRotation.z) * chain[i].localRotation.w : Mathf.Sign(chain[i].localRotation.w) * chain[i].localRotation.z;
            var c = angleBranch[i] ? -c0 * Mathf.Abs(chain[i].localRotation.w) : c0 * Mathf.Abs(chain[i].localRotation.z);
            if (c == 0)
            {
                continue;
            }
            var prevV = i > 0 ? angularVelocity[i - 1] : 0;
            var z1 = z0 + c * (angularVelocity[i] - prevV);
            z1 = Mathf.Clamp(z1, minAngle[i], maxAngle[i]);
            angularVelocity[i] = Mathf.Clamp((z1 - z0) / c + prevV, -maxAngularVelocity, maxAngularVelocity);
        }
    }

    private void EnforceAngleBranch(float dt, float maxAngularVelocity)
    {
        var sign = Mathf.Sign(orientingTransform.localScale.x);
        var c0 = 0.5f * sign * dt;
        //var max = 1 - Mathf.Epsilon;
        //var min = -max;

        for (int i = 0; i < minAngle.Length; i++)
        {
            var z0 = angleBranch[i] ? Mathf.Sign(chain[i].localRotation.z) * chain[i].localRotation.w : Mathf.Sign(chain[i].localRotation.w) * chain[i].localRotation.z;
            var c = angleBranch[i] ? -c0 * Mathf.Abs(chain[i].localRotation.w) : c0 * Mathf.Abs(chain[i].localRotation.z);
            if (c == 0)
            {
                continue;
            }
            var prevV = i > 0 ? angularVelocity[i - 1] : 0;
            var z1 = z0 + c * (angularVelocity[i] - prevV);
            z1 = Mathf.Clamp(z1, -1, 1);
            angularVelocity[i] = Mathf.Clamp((z1 - z0) / c + prevV, -maxAngularVelocity, maxAngularVelocity);
        }
    }
}