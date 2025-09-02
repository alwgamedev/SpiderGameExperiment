using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.U2D.IK;

public class LegAnimator : MonoBehaviour
{
    [SerializeField] float hipRaycastLength = 2;
    [SerializeField] float hipRaycastUpwardBuffer = 0.5f;
    [SerializeField] float staticModeGroundDetectionRadius;
    [SerializeField] float stepMax;
    [SerializeField] float extendFractionMax;
    [SerializeField] float extendFractionMin;
    [SerializeField] Vector2 driftWeightsMax;
    [SerializeField] Vector2 driftWeightsMin;
    //[SerializeField] float randomDriftWeightsSmoothingRate;

    int groundLayer;
    Vector2 currentDriftWeights;
    IKChain2D ikChain;
    Transform hipBone;
    Transform ikTarget;
    float ikChainTotalLength;
    float extendMax;//only use squared versions ATM
    float extendMin;
    float extendMax2;
    float extendMin2;

    private void Awake()
    {
        groundLayer = LayerMask.GetMask("Ground");
        ikChain = GetComponent<FabrikSolver2D>().GetChain(0);
        hipBone = ikChain.transforms[0];
        ikTarget = ikChain.target;
        var lengths = ikChain.lengths;//because retrieving lengths rebuilds (and recalculates) the array every time...
        for (int i = 0; i < lengths.Length; i++)
        {
            ikChainTotalLength += lengths[i];
        }

        extendMax = ikChainTotalLength * extendFractionMax;
        extendMin = ikChainTotalLength * extendFractionMin;
        //extendGoal = ikChainTotalLength * extendFractionGoal;
        extendMax2 = extendMax * extendMax;
        extendMin2 = extendMin * extendMin;
    }

    //very useful for identifying issues (well it used to be until you started using local positions)
    private void OnDrawGizmos()
    {
        if (ikTarget)//because still draws gizmos outside of play mode (and I'm not gonna create needless overhead from using getters with null checks)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(ikTarget.position, .1f);
        }
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

    public void RandomizeDriftWeights()
    {
        currentDriftWeights.x = MathTools.RandomFloat(driftWeightsMin.x, driftWeightsMax.x);
        currentDriftWeights.y = MathTools.RandomFloat(driftWeightsMin.y, driftWeightsMax.y);
    }

    public void UpdateStep(float dt,
        float bodyPosGroundHeight, Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp, bool bodyFacingRight,
        float baseStepHeightMultiplier, float stepHeightSpeedMultiplier, 
        float smoothingRate, float stepProgress, float stepTime, float restTime, out Vector2 groundNormal)
    {
        var stepStart = GetStepStart(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, stepProgress, stepTime, restTime);
        var stepGoal = GetStepGoal(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, 0, restTime);
        var stepRight = (stepGoal - stepStart).normalized;
        var stepUp = bodyFacingRight ? stepRight.CCWPerp() : stepRight.CWPerp();
        var stepCenter = 0.5f * (stepGoal + stepStart);
        var stepRadius = Vector2.Dot(stepGoal - stepCenter, stepRight);
        var t = Mathf.PI * stepProgress;

        var newTargetPosition = stepCenter - stepRadius * Mathf.Cos(t) * stepRight + stepRadius * baseStepHeightMultiplier * Mathf.Sin(t) * stepUp;

        var curGroundRay = GroundRaycast(newTargetPosition, bodyUp, 1f, 1f);
        groundNormal = curGroundRay ? curGroundRay.normal : bodyUp;
        if (stepHeightSpeedMultiplier < 1 && curGroundRay)
        {
            newTargetPosition = Vector2.Lerp(curGroundRay.point, newTargetPosition, stepHeightSpeedMultiplier);
        }

        ikTarget.position = Vector2.Lerp(ikTarget.position, newTargetPosition, smoothingRate * dt);
    }

    public void UpdateRest(float dt, 
        float bodyPosGroundHeight, Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp,
        float smoothingRate, float restProgress, float restTime, out Vector2 groundNormal)
    {
        var stepGoal = GetStepGoal(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, restProgress, restTime);
        ikTarget.position = Vector2.Lerp(ikTarget.position, stepGoal, smoothingRate * dt);
        var g = GroundRaycast(ikTarget.position, bodyUp, 1f, 1f);
        //groundNormal = g ? g.normal : bodyUp;
        if (g)
        {
            groundNormal = g.normal;
            ikTarget.position = Vector2.Lerp(ikTarget.position, g.point, smoothingRate * dt);
        }
        else
        {
            groundNormal = bodyUp;
        }
    }

    public void UpdateStepStaticMode(float dt,
        float bodyPosGroundHeight, Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp, bool bodyFacingRight,
        float baseStepHeightMultiplier, float stepHeightSpeedMultiplier,
        float smoothingRate, float stepProgress, float stepTime, float restTime,
        float driftAmount = 0)
    {
        var stepStart = StaticStepStart(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, stepProgress, stepTime, restTime);
        var stepGoal = StaticStepGoal(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, 0, restTime);

        if (driftAmount != 0)
        {
            //PerturbDriftWeights(dt);
            stepStart = ApplyOutwardDrift(stepStart, bodyMovementRight, bodyUp,//but we don't apply drift to the stored stepStartLocalPosition!
                driftAmount, currentDriftWeights.x, currentDriftWeights.y);
            stepGoal = ApplyOutwardDrift(stepGoal, bodyMovementRight, bodyUp,
                driftAmount, currentDriftWeights.x, currentDriftWeights.y);
        }
        var stepRight = (stepGoal - stepStart).normalized;
        var stepUp = bodyFacingRight ? stepRight.CCWPerp() : stepRight.CWPerp();
        var stepCenter = 0.5f * (stepGoal + stepStart);
        var stepRadius = Vector2.Dot(stepGoal - stepCenter, stepRight);
        var t = Mathf.PI * stepProgress;

        var newTargetPosition = stepCenter - stepRadius * Mathf.Cos(t) * stepRight + stepRadius * baseStepHeightMultiplier * Mathf.Sin(t) * stepUp;

        //var c = StaticCurrentGroundPos(bodyPosGroundHeight, bodyPos, bodyUp);
        //if (stepHeightSpeedMultiplier < 1)
        //{
        //    newTargetPosition = Vector2.Lerp(c, newTargetPosition, stepHeightSpeedMultiplier);
        //}

        ikTarget.position = Vector2.Lerp(ikTarget.position, newTargetPosition, smoothingRate * dt);
    }

    public void UpdateRestStaticMode(float dt, 
        float bodyPosGroundHeight, Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp,
        float smoothingRate, float restProgress, float restTime,
        float driftAmount = 0)
    {
        var stepGoal = StaticStepGoal(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, restProgress, restTime);

        if (driftAmount != 0)
        {
            //PerturbDriftWeights(dt);
            stepGoal = ApplyOutwardDrift(stepGoal, bodyMovementRight, bodyUp, 
                driftAmount, currentDriftWeights.x, currentDriftWeights.y);
        }
        ikTarget.position = Vector2.Lerp(ikTarget.position, stepGoal, smoothingRate * dt);
    }

    public void EnforceExtensionConstraint(float dt, Vector2 groundNormal, Vector2 orientedGroundDir, float smoothingRate)
    {
        //may want to do multiple iterations too
        Vector2 hipBonePos = hipBone.position;
        Vector2 ikTargetPos = ikTarget.position;
        var v = ikTargetPos - hipBonePos;
        var d2 = Vector2.SqrMagnitude(v);
        if (d2 < extendMin2)
        {
            var x = Vector2.Dot(v, orientedGroundDir);
            var y = Vector2.Dot(v, groundNormal);
            var x1 = Mathf.Sign(x) * Mathf.Sqrt(extendMin2 - y * y);
            var r = StepPosRaycast(orientedGroundDir, groundNormal, x1);
            if (r)
            {
                ikTarget.position = Vector2.Lerp(ikTargetPos, r.point, smoothingRate * dt);
            }
        }
        else if (d2 > extendMax2)
        {
            var x = Vector2.Dot(v, orientedGroundDir);
            var y = Vector2.Dot(v, groundNormal);
            var y2 = Mathf.Min(y * y, extendMax2);//bc in this case it's not granted that extendMax2 - y * y >= 0
            var x1 = Mathf.Sign(x) * Mathf.Sqrt(extendMax2 - y2);
            var r = StepPosRaycast(orientedGroundDir, groundNormal, x1);
            if (r)
            {
                ikTarget.position = Vector2.Lerp(ikTargetPos, r.point, smoothingRate * dt);
            }
        }
    }

    public bool KeepTargetAboveGround(/*float dt,*/ Vector2 bodyUp, float smoothingRate, out Vector2 groundNormal)
    {
        Vector2 ikTargetPos = ikTarget.position;
        if (Physics2D.OverlapCircle(ikTargetPos, staticModeGroundDetectionRadius, groundLayer))
        {
            var g = GroundRaycast(ikTargetPos, bodyUp, 1, 1.5f);
            if (g)
            {
                ikTarget.position = g.point;
                //ikTarget.position = Vector2.Lerp(ikTargetPos, g.point, smoothingRate * dt);
                groundNormal = g.normal;
                return true;
            }
        }

        groundNormal = bodyUp;
        return false;
    }

    //the float drift weights x,y (instead of vector) are so you can have default parameters in other methods (without having
    //to construct a vector to pass into this method)
    Vector2 ApplyOutwardDrift(Vector2 positionToDrift, Vector2 bodyRight, Vector2 bodyUp, 
        float driftAmount, float driftWeightX, float driftWeightY)
    {
        return positionToDrift + driftAmount * (driftWeightX * bodyRight + driftWeightY * bodyUp);
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

    //private Vector2 GetStepStartStaticMode(float bodyPosGroundHeight, Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp,
    //    float stepProgress, float stepTime, float restTime)
    //{
    //    var s = StaticStepStart(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, stepProgress, stepTime, restTime);
    //    if (Physics2D.OverlapCircle(s, staticModeGroundDetectionRadius, groundLayer))//can't be that big of a deal since colliders do something like this every frame
    //    {
    //        return GetStepStart(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, stepProgress, stepTime, restTime);
    //    }
    //    return s;
    //}

    //private Vector2 GetStepGoalStaticMode(float bodyPosGroundHeight, Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp,
    //    float restProgress, float restTime)
    //{
    //    var s = StaticStepGoal(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, restProgress, restTime);
    //    if (Physics2D.OverlapCircle(s, staticModeGroundDetectionRadius, groundLayer))//can't be that big of a deal since colliders do something like this every frame
    //    {
    //        return GetStepGoal(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, restProgress, restTime);
    //    }
    //    return s;
    //}

    RaycastHit2D GroundRaycast(Vector2 origin, Vector2 bodyUp, float raycastLength, float upwardBuffer = .5f)
    {
        return Physics2D.Raycast(origin + upwardBuffer * bodyUp, -bodyUp, raycastLength + upwardBuffer, groundLayer);
    }

    RaycastHit2D StepPosRaycast(Vector2 bodyMovementRight, Vector2 bodyUp, float horizontalOffset)
    {
        return GroundRaycast((Vector2)hipBone.position + horizontalOffset * bodyMovementRight, bodyUp, hipRaycastLength, hipRaycastUpwardBuffer);
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
