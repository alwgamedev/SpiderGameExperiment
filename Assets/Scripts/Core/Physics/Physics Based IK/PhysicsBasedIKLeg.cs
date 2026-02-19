using System;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public struct PhysicsLegSettings
{
    public float gravityStrength;
    public float poseForce;
    public float limpness;
    public float jointDamping;
    public float stepHeight;

    public static PhysicsLegSettings Lerp(PhysicsLegSettings s1, PhysicsLegSettings s2, float t)
    {
        return new PhysicsLegSettings
        {
            gravityStrength = Mathf.Lerp(s1.gravityStrength, s2.gravityStrength, t),
            poseForce = Mathf.Lerp(s1.poseForce, s2.poseForce, t),
            limpness = Mathf.Lerp(s1.limpness, s2.limpness, t),
            jointDamping = Mathf.Lerp(s1.jointDamping, s2.jointDamping, t),
            stepHeight = Mathf.Lerp(s1.stepHeight, s2.stepHeight, t)
        };
    }
}

[Serializable]
public class PhysicsBasedIKLeg
{
    public PhysicsLegSettings contactSettings;
    public PhysicsLegSettings noContactSettings;
    public bool useGroundMapClosestPoint;
    public UnityEvent HitGround;

    [SerializeField] Transform orientingTransform;
    [SerializeField] Transform[] chain;
    [SerializeField] Transform target;
    [SerializeField] float stepMax;

    Vector2[] defaultPose;
    Vector2[] positionBuffer;
    float[] angularVelocity;
    float[] length;
    float[] inverseLength;
    float totalLength;
    float totalLengthInverse;

    public bool EffectorIsTouchingGround { get; private set; }
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

        for (int i = 0; i < length.Length; i++)
        {
            Vector2 v = chain[i + 1].position - chain[i].position;
            var l = v.magnitude;
            length[i] = l;
            inverseLength[i] = 1 / l;
            totalLength += l;
            defaultPose[i] = new(Vector2.Dot(v, orientingTransform.right) / l, Vector2.Dot(v, orientingTransform.up) / l);
            positionBuffer[i] = chain[i].position;
        }
        positionBuffer[^1] = chain[^1].position;

        totalLengthInverse = 1 / totalLength;
        target.position = chain[^1].position;
    }

    public void UpdateJoints(GroundMap groundMap, int fabrikIterations, float fabrikTolerance,
        float groundContactRadius, float collisionResponse, float dt, float simulateContactWeight = 0)
    {
        var settings = EffectorIsTouchingGround ? contactSettings
            : simulateContactWeight > 0 ? PhysicsLegSettings.Lerp(noContactSettings, contactSettings, simulateContactWeight)
            : noContactSettings;
        if (settings.limpness > 0 && (chain[0].position.x != positionBuffer[0].x || chain[0].position.y != positionBuffer[0].y))
        {
            for (int i = 1; i < positionBuffer.Length; i++)
            {
                //var p = positionBuffer[i] + dt * settings.gravityStrength * Physics2D.gravity;
                var p = Vector2.Lerp(chain[i].position, positionBuffer[i], settings.limpness);
                Vector2 u = orientingTransform.localScale.x * (p - (Vector2)chain[i - 1].position).normalized;
                chain[i - 1].rotation = MathTools.QuaternionFrom2DUnitVector(u);
            }
        }
        PhysicsBasedIK.IntegrateJoints(chain, angularVelocity, settings.jointDamping, dt);

        SolveForTargetPose(target.position, fabrikIterations, fabrikTolerance);
        PushTowardsTargetPose(settings.poseForce, dt);
        if (!EffectorIsTouchingGround && settings.gravityStrength != 0)
        {
            PhysicsBasedIK.ApplyGravity(chain, inverseLength, angularVelocity, settings.gravityStrength, dt);
        }

        for (int i = 0; i < positionBuffer.Length; i++)
        {
            //put current positions into buffer (for limpness to use next update)
            positionBuffer[i] = chain[i].position;
        }

        UpdateGroundContact(groundMap, groundContactRadius, collisionResponse, dt);
    }

    public void UpdateTargetStepping(GroundMap map, float dt, float stepHeightSpeedMultiplier, float stepHeightFraction, float stepProgress, float stepDistance,
        float restDistance, float targetSmoothingRate)
    {
        ref var settings = ref EffectorIsTouchingGround ? ref contactSettings : ref noContactSettings;
        var stepHeight = settings.stepHeight;
        var bodyFacingRight = orientingTransform.localScale.x > 0;
        var stepStart = GetStepStart(map, bodyFacingRight, stepProgress, stepDistance, restDistance, out var h);
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

        target.position = Vector2.Lerp(target.position, newTargetPos, targetSmoothingRate * dt);
    }

    public void UpdateTargetResting(GroundMap map, float dt, float reachFraction, float restProgress, float restDistance, float targetSmoothingRate)
    {
        target.position = Vector2.Lerp(target.position, GetStepGoal(map, orientingTransform.localScale.x > 0, restProgress, restDistance, out _), targetSmoothingRate * dt);
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

    private void SolveForTargetPose(Vector2 targetPosition, int fabrikIterations, float fabrikTolerance)
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
            if (!FABRIKSolver.RunFABRIKIteration(positionBuffer, length, totalLength, targetPosition, fabrikTolerance))
            {
                break;
            }
        }
    }

    private void PushTowardsTargetPose(float accelFactor, float dt)
    {
        for (int i = 0; i < angularVelocity.Length; i++)
        {
            //Debug.DrawLine(positionBuffer[i], positionBuffer[i + 1], Color.red);
            var v = orientingTransform.localScale.x * inverseLength[i] * (positionBuffer[i + 1] - positionBuffer[i]);
            var q = MathTools.QuaternionFrom2DUnitVector(v);
            q *= MathTools.InverseOfUnitQuaternion(chain[i].rotation);
            if (q.w < 0)
            {
                q = new(-q.x, -q.y, -q.z, -q.w);
            }
            angularVelocity[i] += accelFactor * q.z * dt;//note q.z = sin(t/2) gives a nice angle function on (-pi,pi)
        }
    }

    private void UpdateGroundContact(GroundMap groundMap, float groundContactRadius, float collisionResponse, float dt)
    {
        var wasTouchingGround = EffectorIsTouchingGround;

        Vector2 q = chain[^1].position;
        var p = groundMap.TrueClosestPoint(q, out _, out var n, out var hitGround);
        var l = Vector2.Dot(q - p, n);
        EffectorIsTouchingGround = hitGround && l < groundContactRadius/*(Vector2.SqrMagnitude(d) < groundContactRadiusSqrd || Vector2.Dot(d, n) < 0)*/;
        if (EffectorIsTouchingGround)
        {
            if (l < 0)
            {
                var a = -collisionResponse * dt * l * n;
                PhysicsBasedIK.ApplyForceToJoint(chain, inverseLength, angularVelocity, a, chain.Length - 1);
            }

            if (!wasTouchingGround)
            {
                HitGround.Invoke();
            }
        }
    }

    private Vector2 GetStepStart(GroundMap map, bool bodyFacingRight, float stepProgress, float stepDistance, float restDistance, out float hipPosition)
    {
        if (useGroundMapClosestPoint)
        {
            map.TrueClosestPoint(chain[0].position, out hipPosition, out _, out _);
        }
        else
        {
            hipPosition = Vector2.Dot((Vector2)chain[0].position - map.LastOrigin, map.LastOriginRight);
        }

        return GetStepStart(map, hipPosition, bodyFacingRight, stepProgress, stepDistance, restDistance);
    }

    private Vector2 GetStepStart(GroundMap map, float h, bool bodyFacingRight, float stepProgress, float stepDistance, float restDistance)
    {
        h = bodyFacingRight ? h + StepStartHorizontalOffset(stepProgress, stepDistance, restDistance)
            : h - StepStartHorizontalOffset(stepProgress, stepDistance, restDistance);
        //return map.PointFromCenterByArcLength(h, out _, out _, reachFraction, true);
        return map.PointFromCenterByIntervalWidth(h);
    }

    private Vector2 GetStepGoal(GroundMap map, bool bodyFacingRight, float restProgress, float restDistance, out float hipPosition)
    {
        if (useGroundMapClosestPoint)
        {
            map.TrueClosestPoint(chain[0].position, out hipPosition, out _, out _);
        }
        else
        {
            hipPosition = Vector2.Dot((Vector2)chain[0].position - map.LastOrigin, map.LastOriginRight);
        }

        return GetStepGoal(map, hipPosition, bodyFacingRight, restProgress, restDistance);
        //return map.PointFromCenterByArcLength(h, out _, out _, reachFraction, true);
    }

    private Vector2 GetStepGoal(GroundMap map, float h, bool bodyFacingRight, float restProgress, float restDistance)
    {
        h = bodyFacingRight ? h + StepGoalHorizontalOffset(restProgress, restDistance)
            : h - StepGoalHorizontalOffset(restProgress, restDistance);
        return map.PointFromCenterByIntervalWidth(h);
    }

    private float StepGoalHorizontalOffset(float restProgress, float restDistance)
    {
        return stepMax - restProgress * restDistance;
    }

    private float StepStartHorizontalOffset(float stepProgress, float stepDistance, float restDistance)
    {
        return stepMax - restDistance - stepProgress * stepDistance;
    }
}