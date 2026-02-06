using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public struct PhysicsLegSettings
{
    public float gravityStrength;
    public float poseStrength;
    public float limpness;
    public float jointDamping;
    public float stepHeight;
}

[Serializable]
public class PhysicsBasedIKLeg
{
    public PhysicsLegSettings settings;
    public UnityEvent HitGround;

    [SerializeField] Transform orientingTransform;
    [SerializeField] Transform[] chain;
    [SerializeField] Transform target;
    [SerializeField] float stepMax;

    Vector2[] defaultPose;
    Vector2[] positionBuffer;
    float[] angularVelocity;
    float[] length;
    float totalLength;
    float totalLengthInverse;

    public bool EffectorIsTouchingGround { get; private set; }
    public Vector2 LegExtensionVector => chain[^1].position - chain[0].position;
    public float LegExtensionFraction => LegExtensionVector.magnitude * totalLengthInverse;

    public void Initialize()
    {
        totalLength = 0f;
        length = new float[chain.Length - 1];
        angularVelocity = new float[chain.Length - 1];
        defaultPose = new Vector2[chain.Length - 1];
        positionBuffer = new Vector2[chain.Length];

        for (int i = 0; i < length.Length; i++)
        {
            Vector2 v = chain[i + 1].position - chain[i].position;
            var l = v.magnitude;
            length[i] = l;
            totalLength += l;
            defaultPose[i] = new(Vector2.Dot(v, orientingTransform.right) / l, Vector2.Dot(v, orientingTransform.up) / l);
            positionBuffer[i] = chain[i].position;
        }
        positionBuffer[^1] = chain[^1].position;

        totalLengthInverse = 1 / totalLength;
        target.position = chain[^1].position;
    }

    public void UpdateJoints(GroundMap groundMap, int fabrikIterations, float fabrikTolerance, float groundContactRadiusSqrd, float dt)
    {
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
        SolveForTargetPose(target.position, fabrikIterations, fabrikTolerance);
        if (settings.poseStrength != 0)
        { 
            PushTowardsTargetPose(settings.poseStrength, dt);
        }
        if (settings.gravityStrength != 0)
        {
            PhysicsBasedIK.ApplyGravity(chain, length, angularVelocity, settings.gravityStrength, dt);
        }

        for (int i = 0; i < positionBuffer.Length; i++)
        {
            //put current positions into buffer (for limpness to use next update)
            positionBuffer[i] = chain[i].position;
        }

        UpdateIsTouchingGround(groundMap, groundContactRadiusSqrd);
    }

    public void UpdateTargetStepping(GroundMap map, float stepHeightSpeedMultiplier, float stepHeightFraction, float stepProgress, float stepDistance, float restDistance)
    {
        var bodyFacingRight = orientingTransform.localScale.x > 0;
        var stepStart = GetStepStart(map, bodyFacingRight, stepProgress, stepDistance, restDistance);
        var stepGoal = GetStepGoal(map, bodyFacingRight, 0, restDistance);
        var stepRight = stepGoal - stepStart;
        var stepUp = bodyFacingRight ? 0.5f * stepRight.CCWPerp() : 0.5f * stepRight.CWPerp();

        //parabola instead of trig fcts
        var newTargetPos = stepStart + stepProgress * stepRight + 4 * stepProgress * (1 - stepProgress) * stepHeightFraction * settings.stepHeight * stepUp;

        if (stepHeightSpeedMultiplier < 1)
        {
            var g = map.ProjectOntoGroundByArcLength(newTargetPos, out _, out _);
            newTargetPos = Vector2.Lerp(g, newTargetPos, stepHeightSpeedMultiplier);
        }

        target.position = newTargetPos;//Vector2.Lerp(target.position, newTargetPos, smoothingRate * dt);
    }

    public void UpdateTargetResting(GroundMap map, float restProgress, float restDistance)
    {
        target.position = GetStepGoal(map, orientingTransform.localScale.x > 0, restProgress, restDistance);
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
        for (int i = 0; i < positionBuffer.Length - 1; i++)
        {
            Debug.DrawLine(positionBuffer[i], positionBuffer[i + 1], Color.red);
        }
        for (int i = 0; i < angularVelocity.Length; i++)
        {
            var v = orientingTransform.localScale.x * (positionBuffer[i + 1] - positionBuffer[i]) / length[i];
            var q = MathTools.QuaternionFrom2DUnitVector(v);
            q *= MathTools.InverseOfUnitQuaternion(chain[i].rotation);
            if (q.w < 0)
            {
                q = new(-q.x, -q.y, -q.z, -q.w);
            }
            angularVelocity[i] += accelFactor * q.z * dt;//note q.z = sin(t/2) gives a nice angle function on (-pi,pi)
        }
    }

    private void UpdateIsTouchingGround(GroundMap groundMap, float groundContactRadiusSqrd)
    {
        var wasTouchingGround = EffectorIsTouchingGround;

        Vector2 q = chain[^1].position;
        q -= groundMap.ClosestPoint(q, out var n, out var hitGround);
        EffectorIsTouchingGround = hitGround && (Vector2.SqrMagnitude(q) < groundContactRadiusSqrd || Vector2.Dot(q, n) < 0);

        if (!wasTouchingGround && EffectorIsTouchingGround)
        {
            HitGround.Invoke();
        }
    }

    private Vector2 GetStepStart(GroundMap map, bool bodyFacingRight, float stepProgress, float stepDistance, float restDistance)
    {
        var h = Vector2.Dot((Vector2)chain[0].position - map.LastOrigin, map.LastOriginRight);//we could also use body position and body right
        h = bodyFacingRight ? h + StepStartHorizontalOffset(stepProgress, stepDistance, restDistance)
            : h - StepStartHorizontalOffset(stepProgress, stepDistance, restDistance);
        return map.PointFromCenterByPosition(h, out _, out _);
    }

    private Vector2 GetStepGoal(GroundMap map, bool bodyFacingRight, float restProgress, float restDistance)
    {
        var h = Vector2.Dot((Vector2)chain[0].position - map.LastOrigin, map.LastOriginRight);//we could also use body position and body right
        h = bodyFacingRight ? h + StepGoalHorizontalOffset(restProgress, restDistance)
            : h - StepGoalHorizontalOffset(restProgress, restDistance);
        return map.PointFromCenterByPosition(h, out _, out _);
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