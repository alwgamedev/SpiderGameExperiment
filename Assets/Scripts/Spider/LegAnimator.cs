using UnityEngine;

public class LegAnimator : MonoBehaviour
{
    [SerializeField] Transform hipBone;
    [SerializeField] Transform footBone;
    [SerializeField] Transform ikTarget;
    [SerializeField] float stepMax;
    [SerializeField] float recalculateThreshold = .5f;
    //[SerializeField] float driftMultiplier = 1.0f;
    [SerializeField] Vector2 driftWeightsMax;
    [SerializeField] Vector2 driftWeightsMin;
    //[SerializeField] float randomDriftWeightsSmoothingRate;

    Vector2 stepStartPosition;
    Vector2 stepGoalPosition;
    Vector2 currentDriftWeights;
    int groundLayer;

    private void Awake()
    {
        groundLayer = LayerMask.GetMask("Ground");
        //RandomizeDriftWeights();
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

    public void InitializePosition(float bodyPosGroundHeight, Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp, 
        bool stepping, float stateProgress, float stepTime, float restTime)
    {
        RecalculateGuides(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, stepping, stateProgress, stepTime, restTime);
        ikTarget.position = stepping ?
                Vector2.Lerp(stepStartPosition, stepGoalPosition, stateProgress)
                : Vector2.Lerp(stepGoalPosition, stepGoalPosition - restTime * bodyMovementRight, stateProgress);
        var c = GroundRaycast(ikTarget.position, bodyUp);
        if (c)
        {
            ikTarget.position = c.point;
        }
    }

    public void RandomizeDriftWeights()
    {
        currentDriftWeights.x = MathTools.RandomFloat(driftWeightsMin.x, driftWeightsMax.x);
        currentDriftWeights.y = MathTools.RandomFloat(driftWeightsMin.y, driftWeightsMax.y);
    }

    public void RecalculateGuides(float bodyPosGroundHeight, Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp, 
        bool stepping, float stateProgress, float stepTime, float restTime)
    {
        //approximate step start position and step goal
        if (stepping)
        {
            RecalculateStepStart(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, stateProgress, stepTime, restTime);
            RecalculateStepGoal(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, 0, restTime);
        }
        else
        {
            RecalculateStepGoal(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, stateProgress, restTime);
        }
    }

    public void BeginStep(float bodyPosGroundHeight, Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp, 
        float stepProgress, float stepTime, float restTime)
    {
        var stepStartRay = GroundRaycast(ikTarget.position, bodyUp);
        if (stepStartRay)
        {
            stepStartPosition = stepStartRay.point;
        }
        else
        {
            RecalculateStepStart(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, stepProgress, stepTime, restTime);
        }

        //RecalculateStepGoal(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, 0, restTime);
        //don't need to calculate step goal because it gets recalculated in every step update
    }

    public void UpdateStep(float dt,
        float bodyPosGroundHeight, Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp, bool bodyFacingRight,
        float baseStepHeightMultiplier, float stepHeightSpeedMultiplier, 
        float smoothingRate, float stepProgress, float stepTime, float restTime)
    {
        //stepProgress = Mathf.Clamp(stepProgress, 0.0f, 1.0f);

        //UPDATE STEPSTART AND STEPGOAL AS NEEDED

        ConstrainStepStartPosition(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, stepProgress, stepTime, restTime);
        RecalculateStepGoal(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, 0, restTime);

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

        //footBone.right = bodyFacingRight ? -bodyUp : bodyUp;

        //if (stepHeightSpeedMultiplier != 0)
        //{
        //    //wanting feet to point straight down is spider-specific, I guess
        //    var r = bodyFacingRight ? -bodyUp : bodyUp;//r needs to be normalized before lerp (in case you change r in future)
        //    footBone.right = Vector2.Lerp(footBone.right, r, footRotationSpeed * dt);
        //}
    }

    public void UpdateStepStaticMode(float dt,
        float bodyPosGroundHeight, Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp, bool bodyFacingRight,
        float baseStepHeightMultiplier, float stepHeightSpeedMultiplier,
        float smoothingRate, float stepProgress, float stepTime, float restTime,
        float driftAmount = 0)
    {
        stepProgress = Mathf.Clamp(stepProgress, 0.0f, 1.0f);
        stepStartPosition = StaticStepStart(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, stepProgress, stepTime, restTime);
        stepGoalPosition = StaticStepGoal(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, 0, restTime);
        if (driftAmount != 0)
        {
            //PerturbDriftWeights(dt);
            stepStartPosition = ApplyOutwardDrift(stepStartPosition, bodyMovementRight, bodyUp,
                driftAmount, currentDriftWeights.x, currentDriftWeights.y);
            stepGoalPosition = ApplyOutwardDrift(stepGoalPosition, bodyMovementRight, bodyUp,
                driftAmount, currentDriftWeights.x, currentDriftWeights.y);
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

        //if (stepHeightSpeedMultiplier != 0)
        //{
        //    //wanting feet to point straight down is spider-specific, I guess
        //    var r = bodyFacingRight ? -bodyUp : bodyUp;//r needs to be normalized before lerp (in case you change r in future)
        //    footBone.right = Vector2.Lerp(footBone.right, r, footRotationSpeed * dt);
        //}
    }

    public void UpdateRest(float dt, 
        float bodyPosGroundHeight, Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp,
        float smoothingRate, float restProgress, float restTime)
    {
        ConstrainStepGoalPosition(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, restProgress, restTime);
        ikTarget.position = Vector2.Lerp(ikTarget.position, stepGoalPosition, smoothingRate * dt);
        var g = GroundRaycast(ikTarget.position, bodyUp);
        if (g)
        {
            ikTarget.position = Vector2.Lerp(ikTarget.position, g.point, smoothingRate * dt);
        }
    }

    public void UpdateRestStaticMode(float dt, 
        float bodyPosGroundHeight, Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp,
        float smoothingRate, float restProgress, float restTime,
        float driftAmount = 0)
    {
        //interpolate from stepGoal back towards stepStart
        stepGoalPosition = StaticStepGoal(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, restProgress, restTime);
        if (driftAmount != 0)
        {
            //PerturbDriftWeights(dt);
            stepGoalPosition = ApplyOutwardDrift(stepGoalPosition, bodyMovementRight, bodyUp, 
                driftAmount, currentDriftWeights.x, currentDriftWeights.y);
        }
        ikTarget.position = Vector2.Lerp(ikTarget.position, stepGoalPosition, smoothingRate * dt);
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

    //the float drift weights x,y (instead of vector) are so you can have default parameters in other methods (without having
    //to construct a vector to pass into this method)
    Vector2 ApplyOutwardDrift(Vector2 positionToDrift, Vector2 bodyRight, Vector2 bodyUp, 
        float driftAmount, float driftWeightX, float driftWeightY)
    {
        return positionToDrift + driftAmount * (driftWeightX * bodyRight + driftWeightY * bodyUp);
    }

    private void RecalculateStepStart(float bodyPosGroundHeight, Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp, 
        float stepProgress, float stepTime, float restTime)
    {
        var start = StepStartRaycast(bodyMovementRight, bodyUp, stepProgress, stepTime, restTime);

        if (start)
        {
            stepStartPosition = start.point;
        }
        else
        {
            var p = StaticStepStart(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, stepProgress, stepTime, restTime);
            var g = GroundRaycast(p, bodyUp);
            stepStartPosition = g ? g.point : p;
        }
    }

    private void RecalculateStepGoal(float bodyPosGroundHeight, Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp, 
        float restProgress, float restTime)
    {
        var goal = StepGoalRaycast(bodyMovementRight, bodyUp, restProgress, restTime);

        if (goal)
        {
            stepGoalPosition = goal.point;
        }
        else
        {
            var p = StaticStepGoal(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, restProgress, restTime);
            goal = GroundRaycast(p, bodyUp);
            stepGoalPosition = goal ? goal.point : p;
        }
    }

    private void ConstrainStepStartPosition(float bodyPosGroundHeight, Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp,
        float stepProgress, float stepTime, float restTime)
    {
        if (!Physics2D.OverlapCircle(stepStartPosition, 0.05f, groundLayer))
        {
            RecalculateStepStart(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, stepProgress, stepTime, restTime);
            return;
        }

        Vector2 d = stepStartPosition - (Vector2)hipBone.position;
        var h = Vector2.Dot(d, bodyMovementRight);
        var g = StepStartHorizontalOffset(stepProgress, stepTime, restTime);
        if (Mathf.Abs(h - g) > recalculateThreshold)
        {
            Debug.Log($"{gameObject.name} correcting step start");
            RecalculateStepStart(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, stepProgress, stepTime, restTime);
        }
    }

    private void ConstrainStepGoalPosition(float bodyPosGroundHeight, Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp,
        float restProgress, float restTime)
    {
        if (!Physics2D.OverlapCircle(stepGoalPosition, 0.05f, groundLayer))
        {
            RecalculateStepGoal(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, restProgress, restTime);
            return;
        }

        Vector2 d = stepGoalPosition - (Vector2)hipBone.position;
        var h = Vector2.Dot(d, bodyMovementRight);
        var g = StepGoalHorizontalOffset(restProgress, restTime);
        if (Mathf.Abs(h - g) > recalculateThreshold)
        {
            Debug.Log($"{gameObject.name} correcting step goal");
            RecalculateStepGoal(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, restProgress, restTime);
        }
    }

    RaycastHit2D GroundRaycast(Vector2 origin, Vector2 bodyUp)
    {
        return Physics2D.Raycast(origin + 3 * bodyUp, -bodyUp, 10, groundLayer);
    }

    //RaycastHit2D CurrentGroundRaycast(Vector2 bodyUp)
    //{
    //    return GroundRaycast(ikTarget.position, bodyUp);
    //}

    RaycastHit2D StepPosRaycast(Vector2 bodyMovementRight, Vector2 bodyUp, float horizontalOffset)
    {
        return GroundRaycast((Vector2)hipBone.position + horizontalOffset * bodyMovementRight, bodyUp);
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

    Vector2 StaticCurrentGroundPos(float bodyPosGroundHeight, Vector2 bodyPos, Vector2 bodyUp)
    {
        Vector2 targetPos = ikTarget.position;
        var h = bodyPosGroundHeight + Vector2.Dot(targetPos - bodyPos, bodyUp);
        return targetPos - h * bodyUp;
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
