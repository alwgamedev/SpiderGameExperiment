using UnityEngine;

public class LegAnimator : MonoBehaviour
{
    [SerializeField] Transform hipBone;
    [SerializeField] Transform footBone;
    [SerializeField] Transform ikTarget;
    [SerializeField] float stepMax;
    [SerializeField] float driftMultiplier = 1.0f;

    Vector2 stepStartPosition;
    Vector2 stepGoalPosition;
    int groundLayer;

    private void Awake()
    {
        groundLayer = LayerMask.GetMask("Ground");
    }

    //very useful for identifying issues
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(ikTarget.position, .1f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(stepStartPosition, .06f);
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(stepGoalPosition, 0.06f);
    }

    public void InitializePosition(float bodyPosGroundHeight, Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp, float restProgress, float maxStrideLength)
    {
        var start = StepStartRaycast(bodyMovementRight, bodyUp, maxStrideLength);
        var goal = StepGoalRaycast(bodyMovementRight, bodyUp);
        stepStartPosition = start ? start.point : StaticStepStart(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, maxStrideLength);
        stepGoalPosition = goal ? goal.point : StaticStepGoal(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp);
        ikTarget.position = Vector2.Lerp(stepGoalPosition, stepStartPosition, restProgress);
        var c = CurrentGroundRaycast(bodyUp);
        if (c)
        {
            ikTarget.position = c.point;
        }
    }

    public void RepositionStepping(float bodyPosGroundHeight, Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp, float maxStrideLength)
    {
        //approximate step start position and step goal
        var start = StepStartRaycast(bodyMovementRight, bodyUp, maxStrideLength);
        var goal = StepGoalRaycast(bodyMovementRight, bodyUp);

        if (start)
        {
            stepStartPosition = start.point;
        }
        else
        {
            var p = StaticStepStart(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, maxStrideLength);
            var g = GroundRaycast(p, bodyUp);
            stepStartPosition = g ? g.point : p;
        }

        if (goal)
        {
            stepGoalPosition = goal.point;
        }
        else
        {
            var p = StaticStepGoal(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp);
            var g = GroundRaycast(p, bodyUp);
            stepGoalPosition = g ? g.point : p;
        }
    }

    public void RepositionResting(float bodyPosGroundHeight, Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp, 
        float restProgress, float maxStrideLength)
    {
        var start = StepStartRaycast(bodyMovementRight, bodyUp, maxStrideLength);
        var goal = StepPosRaycast(bodyMovementRight, bodyUp, restProgress, maxStrideLength);

        if (start)
        {
            stepStartPosition = start.point;
        }
        else
        {
            var p = StaticStepStart(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, maxStrideLength);
            var g = GroundRaycast(p, bodyUp);
            stepStartPosition = g ? g.point : p;
        }

        if (goal)
        {
            stepGoalPosition = goal.point;
        }
        else
        {
            var p = StaticStepPos(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, restProgress, maxStrideLength);
            var g = GroundRaycast(p, bodyUp);
            stepGoalPosition = g ? g.point : p;
        }
    }

    public void BeginStep(float bodyPosGroundHeight, Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp/*, float maxStrideLength*/)
    {
        var stepStartRay = GroundRaycast(ikTarget.position, bodyUp);
        stepStartPosition = stepStartRay ? stepStartRay.point : ikTarget.position;
        //ClampStepStartPosition(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, maxStrideLength);

        var stepGoalRay = StepGoalRaycast(bodyMovementRight, bodyUp);
        stepGoalPosition = stepGoalRay ? stepGoalRay.point : StaticStepPos(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp);
    }

    /// <param name="stepProgress">btwn 0 & 1</param
    /// <param name="bodyRight">multiplied by sign of body local scale (i.e. points in direction body is facing)</param>
    public void UpdateStep(float dt, float stepProgress, 
        float bodyPosGroundHeight, Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp, bool bodyFacingRight,
        /*float maxStrideLength,*/ float baseStepHeightMultiplier, float stepHeightSpeedMultiplier, 
        float smoothingRate, float footRotationSpeed)
    {
        stepProgress = Mathf.Clamp(stepProgress, 0.0f, 1.0f);

        //UPDATE STEPSTART AND STEPGOAL AS NEEDED

        //ClampStepStartPosition(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, maxStrideLength);

        var stepGoalRay = StepGoalRaycast(bodyMovementRight, bodyUp);
        stepGoalPosition = stepGoalRay ? stepGoalRay.point : StaticStepGoal(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp);

        //UPDATE IKTARGET POSITION

        var stepRight = (stepGoalPosition - stepStartPosition).normalized;
        var stepUp = bodyFacingRight ? stepRight.CCWPerp() : stepRight.CWPerp();
        var stepCenter = 0.5f * (stepGoalPosition + stepStartPosition);
        var stepRadius = Vector2.Dot(stepGoalPosition - stepCenter, stepRight);
        var t = Mathf.PI * stepProgress;

        var newTargetPosition = stepCenter - stepRadius * Mathf.Cos(t) * stepRight + stepRadius * baseStepHeightMultiplier * Mathf.Sin(t) * stepUp;

        var curGroundRay = GroundRaycast(newTargetPosition, bodyUp);
        if (stepHeightSpeedMultiplier < 1 && curGroundRay)
        {
            newTargetPosition = Vector2.Lerp(curGroundRay.point, newTargetPosition, stepHeightSpeedMultiplier);
        }

        ikTarget.position = Vector2.Lerp(ikTarget.position, newTargetPosition, smoothingRate * dt);

        //ROTATE FOOT

        if (stepHeightSpeedMultiplier != 0)
        {
            //wanting feet to point straight down is spider-specific, I guess
            var r = bodyFacingRight ? -bodyUp : bodyUp;//r needs to be normalized before lerp (in case you change r in future)
            footBone.right = Vector2.Lerp(footBone.right, r, footRotationSpeed * dt);
        }
    }

    public void UpdateStepStaticMode(float dt, float stepProgress, float bodyPosGroundHeight,
        Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp, bool bodyFacingRight, float maxStrideLength,
        float baseStepHeightMultiplier, float stepHeightSpeedMultiplier, float smoothingRate, float footRotationSpeed,
        float driftAmount = 0)
    {
        stepProgress = Mathf.Clamp(stepProgress, 0.0f, 1.0f);
        stepStartPosition = StaticStepStart(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, maxStrideLength);
        stepGoalPosition = StaticStepGoal(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp);
        if (driftAmount != 0)
        {
            stepStartPosition = ApplyOutwardDrift(stepStartPosition, bodyPos, driftAmount);
            stepGoalPosition = ApplyOutwardDrift(stepGoalPosition, bodyPos, driftAmount);
        }
        var stepRight = (stepGoalPosition - stepStartPosition).normalized;
        var stepUp = bodyFacingRight ? stepRight.CCWPerp() : stepRight.CWPerp();
        var stepCenter = 0.5f * (stepGoalPosition + stepStartPosition);
        var stepRadius = Vector2.Dot(stepGoalPosition - stepCenter, stepRight);
        var t = Mathf.PI * stepProgress;

        var newTargetPosition = stepCenter - stepRadius * Mathf.Cos(t) * stepRight + stepRadius * baseStepHeightMultiplier * Mathf.Sin(t) * stepUp;

        //var c = StaticCurrentGroundPos(bodyPosGroundHeight, bodyPos, bodyUp);
        //if (stepHeightSpeedMultiplier < 1)
        //{
        //    newTargetPosition = Vector2.Lerp(c, newTargetPosition, stepHeightSpeedMultiplier);
        //}

        ikTarget.position = Vector2.Lerp(ikTarget.position, newTargetPosition, smoothingRate * dt);

        if (stepHeightSpeedMultiplier != 0)
        {
            //wanting feet to point straight down is spider-specific, I guess
            var r = bodyFacingRight ? -bodyUp : bodyUp;//r needs to be normalized before lerp (in case you change r in future)
            footBone.right = Vector2.Lerp(footBone.right, r, footRotationSpeed * dt);
        }
    }

    public void UpdateRest(float dt, float bodyPosGroundHeight, Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp, float smoothingRate)
    {
        var w = Vector2.Dot(stepGoalPosition - (Vector2)hipBone.position, bodyMovementRight);
        if (w > stepMax)
        {
            var g = StepGoalRaycast(bodyMovementRight, bodyUp);
            stepGoalPosition = g ? g.point : StaticStepGoal(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp);
        }
        ikTarget.position = Vector2.Lerp(ikTarget.position, stepGoalPosition, smoothingRate * dt);
        //since we're only allowing feet to drag slightly -- i.e. they stay pretty near lastComputedStepGoal
        //-- don't worry about raycasting to ground to correct position
    }

    public void UpdateRestStaticMode(float dt, float restProgress, float bodyPosGroundHeight, Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp,
        float maxStrideLength, float smoothingRate, float driftAmount = 0)
    {
        //interpolate from stepGoal back towards stepStart
        var p = StaticStepPos(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, restProgress, maxStrideLength);
        if (driftAmount != 0)
        {
            p = ApplyOutwardDrift(p, bodyPos, driftAmount);
        }
        ikTarget.position = Vector2.Lerp(ikTarget.position, p, smoothingRate * dt);
    }

    public void OnBodyChangedDirection(Vector2 bodyPosition, Vector2 bodyRight, Vector2 bodyUp)
    {
        stepGoalPosition = bodyPosition + (stepGoalPosition - bodyPosition).ReflectAcrossHyperplane(bodyRight);
        var r = GroundRaycast(stepGoalPosition, bodyUp);
        if (r)
        {
            stepGoalPosition = r.point;
        }

        stepStartPosition = bodyPosition + (stepStartPosition - bodyPosition).ReflectAcrossHyperplane(bodyRight);
        var s = GroundRaycast(stepStartPosition, bodyUp);
        if (s)
        {
            stepStartPosition = s.point;
        }
    }

    Vector2 ApplyOutwardDrift(Vector2 positionToDrift, Vector2 driftCenter, float driftAmount)
    {
        return driftCenter + (1 + driftMultiplier * driftAmount) * (positionToDrift - driftCenter);
        //var u = (positionToDrift - driftCenter).normalized;
        //return positionToDrift + driftMultiplier * driftAmount * u;
    }

    RaycastHit2D GroundRaycast(Vector2 origin, Vector2 bodyUp)
    {
        return Physics2D.Raycast(origin + 3 * bodyUp, -bodyUp, Mathf.Infinity, groundLayer);
    }

    RaycastHit2D CurrentGroundRaycast(Vector2 bodyUp)
    {
        return GroundRaycast(ikTarget.position, bodyUp);
    }

    RaycastHit2D StepPosRaycast(Vector2 bodyMovementRight, Vector2 bodyUp, float restProgress = 0, float maxStrideLength = 0)
    {
        return GroundRaycast((Vector2)hipBone.position + AdjustedStepMax(restProgress, maxStrideLength) * bodyMovementRight, bodyUp);
    }

    RaycastHit2D StepGoalRaycast(Vector2 bodyMovementRight, Vector2 bodyUp)
    {
        return StepPosRaycast(bodyMovementRight, bodyUp, 0, 0);
    }

    RaycastHit2D StepStartRaycast(Vector2 bodyMovementRight, Vector2 bodyUp, float maxStrideLength)
    {
        return StepPosRaycast(bodyMovementRight, bodyUp, 1, maxStrideLength);
    }

    //and use stepProgress = 1 - restProgress
    Vector2 StaticStepPos(float bodyPosGroundHeight, Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp, float restProgress = 0, float maxStrideLength = 0)
    {
        Vector2 hipPos = hipBone.position;
        var h = bodyPosGroundHeight + Vector2.Dot(hipPos - bodyPos, bodyUp);
        return hipPos + AdjustedStepMax(restProgress, maxStrideLength) * bodyMovementRight - h * bodyUp;
    }

    Vector2 StaticStepGoal(float bodyPosGroundHeight, Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp)
    {
        return StaticStepPos(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, 0, 0);
    }

    Vector2 StaticStepStart(float bodyPosGroundHeight, Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp, float maxStrideLength)
    {
        return StaticStepPos(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, 1, maxStrideLength);
    }

    Vector2 StaticCurrentGroundPos(float bodyPosGroundHeight, Vector2 bodyPos, Vector2 bodyUp)
    {
        Vector2 targetPos = ikTarget.position;
        var h = bodyPosGroundHeight + Vector2.Dot(targetPos - bodyPos, bodyUp);
        return targetPos - h * bodyUp;
    }

    float AdjustedStepMax(float restProgress, float maxStrideLength)
    {
        return stepMax - restProgress * maxStrideLength;
    }
}
