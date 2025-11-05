using UnityEngine;
using UnityEngine.U2D.IK;

public class LegAnimator : MonoBehaviour
{
    [SerializeField] float hipRaycastLength = 2;
    [SerializeField] float hipRaycastUpwardBuffer = 0.5f;
    [SerializeField] float stepMax;
    [SerializeField] float groundContactRadius;
    //[SerializeField] float preferredExtension;
    [SerializeField] Vector2 freeHangLagWeights;
    [SerializeField] Vector2 drift;
    
    int groundLayer;
    float groundContactRadius2;
    Transform hipBone;
    Transform ikEffector;
    Transform ikTarget;
    float ikChainLength;
    float ikChainLengthInverse;
    //float preferredExtensionPower;

    Vector2 lastFreeHangPosition;

    public float driftWeight;

    public Vector2 HipPosition => hipBone.position;
    public Vector2 FootPosition => ikEffector.position;
    public bool IsTouchingGround { get; private set; }
    public Vector2 ContactNormal { get; private set; }
    public Vector2 LegExtensionVector => FootPosition - HipPosition;
    public float LegExtensionFraction => LegExtensionVector.magnitude * ikChainLengthInverse;

    private void Awake()
    {
        groundLayer = LayerMask.GetMask("Ground");
        groundContactRadius2 = groundContactRadius * groundContactRadius;
        var ikChain = GetComponent<Solver2D>().GetChain(0);
        hipBone = ikChain.transforms[0];
        ikEffector = ikChain.effector;
        ikTarget = ikChain.target;

        var lengths = ikChain.lengths;
        ikChainLength = 0;
        for (int i = 0; i < lengths.Length; i++)
        {
            ikChainLength += lengths[i];
        }

        ikChainLengthInverse = 1 / ikChainLength;
        //preferredExtensionPower = (1 / preferredExtension) - 1;
    }

    //public void ApplyContactForce(Rigidbody2D rb, float forceScale)
    //{
    //    if (IsTouchingGround)
    //    {
    //        rb.AddForceAtPosition(rb.mass * forceScale * Vector2.Dot(LegExtensionVector, ContactNormal) * Mathf.Pow(1 - LegExtensionFraction, preferredExtensionPower) * LegExtensionVector,
    //            FootPosition);
    //    }
    //}

    public void InitializePosition(float bodyPosGroundHeight, Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp, 
        bool stepping, float stateProgress, float stepTime, float restTime)
    {
        if (stepping)
        {
            var stepStart = GetStepStart(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, stateProgress, stepTime, restTime);
            var stepGoal = GetStepGoal(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, 0, restTime);
            ikTarget.position = Vector2.Lerp(stepStart, stepGoal, stateProgress);
        }
        else
        {
            ikTarget.position = GetStepGoal(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, stateProgress, restTime);
        }

        var c = GroundRaycast(ikTarget.position, bodyUp, 1f, 1f);
        if (c)
        {
            ikTarget.position = c.point;
        }

        IsTouchingGround = Physics2D.OverlapCircle(ikTarget.position, groundContactRadius, groundLayer);
        //UpdateExtension();
    }

    public void UpdateStep(float dt, GroundMap map, bool bodyFacingRight, bool freeHanging,
        float baseStepHeightMultiplier, float stepHeightSpeedMultiplier,
        float smoothingRate, float stepProgress, float stepTime, float restTime)
    {
        var stepStart = GetStepStart(map, bodyFacingRight, stepProgress, stepTime, restTime);
        var stepGoal = GetStepGoal(map, bodyFacingRight, 0, restTime);
        //var stepRight = (stepGoal - stepStart).normalized;
        //var stepUp = bodyFacingRight ? stepRight.CCWPerp() : stepRight.CWPerp();
        //var stepCenter = 0.5f * (stepGoal + stepStart);
        //var stepRadius = Vector2.Dot(stepGoal - stepCenter, stepRight);
        //var t = Mathf.PI * stepProgress;
        var stepRight = stepGoal - stepStart;
        var stepUp = bodyFacingRight ? 0.5f * stepRight.CCWPerp() : 0.5f * stepRight.CWPerp();

        //to-do: parabola instead of trig fcts
        var newTargetPos = stepStart + stepProgress * stepRight + 4 * stepProgress * (1 - stepProgress) * baseStepHeightMultiplier * stepUp;
            //stepCenter - stepRadius * Mathf.Cos(t) * stepRight + stepRadius * baseStepHeightMultiplier * Mathf.Sin(t) * stepUp;

        if (stepHeightSpeedMultiplier < 1)
        {
            var g = map.ProjectOntoGroundByArcLength(newTargetPos, out var n, out _) + driftWeight * drift.y * n;
            newTargetPos = Vector2.Lerp(g, newTargetPos, stepHeightSpeedMultiplier);
        }

        if (freeHanging)
        {
            var s = smoothingRate * dt;
            lastFreeHangPosition = MathTools.Lerp(lastFreeHangPosition, ikTarget.position, s * freeHangLagWeights.x, s * freeHangLagWeights.y);
            ikTarget.position = Vector2.Lerp(lastFreeHangPosition, newTargetPos, s);
            lastFreeHangPosition = ikTarget.position;
        }
        else
        {
            ikTarget.position = Vector2.Lerp(ikTarget.position, newTargetPos, smoothingRate * dt);
        }

        UpdateIsTouchingGround(map);
        //UpdateExtension();
    }

    public void UpdateRest(float dt, GroundMap map, bool bodyFacingRight, bool freeHanging,
        float smoothingRate, float restProgress, float restTime)
    {
        var newTargetPos = GetStepGoal(map, bodyFacingRight, restProgress, restTime);
        if (freeHanging)
        {
            var s = smoothingRate * dt;
            lastFreeHangPosition = MathTools.Lerp(lastFreeHangPosition, ikTarget.position, s * freeHangLagWeights.x, s * freeHangLagWeights.y);
            ikTarget.position = Vector2.Lerp(lastFreeHangPosition, newTargetPos, s);
            lastFreeHangPosition = ikTarget.position;
        }
        else
        {
            ikTarget.position = Vector2.Lerp(ikTarget.position, newTargetPos, smoothingRate * dt);
        }

        UpdateIsTouchingGround(map);
        //UpdateExtension();
    }

    public void OnBodyChangedDirectionFreeHanging(Vector2 position0, Vector2 position1, Vector2 flipNormal)
    {
        var v = lastFreeHangPosition - position0;
        lastFreeHangPosition = position1 + MathTools.ReflectAcrossHyperplane(v, flipNormal);
    }

    public void CacheFreeHangPosition()
    {
        lastFreeHangPosition = ikTarget.position;
    }

    private void UpdateIsTouchingGround(GroundMap groundMap)
    {
        Vector2 q = ikEffector.position;
        q -= groundMap.ClosestPoint(q, out var n, out var hitGround);
        ContactNormal = n;
        IsTouchingGround = hitGround && (Vector2.SqrMagnitude(q) < groundContactRadius2 || Vector2.Dot(q, n) < 0);
    }

    //private void UpdateExtension()
    //{
    //    LegExtensionVector = new(ikTarget.position.x - hipBone.position.x, ikTarget.position.y - hipBone.position.y);
    //    LegExtensionFraction = LegExtensionVector.magnitude / ikChainLength;
    //}

    private Vector2 GetStepStart(GroundMap map, bool bodyFacingRight, float stepProgress, float stepTime, float restTime)
    {
        //var c = map.Center;
        var h = Vector2.Dot((Vector2)hipBone.position - map.LastOrigin/*c.point*/, map.LastOriginRight/*c.normal.CWPerp()*/);//we could also use body position and body right
        h = bodyFacingRight ? h + StepStartHorizontalOffset(stepProgress, stepTime, restTime)
            : h - StepStartHorizontalOffset(stepProgress, stepTime, restTime);
        return map.PointFromCenterByPosition(bodyFacingRight ? h + driftWeight * drift.x : h - driftWeight * drift.x, out var n, out _) + driftWeight * drift.y * n;
    }

    private Vector2 GetStepGoal(GroundMap map, bool bodyFacingRight, float restProgress, float restTime)
    {
        //var c = map.Center;
        var h = Vector2.Dot((Vector2)hipBone.position - map.LastOrigin/*c.point*/, map.LastOriginRight/*c.normal.CWPerp()*/);//we could also use body position and body right
        h = bodyFacingRight ? h + StepGoalHorizontalOffset(restProgress, restTime)
            : h - StepGoalHorizontalOffset(restProgress, restTime);
        return map.PointFromCenterByPosition(bodyFacingRight ? h + driftWeight * drift.x : h - driftWeight * drift.x, out var n, out _) + driftWeight * drift.y * n;
    }

    private Vector2 GetStepStart(float bodyPosGroundHeight, Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp, 
        float stepProgress, float stepTime, float restTime)
    {
        var r = StepStartRaycast(bodyMovementRight, bodyUp, stepProgress, stepTime, restTime);
        if (r)
        {
            return r.point;
        }
        return StaticStepStart(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, stepProgress, stepTime, restTime);
    }

    private Vector2 GetStepGoal(float bodyPosGroundHeight, Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp, 
        float restProgress, float restTime)
    {
        var r = StepGoalRaycast(bodyMovementRight, bodyUp, restProgress, restTime);
        if (r)
        {
            return r.point;
        }
        return StaticStepGoal(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, restProgress, restTime);
    }

    RaycastHit2D GroundRaycast(Vector2 origin, Vector2 bodyUp, float raycastLength, float upwardBuffer = .5f)
    {
        return Physics2D.Raycast(origin + upwardBuffer * bodyUp, -bodyUp, raycastLength + upwardBuffer, groundLayer);
    }

    RaycastHit2D StepPosRaycast(Vector2 searchRight, Vector2 searchUp, float horizontalOffset)
    {
        return GroundRaycast((Vector2)hipBone.position + horizontalOffset * searchRight, searchUp, hipRaycastLength, hipRaycastUpwardBuffer);
    }

    RaycastHit2D StepGoalRaycast(Vector2 bodyMovementRight, Vector2 bodyUp, float restProgress, float restTime)
    {
        return StepPosRaycast(bodyMovementRight, bodyUp, StepGoalHorizontalOffset(restProgress, restTime));
    }

    RaycastHit2D StepStartRaycast(Vector2 bodyMovementRight, Vector2 bodyUp, float stepProgress, float stepTime, float restTime)
    {
        return StepPosRaycast(bodyMovementRight, bodyUp, StepStartHorizontalOffset(stepProgress, stepTime, restTime));
    }

    Vector2 StaticStepPos(float bodyPosGroundHeight, Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp, float horizontalOffset)
    {
        Vector2 hipPos = hipBone.position;
        var h = bodyPosGroundHeight + Vector2.Dot(hipPos - bodyPos, bodyUp);
        return hipPos + horizontalOffset * bodyMovementRight - h * bodyUp;
    }

    Vector2 StaticStepGoal(float bodyPosGroundHeight, Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp, float restProgress, float restTime)
    {
        return StaticStepPos(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, StepGoalHorizontalOffset(restProgress, restTime));
    }

    Vector2 StaticStepStart(float bodyPosGroundHeight, Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp, 
        float stepProgress, float stepTime, float restTime)
    {
        return StaticStepPos(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, StepStartHorizontalOffset(stepProgress, stepTime, restTime));
    }

    float StepGoalHorizontalOffset(float restProgress, float restTime)
    {
        return stepMax - restProgress * restTime;
    }

    float StepStartHorizontalOffset(float stepProgress, float stepTime, float restTime)
    {
        return stepMax - restTime - stepProgress * stepTime;
    }
}
