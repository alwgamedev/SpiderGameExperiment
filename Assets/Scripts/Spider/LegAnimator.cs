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
    //[SerializeField] float ikTargetGroundDetectionRadius;
    [SerializeField] Vector2 driftWeightsMax;
    [SerializeField] Vector2 driftWeightsMin;
    

    int groundLayer;
    Vector2 currentDriftWeights;
    //IKChain2D ikChain;
    Transform hipBone;
    Transform ikTarget;
    float ikChainTotalLength;
    //float extendMax;//only use squared versions ATM
    //float extendMin;
    float extendMax2;
    float extendMin2;

    private void Awake()
    {
        groundLayer = LayerMask.GetMask("Ground");
        var ikChain = GetComponent<Solver2D>().GetChain(0);
        hipBone = ikChain.transforms[0];
        ikTarget = ikChain.target;
        var lengths = ikChain.lengths;//because retrieving lengths rebuilds (and recalculates) the array every time...
        for (int i = 0; i < lengths.Length; i++)
        {
            ikChainTotalLength += lengths[i];
        }

        var extendMax = ikChainTotalLength * extendFractionMax;
        var extendMin = ikChainTotalLength * extendFractionMin;
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
        float baseStepHeightMultiplier, /*float stepHeightSpeedMultiplier,*/
        float smoothingRate, float stepProgress, float stepTime, float restTime,
        float driftAmount = 0)
    {
        var stepStart = StaticStepStart(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, stepProgress, stepTime, restTime);
        var stepGoal = StaticStepGoal(bodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, 0, restTime);

        if (driftAmount != 0)
        {
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

        //in static mode we aren't lerping vertically by stepHeightSpeedMultiplier

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
            //var r = StepPosRaycast(orientedGroundDir, groundNormal, x1);
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

    //2do: this causes feet to cling to ground during jump takeoff
    //maybe offset the overlap circle by body velocity in the up direction? (clamping the max offset)
    //(hece offset up when taking off, and offset down when landing)
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
                //we could add smoothing, but should be naturally smoothed by physics
                //-- actually we need to add smoothing, otherwise feet can snap to ground on landing
                //ALSO 2DO: leg synch doesn't need stepSmoothRate and restSmoothRate -- may just separate smoothing
                //rates for default mode and static mode
                ikTarget.position = verticalVelocity > 0 ? Vector2.Lerp(ikTargetPos, g.point, smoothingRate * dt) : g.point;
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

    RaycastHit2D GroundRaycast(Vector2 origin, Vector2 bodyUp, float raycastLength, float upwardBuffer = .5f)
    {
        return Physics2D.Raycast(origin + upwardBuffer * bodyUp, -bodyUp, raycastLength + upwardBuffer, groundLayer);
    }

    //RaycastHit2D StepPosRaycast(float groundDetectionRadius, Vector2 defaultSearchRight, Vector2 defaultSearchUp, float horizontalOffset)
    //{
    //    Vector2 ikTargetPos = ikTarget.position;
    //    var c = Physics2D.OverlapCircle(ikTarget.position, groundDetectionRadius, groundLayer);
    //    if (c)
    //    {
    //        var p = c.ClosestPoint(ikTargetPos);
    //        var ray = Physics2D.Raycast(ikTargetPos + defaultSearchUp, p - ikTargetPos, groundDetectionRadius + 1, groundLayer);
    //        if (ray)
    //        {
    //            var u = ray.normal;
    //            var r = u.CWPerp();
    //            if (Vector2.Dot(r, defaultSearchRight) < 0)//in future we could pass a "facing right" parameter
    //            {
    //                r = -r;
    //            }
    //            Debug.Log(u);
    //            return StepPosRaycast(r, u, horizontalOffset);
    //        }
    //    }

    //    return StepPosRaycast(defaultSearchRight, defaultSearchUp, horizontalOffset);
    //}

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
