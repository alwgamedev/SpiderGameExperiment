using UnityEngine;
using UnityEngine.U2D.IK;

public class LegAnimator : MonoBehaviour
{
    [SerializeField] float hipRaycastLength = 2;
    [SerializeField] float hipRaycastUpwardBuffer = 0.5f;
    [SerializeField] float staticModeGroundDetectionRadius;
    [SerializeField] float stepMax;
    [SerializeField] float freeHangExtensionFraction;
    [SerializeField] float freeHangRotationMin;//angle in degrees wrt frame (body.down, body.OrientedRight)
    [SerializeField] float freeHangRotationMax;

    public Vector2 drift;
    

    int groundLayer;
    Transform hipBone;
    Transform ikTarget;
    float ikChainLength;

    Vector2 freeHangDirectionMin;
    Vector2 freeHangDirectionMax;

    float FreeHangLength => freeHangExtensionFraction * ikChainLength;//will cache once we're done playing with freeHangExtensionFraction

    private void Awake()
    {
        groundLayer = LayerMask.GetMask("Ground");
        var ikChain = GetComponent<Solver2D>().GetChain(0);
        hipBone = ikChain.transforms[0];
        ikTarget = ikChain.target;
        ikChainLength = 0;
        var lengths = ikChain.lengths;//because it rebuilds the lengths array every time you ask for it...
        for (int i = 0; i < lengths.Length; i++)
        {
            ikChainLength += lengths[i];
        }

        freeHangDirectionMin = new(Mathf.Cos(Mathf.Deg2Rad * freeHangRotationMin), Mathf.Sin(Mathf.Deg2Rad * freeHangRotationMin));
        freeHangDirectionMax = new(Mathf.Cos(Mathf.Deg2Rad * freeHangRotationMax), Mathf.Sin(Mathf.Deg2Rad * freeHangRotationMax));
    }

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
    }

    public void UpdateStep(float dt, GroundMap map, bool bodyFacingRight,
        float baseStepHeightMultiplier, float stepHeightSpeedMultiplier,
        float smoothingRate, float stepProgress, float stepTime, float restTime, Vector2 driftWeight)
    {
        var drift = driftWeight * this.drift;
        var stepStart = GetStepStart(map, bodyFacingRight, stepProgress, stepTime, restTime, drift);
        var stepGoal = GetStepGoal(map, bodyFacingRight, 0, restTime, drift);
        var stepRight = (stepGoal - stepStart).normalized;
        var stepUp = bodyFacingRight ? stepRight.CCWPerp() : stepRight.CWPerp();
        var stepCenter = 0.5f * (stepGoal + stepStart);
        var stepRadius = Vector2.Dot(stepGoal - stepCenter, stepRight);
        var t = Mathf.PI * stepProgress;

        //to-do: parabola instead of trig fcts
        var newTargetPos = stepCenter - stepRadius * Mathf.Cos(t) * stepRight + stepRadius * baseStepHeightMultiplier * Mathf.Sin(t) * stepUp;

        if (stepHeightSpeedMultiplier < 1)
        {
            var g = map.ProjectOntoGround(newTargetPos, out var n) + drift.y * n;
            newTargetPos = Vector2.Lerp(g, newTargetPos, stepHeightSpeedMultiplier);
        }

        ikTarget.position = Vector2.Lerp(ikTarget.position, newTargetPos, smoothingRate * dt);
    }

    public void UpdateRest(float dt, GroundMap map, bool bodyFacingRight,
        float smoothingRate, float restProgress, float restTime, Vector2 driftWeight)
    {
        var newTargetPos = GetStepGoal(map, bodyFacingRight, restProgress, restTime, driftWeight * drift);
        ikTarget.position = Vector2.Lerp(ikTarget.position, newTargetPos, smoothingRate * dt);
    }

    public void UpdateFreeHang(float dt, GroundMap map, Vector2 freeHangDirection, Vector2 perturbation, Vector2 bodyDown, Vector2 bodyOrientedRight, float smoothingRate)
    {
        var x = Vector2.Dot(freeHangDirection, bodyDown);
        var y = Vector2.Dot(freeHangDirection, bodyOrientedRight);
        if ((freeHangDirectionMin.y < 0 && y < 0 && x < freeHangDirectionMin.x) || (!(freeHangDirectionMin.y < 0) && (y < 0 || x > freeHangDirectionMin.x)))
        {
            freeHangDirection = freeHangDirectionMin.ApplyTransformation(bodyDown, bodyOrientedRight);
        }
        else if ((freeHangDirectionMax.y > 0 && y > 0 && x < freeHangDirectionMax.x) || (!(freeHangDirectionMax.y > 0) && (y > 0 || x > freeHangDirectionMax.x)))
        {
            freeHangDirection = freeHangDirectionMax.ApplyTransformation(bodyDown, bodyOrientedRight);
        }

        map.CastToGround(hipBone.position, freeHangDirection, FreeHangLength, out var p);
        ikTarget.position = Vector2.Lerp(ikTarget.position, p + perturbation, smoothingRate * dt);
    }

    //public void OnBeginFreeHang()
    //{
    //    lastFreeHangPosition = ikTarget.position;
    //}

    public bool KeepTargetAboveGround(float dt, Vector2 bodyUp, 
        Vector2 bodyVelocity, float velocityOffsetRate, float velocityOffsetMax, 
        float smoothingRate, out Vector2 groundNormal)
    {
        Vector2 ikTargetPos = ikTarget.position;
        float verticalVelocity = Vector2.Dot(bodyVelocity, bodyUp);
        Vector2 predictiveTargetPos = verticalVelocity > 0 ?
            ikTargetPos + Mathf.Clamp(verticalVelocity * velocityOffsetRate, -velocityOffsetMax, velocityOffsetMax) * bodyUp 
            : ikTargetPos;
        if (Physics2D.OverlapCircle(predictiveTargetPos, staticModeGroundDetectionRadius, groundLayer))
        {
            var g = GroundRaycast(ikTargetPos, bodyUp, 1f, 1.5f);
            if (g)
            {
                ikTarget.position = verticalVelocity > 0 ? Vector2.Lerp(ikTargetPos, g.point, smoothingRate * dt) : g.point;
                groundNormal = g.normal;
                return true;
            }
        }

        groundNormal = bodyUp;
        return false;
    }

    private Vector2 GetStepStart(GroundMap map, bool bodyFacingRight, float stepProgress, float stepTime, float restTime, Vector2 drift)
    {
        var c = map.Center;
        var h = Vector2.Dot((Vector2)hipBone.position - c.point, c.normal.CWPerp());//we could also use body position and body right
        h = bodyFacingRight ? h + StepStartHorizontalOffset(stepProgress, stepTime, restTime)
            : h - StepStartHorizontalOffset(stepProgress, stepTime, restTime);
        return map.PointFromCenterByPosition(bodyFacingRight ? h + drift.x : h - drift.x, out var n) + drift.y * n;
    }

    private Vector2 GetStepGoal(GroundMap map, bool bodyFacingRight, float restProgress, float restTime, Vector2 drift)
    {
        var c = map.Center;
        var h = Vector2.Dot((Vector2)hipBone.position - c.point, c.normal.CWPerp());//we could also use body position and body right
        h = bodyFacingRight ? h + StepGoalHorizontalOffset(restProgress, restTime)
            : h - StepGoalHorizontalOffset(restProgress, restTime);
        return map.PointFromCenterByPosition(bodyFacingRight ? h + drift.x : h - drift.x, out var n) + drift.y * n;
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
