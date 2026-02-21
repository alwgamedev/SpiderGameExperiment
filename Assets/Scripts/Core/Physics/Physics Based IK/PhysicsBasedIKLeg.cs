using System;
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
    public bool nonSimulatedPose;

    public static PhysicsLegSettings Lerp(PhysicsLegSettings s1, PhysicsLegSettings s2, float t)
    {
        return new PhysicsLegSettings
        {
            gravityStrength = Mathf.Lerp(s1.gravityStrength, s2.gravityStrength, t),
            poseForce = Mathf.Lerp(s1.poseForce, s2.poseForce, t),
            limpness = Mathf.Lerp(s1.limpness, s2.limpness, t),
            jointDamping = Mathf.Lerp(s1.jointDamping, s2.jointDamping, t),
            stepHeight = Mathf.Lerp(s1.stepHeight, s2.stepHeight, t),
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
    [SerializeField] float defaultPoseDeviationMax;

    Vector2[] defaultPose;
    Vector2[] positionBuffer;
    float[] angularVelocity;
    float[] minAngle;//really should be sin(t/2) (the z-coordinate of the quaternion with q.w > 0)
    float[] maxAngle;
    float[] length;
    float[] inverseLength;
    float totalLength;
    float totalLengthInverse;
    float smoothedHipPosition;

    public bool EffectorIsTouchingGround { get; private set; }
    public Vector2 EffectorPosition => chain[^1].position;
    public Vector2 LegExtensionVector => chain[^1].position - chain[0].position;
    public float LegExtensionFraction => LegExtensionVector.magnitude * totalLengthInverse;

    public void Initialize(Vector2 groundPosition, Vector2 groundNormal)
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

        minAngle = new float[chain.Length - 1];
        maxAngle = new float[chain.Length - 1];
        Quaternion q0 = chain[0].rotation;
        for (int i = 1; i < minAngle.Length; i++)
        {
            Quaternion q1 = chain[i].rotation;
            q0 = q1 * MathTools.InverseOfUnitQuaternion(q0);
            var t = Mathf.Sign(sign * q0.w) * q0.z;
            t = 2 * Mathf.Asin(t);
            minAngle[i] = Mathf.Sin(0.5f * Mathf.Clamp(t - defaultPoseDeviationMax, -Mathf.PI, Mathf.PI));
            maxAngle[i] = Mathf.Sin(0.5f * Mathf.Clamp(t + defaultPoseDeviationMax, -Mathf.PI, Mathf.PI));

            q0 = q1;
        }

        smoothedHipPosition = sign * Vector2.Dot((Vector2)chain[0].position - groundPosition, groundNormal.CWPerp());
    }

    public Vector2 UpdateJoints(GroundMap groundMap, int fabrikIterations, float fabrikTolerance,
        float groundContactRadius, float collisionResponse, float angleBoundsForce, float dt, float simulateContactWeight = 0)
    {
        var settings = EffectorIsTouchingGround ? contactSettings
            : simulateContactWeight > 0 ? PhysicsLegSettings.Lerp(noContactSettings, contactSettings, simulateContactWeight)
            : noContactSettings;
        if (settings.limpness > 0 && (chain[0].position.x != positionBuffer[0].x || chain[0].position.y != positionBuffer[0].y))
        {
            for (int i = 1; i < positionBuffer.Length; i++)
            {
                var p = Vector2.Lerp(chain[i].position, positionBuffer[i], settings.limpness);
                Vector2 u = orientingTransform.localScale.x * (p - (Vector2)chain[i - 1].position).normalized;
                chain[i - 1].rotation = MathTools.QuaternionFrom2DUnitVector(u);
            }
        }

        PhysicsBasedIK.IntegrateJoints(chain, angularVelocity, settings.jointDamping, dt);

        if (settings.poseForce != 0)
        {
            SolveForTargetPose(target.position, fabrikIterations, fabrikTolerance);
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
            PushTowardsTarget(target.position, settings.reachForce, dt);
        }

        if (!EffectorIsTouchingGround && settings.gravityStrength != 0)
        {
            PhysicsBasedIK.ApplyGravity(chain, inverseLength, angularVelocity, settings.gravityStrength, dt);
        }

        PushAwayFromAngleBounds(dt, angleBoundsForce);

        for (int i = 0; i < positionBuffer.Length; i++)
        {
            //put current positions into buffer (for limpness to use next update)
            positionBuffer[i] = chain[i].position;
        }

        return UpdateGroundContact(groundMap, groundContactRadius, collisionResponse, dt, !settings.nonSimulatedPose);
    }

    public void UpdateTargetStepping(GroundMap map, Vector2 castDir,
        float stepHeightSpeedMultiplier, float stepHeightFraction, float stepProgress, float stepDistance,
        float restDistance)
    {
        ref var settings = ref EffectorIsTouchingGround ? ref contactSettings : ref noContactSettings;
        var stepHeight = settings.stepHeight;
        var bodyFacingRight = orientingTransform.localScale.x > 0;

        var dot = Vector2.Dot((Vector2)chain[0].position - map.LastOrigin, map.LastOriginRight);
        if (!map.LineCastToGround(chain[0].position, castDir, out var p, out var h, out _, out _) || Mathf.Abs(h) < Mathf.Abs(dot))
        {
            h = dot;
        }

        var stepStart = GetStepStart(map, h, bodyFacingRight, stepProgress, stepDistance, restDistance/*, stepMax*/);
        var stepGoal = GetStepGoal(map, h, bodyFacingRight, 0, restDistance/*, stepMax*/);
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
        var dot = Vector2.Dot((Vector2)chain[0].position - map.LastOrigin, map.LastOriginRight);
        if (!map.LineCastToGround(chain[0].position, castDir, out var p, out var h, out _, out _) || Mathf.Abs(h) < Mathf.Abs(dot))
        {
            h = dot;
        }

        target.position = GetStepGoal(map, h, orientingTransform.localScale.x > 0, restProgress, restDistance/*, stepMax*/);
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

    private void PushTowardsTarget(Vector2 targetPosition, float accelFactor, float dt)
    {
        var d = targetPosition - (Vector2)chain[^1].position;
        PhysicsBasedIK.ApplyForceDownChain(chain, inverseLength, angularVelocity, dt * accelFactor * d, 1, null, true);
    }

    private void PushTowardsTargetPose(float accelFactor, float dt)
    {
        accelFactor *= dt;
        for (int i = 0; i < angularVelocity.Length; i++)
        {
            //Debug.DrawLine(positionBuffer[i], positionBuffer[i + 1], Color.red);
            var v = orientingTransform.localScale.x * inverseLength[i] * (positionBuffer[i + 1] - positionBuffer[i]);
            var q = MathTools.QuaternionFrom2DUnitVector(v);
            q *= MathTools.InverseOfUnitQuaternion(chain[i].rotation);
            angularVelocity[i] += accelFactor * Mathf.Sin(q.w) * q.z;//note q.z = sin(t/2) gives a nice angle function on (-pi,pi)
        }
    }

    private Vector2 UpdateGroundContact(GroundMap groundMap, float groundContactRadius, float collisionResponse, float dt, bool applyContactForces)
    {
        var wasTouchingGround = EffectorIsTouchingGround;
        Vector2 a = Vector2.zero;
        Vector2 q = chain[^1].position;
        q -= groundMap.TrueClosestPoint(q, out _, out var n, out var hitGround);

        var l = Vector2.Dot(q, n);
        EffectorIsTouchingGround = hitGround && l < groundContactRadius;
        if (EffectorIsTouchingGround)
        {
            if (applyContactForces && l < 0)
            {
                a = -collisionResponse * l * n;
                PhysicsBasedIK.ApplyForceUpChain(chain, inverseLength, angularVelocity, dt * a, chain.Length - 1);
            }

            if (!wasTouchingGround)
            {
                HitGround.Invoke();
            }
        }

        return a;
    }

    private Vector2 GetStepStart(GroundMap map, bool bodyFacingRight, float stepProgress, float stepDistance, float restDistance/*, float stepMax*/)
    {
        var h = Vector2.Dot((Vector2)chain[0].position - map.LastOrigin, map.LastOriginRight);
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
        var h = Vector2.Dot((Vector2)chain[0].position - map.LastOrigin, map.LastOriginRight);
        h = bodyFacingRight ? h + StepGoalHorizontalOffset(restProgress, restDistance/*, stepMax*/)
            : h - StepGoalHorizontalOffset(restProgress, restDistance/*, stepMax*/);
        return map.PointFromCenterByArcLength(h, out _, out _);
        //return map.PointFromCenterByIntervalWidth(h);
    }

    private Vector2 GetStepGoal(GroundMap map, float h, bool bodyFacingRight, float restProgress, float restDistance/*, float stepMax*/)
    {
        h = bodyFacingRight ? h + StepGoalHorizontalOffset(restProgress, restDistance/*, stepMax*/)
            : h - StepGoalHorizontalOffset(restProgress, restDistance/*, stepMax*/);
        return map.PointFromCenterByArcLength(h, out _, out _);
        //return map.PointFromCenterByIntervalWidth(h);
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
        Quaternion q0 = chain[0].rotation;
        var sign = Mathf.Sign(orientingTransform.localScale.x);
        for (int i = 1; i < minAngle.Length; i++)
        {
            Quaternion q1 = chain[i].rotation;
            q0 = q1 * MathTools.InverseOfUnitQuaternion(q0);
            var z = Mathf.Sign(sign * q0.w) * q0.z;

            if (z < minAngle[i])
            {
                angularVelocity[i] += sign * accel * (minAngle[i] - z);
            }
            else if (z > maxAngle[i])
            {
                angularVelocity[i] -= sign * accel * (z - maxAngle[i]);
            }

            q0 = q1;
        }
    }
}