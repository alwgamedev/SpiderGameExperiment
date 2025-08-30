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

    public void RandomizeDriftWeights()
    {
        currentDriftWeights.x = MathTools.RandomFloat(driftWeightsMin.x, driftWeightsMax.x);
        currentDriftWeights.y = MathTools.RandomFloat(driftWeightsMin.y, driftWeightsMax.y);
    }

    public void Reposition(float bodyPosGroundHeight, Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp, 
        float maxStrideLength, float restProgress = 0)
    {
        //approximate step start position and step goal
        RecalculateStepStart(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, maxStrideLength);
        RecalculateStepGoal(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, maxStrideLength, restProgress);

        //var start = StepStartRaycast(bodyMovementRight, bodyUp, maxStrideLength);
        //var goal = StepGoalRaycast(bodyMovementRight, bodyUp);

        //if (start)
        //{
        //    stepStartPosition = start.point;
        //}
        //else
        //{
        //    var p = StaticStepStart(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, maxStrideLength);
        //    var g = GroundRaycast(p, bodyUp);
        //    stepStartPosition = g ? g.point : p;
        //}

        //if (goal)
        //{
        //    stepGoalPosition = goal.point;
        //}
        //else
        //{
        //    var p = StaticStepGoal(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp);
        //    var g = GroundRaycast(p, bodyUp);
        //    stepGoalPosition = g ? g.point : p;
        //}
    }

    //public void RepositionResting(float bodyPosGroundHeight, Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp, 
    //    float restProgress, float maxStrideLength)
    //{
    //    var start = StepStartRaycast(bodyMovementRight, bodyUp, maxStrideLength);
    //    var goal = StepPosRaycast(bodyMovementRight, bodyUp, restProgress, maxStrideLength);

    //    if (start)
    //    {
    //        stepStartPosition = start.point;
    //    }
    //    else
    //    {
    //        var p = StaticStepStart(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, maxStrideLength);
    //        var g = GroundRaycast(p, bodyUp);
    //        stepStartPosition = g ? g.point : p;
    //    }

    //    if (goal)
    //    {
    //        stepGoalPosition = goal.point;
    //    }
    //    else
    //    {
    //        var p = StaticStepPos(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, restProgress, maxStrideLength);
    //        var g = GroundRaycast(p, bodyUp);
    //        stepGoalPosition = g ? g.point : p;
    //    }
    //}

    //public void BeginStep(float bodyPosGroundHeight, Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp/*, float maxStrideLength*/)
    //{
    //    var stepStartRay = GroundRaycast(ikTarget.position, bodyUp);
    //    stepStartPosition = stepStartRay ? stepStartRay.point : ikTarget.position;
    //    //ClampStepStartPosition(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, maxStrideLength);

    //    var stepGoalRay = StepGoalRaycast(bodyMovementRight, bodyUp);
    //    stepGoalPosition = stepGoalRay ? stepGoalRay.point : StaticStepPos(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp);
    //}

    /// <param name="stepProgress">btwn 0 & 1</param
    /// <param name="bodyRight">multiplied by sign of body local scale (i.e. points in direction body is facing)</param>
    public void UpdateStep(float dt, float stepProgress, 
        float bodyPosGroundHeight, Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp, bool bodyFacingRight,
        float maxStrideLength, float baseStepHeightMultiplier, float stepHeightSpeedMultiplier, 
        float smoothingRate)
    {
        stepProgress = Mathf.Clamp(stepProgress, 0.0f, 1.0f);

        //UPDATE STEPSTART AND STEPGOAL AS NEEDED

        //ClampStepStartPosition(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, maxStrideLength);
        //ConstrainStepStartPosition(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, maxStrideLength);
        //RecalculateStepStart(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, maxStrideLength);
        ConstrainStepStartPosition(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, maxStrideLength, stepProgress);
        RecalculateStepGoal(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, maxStrideLength);
        //var stepGoalRay = StepGoalRaycast(bodyMovementRight, bodyUp);
        //stepGoalPosition = stepGoalRay ? stepGoalRay.point : StaticStepGoal(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp);

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

    public void UpdateStepStaticMode(float dt, float stepProgress, float bodyPosGroundHeight,
        Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp, bool bodyFacingRight, float maxStrideLength,
        float baseStepHeightMultiplier, float stepHeightSpeedMultiplier, float smoothingRate, /*float footRotationSpeed,*/
        float driftAmount = 0)
    {
        stepProgress = Mathf.Clamp(stepProgress, 0.0f, 1.0f);
        stepStartPosition = StaticStepStart(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, maxStrideLength);
        stepGoalPosition = StaticStepGoal(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp);
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

    public void UpdateRest(float dt, float bodyPosGroundHeight, Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp, 
        float restProgress, float maxStrideLength, float smoothingRate)
    {
        ConstrainStepGoalPosition(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, maxStrideLength, restProgress);
        ikTarget.position = Vector2.Lerp(ikTarget.position, stepGoalPosition, smoothingRate * dt);
        var g = GroundRaycast(ikTarget.position, bodyUp);
        if (g)
        {
            ikTarget.position = g.point;
        }
    }

    public void UpdateRestStaticMode(float dt, float restProgress, float bodyPosGroundHeight, Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp,
        float maxStrideLength, float smoothingRate, float driftAmount = 0)
    {
        //interpolate from stepGoal back towards stepStart
        var p = StaticStepPos(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, restProgress, maxStrideLength);
        if (driftAmount != 0)
        {
            //PerturbDriftWeights(dt);
            p = ApplyOutwardDrift(p, bodyMovementRight, bodyUp, 
                driftAmount, currentDriftWeights.x, currentDriftWeights.y);
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

    //the float drift weights x,y (instead of vector) are so you can have default parameters in other methods (without having
    //to construct a vector to pass into this method)
    Vector2 ApplyOutwardDrift(Vector2 positionToDrift, Vector2 bodyRight, Vector2 bodyUp, 
        float driftAmount, float driftWeightX, float driftWeightY)
    {
        return positionToDrift + driftAmount * (driftWeightX * bodyRight + driftWeightY * bodyUp);
    }

    private void RecalculateStepStart(float bodyPosGroundHeight, Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp, 
        float maxStrideLength)
    {
        var start = StepStartRaycast(bodyMovementRight, bodyUp, maxStrideLength);

        if (start && start.distance > 0)
        {
            stepStartPosition = start.point;
        }
        else
        {
            var p = StaticStepStart(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, maxStrideLength);
            var g = GroundRaycast(p, bodyUp);
            stepStartPosition = g && g.distance > 0 ? g.point : p;
        }
    }

    private void RecalculateStepGoal(float bodyPosGroundHeight, Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp, 
        float maxStrideLength, float restProgress = 0)
    {
        var goal = StepPosRaycast(bodyMovementRight, bodyUp, restProgress, maxStrideLength);

        if (goal && goal.distance > 0)
        {
            stepGoalPosition = goal.point;
        }
        else
        {
            var p = StaticStepPos(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, restProgress, maxStrideLength);
            var g = GroundRaycast(p, bodyUp);
            stepGoalPosition = g && g.distance > 0 ? g.point : p;
        }
    }

    private void ConstrainStepStartPosition(float bodyPosGroundHeight, Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp,
        float maxStrideLength, float stepProgress)
    {
        if (!Physics2D.OverlapCircle(stepStartPosition, 0.05f, groundLayer))
        {
            RecalculateStepStart(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, maxStrideLength);
            return;
        }

        Vector2 d = stepStartPosition - (Vector2)hipBone.position;
        var h = Vector2.Dot(d, bodyMovementRight);
        var g = AdjustedStepMax(1 + stepProgress, maxStrideLength);
        if (Mathf.Abs(h - g) > recalculateThreshold)
        {
            RecalculateStepStart(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, maxStrideLength);
        }
    }

    private void ConstrainStepGoalPosition(float bodyPosGroundHeight, Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp,
        float maxStrideLength, float restProgress = 0)
    {
        if (!Physics2D.OverlapCircle(stepGoalPosition, 0.05f, groundLayer))
        {
            RecalculateStepGoal(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, maxStrideLength);
            return;
        }

        Vector2 d = stepGoalPosition - (Vector2)hipBone.position;
        var h = Vector2.Dot(d, bodyMovementRight);
        var g = AdjustedStepMax(restProgress, maxStrideLength);
        //if (debugLegCorrection)
        //{
        //    Debug.Log(h - g);
        //}
        if (Mathf.Abs(h - g) > recalculateThreshold)
        {
            RecalculateStepGoal(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, maxStrideLength);
        }
    }

    RaycastHit2D GroundRaycast(Vector2 origin, Vector2 bodyUp)
    {
        return Physics2D.Raycast(origin + 3 * bodyUp, -bodyUp, 10, groundLayer);
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
