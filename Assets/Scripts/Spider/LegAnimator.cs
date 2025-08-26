using Unity.VisualScripting;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;

public class LegAnimator : MonoBehaviour
{
    [SerializeField] Transform hipBone;
    [SerializeField] Transform footBone;
    [SerializeField] Transform ikTarget;
    [SerializeField] float stepMax;

    Vector2 stepStartPosition;
    Vector2 stepStartLocalPosition;
    Vector2 stepGoalPosition;
    Vector2 stepGoalLocalPosition;
    int groundLayer;

    public Transform HipBone => hipBone;
    public Transform FootBone => footBone;
    public Vector2 UpLegUnitRay => (hipBone.position - footBone.position).normalized;

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

    public void OnEnterStaticMode(Vector2 bodyPos)
    {
        CaptureLocalPositions(bodyPos);
    }

    public void OnEndStaticMode(/*Vector2 bodyPos,*/ Vector2 bodyUp)
    {
        //stepGoalPosition = bodyPos + stepGoalLocalPosition;
        var r = Physics2D.Raycast(stepGoalPosition + 3 * bodyUp, -bodyUp, Mathf.Infinity, groundLayer);
        if (r)
        {
            stepGoalPosition = r.point;
        }

        //stepStartPosition = bodyPos + stepStartLocalPosition;
        var s = Physics2D.Raycast(stepStartPosition + 3 * bodyUp, -bodyUp, Mathf.Infinity, groundLayer);
        if (s)
        {
            stepStartPosition = s.point;
        }
    }

    public void RepositionStepping(Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp, float maxStrideLength)
    {
        //approximate step start position and step goal
        var start = StepGoalRaycast(bodyMovementRight, bodyUp);
        var goal = StepGoalRaycast(bodyMovementRight, bodyUp);
        stepStartPosition = start ? start.point : BackupStepGoal(bodyPos, bodyMovementRight, bodyUp, 1, maxStrideLength);
        stepGoalPosition = goal ? goal.point : BackupStepGoal(bodyPos, bodyMovementRight, bodyUp);
    }

    public void RepositionResting(Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp, float restProgress, float maxStrideLength)
    {
        var s = StepGoalRaycast(bodyMovementRight, bodyUp);
        stepGoalPosition = s ? s.point : BackupStepGoal(bodyPos, bodyMovementRight, bodyUp, restProgress, maxStrideLength);
    }

    public void BeginStep(Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp)
    {
        //var bRight = body.transform.right;
        //var bUp = body.transform.up;
        //var bPos = body.transform.position;
        stepStartPosition = ikTarget.position;
        var stepGoalRay = StepGoalRaycast(bodyMovementRight, bodyUp);
        stepGoalPosition = stepGoalRay ? stepGoalRay.point : BackupStepGoal(bodyPos, bodyMovementRight, bodyUp);
    }

    public void BeginStepStaticMode(Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp)
    {
        stepStartLocalPosition = (Vector2)ikTarget.position - bodyPos;
        stepGoalLocalPosition = BackupStepGoal(bodyPos, bodyMovementRight, bodyUp) - bodyPos;
    }

    /// <param name="stepProgress">btwn 0 & 1</param
    /// <param name="bodyRight">multiplied by sign of body local scale (i.e. points in direction body is facing)</param>
    public void UpdateStep(float dt, float stepProgress, Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp, bool bodyFacingRight,
        float stepHeightSpeedMultiplier, float baseStepHeightMultiplier,
        float smoothingRate, float footRotationSpeed)
    {
        stepProgress = Mathf.Clamp(stepProgress, 0.0f, 1.0f);
        //var bodyMovementRight = bodyFacingRight ? bodyRight : -bodyRight;
        var stepGoalRay = StepGoalRaycast(bodyMovementRight, bodyUp);
        stepGoalPosition = stepGoalRay ? stepGoalRay.point : BackupStepGoal(bodyPos, bodyMovementRight, bodyUp);
        var curGroundRay = CurrentGroundRaycast(bodyUp);

        //var stepStartPosition = bodyPos + stepStartLocalPosition;
        var stepRight = (stepGoalPosition - stepStartPosition).normalized;
        var stepUp = bodyFacingRight ? stepRight.CCWPerp() : stepRight.CWPerp();
        var stepCenter = 0.5f * (stepGoalPosition + stepStartPosition);
        var stepRadius = Vector2.Dot(stepGoalPosition - stepCenter, stepRight);
        var t = Mathf.PI * stepProgress;
        var newTargetPosition = stepCenter - stepRadius * Mathf.Cos(t) * stepRight + stepRadius * baseStepHeightMultiplier * Mathf.Sin(t) * stepUp;
        if (stepHeightSpeedMultiplier < 1 && curGroundRay)
        {
            newTargetPosition = Vector2.Lerp(curGroundRay.point, newTargetPosition, stepHeightSpeedMultiplier);
        }

        //var lerpAmount = smoothingRate * dt;
        ikTarget.position = Vector2.Lerp(ikTarget.position, newTargetPosition, smoothingRate * dt);
        //EnforceDistanceConstraint();

        if (stepHeightSpeedMultiplier != 0)
        {
            //wanting feet to point straight down is spider-specific, I guess
            var r = bodyFacingRight ? -bodyUp : bodyUp;//r needs to be normalized before lerp (in case you change r in future)
            footBone.right = Vector2.Lerp(footBone.right, r, footRotationSpeed * dt);
        }
    }

    public void UpdateStepStaticMode(float dt, float stepProgress, Vector2 bodyPos, Vector2 bodyUp, bool bodyFacingRight, 
        float baseStepHeightMultiplier, float stepHeightSpeedMultiplier, float smoothingRate, float footRotationSpeed)
    {
        stepProgress = Mathf.Clamp(stepProgress, 0.0f, 1.0f);
        stepStartPosition = bodyPos + stepStartLocalPosition;
        stepGoalPosition = bodyPos + stepGoalLocalPosition;
        var stepRight = (stepGoalPosition - stepStartPosition).normalized;
        var stepUp = bodyFacingRight ? stepRight.CCWPerp() : stepRight.CWPerp();
        var stepCenter = 0.5f * (stepGoalPosition + stepStartPosition);
        var stepRadius = Vector2.Dot(stepGoalPosition - stepCenter, stepRight);
        var t = Mathf.PI * stepProgress;
        var newEffectorPosition = stepCenter - stepRadius * Mathf.Cos(t) * stepRight + stepRadius * baseStepHeightMultiplier * Mathf.Sin(t) * stepUp;

        ikTarget.position = Vector2.Lerp(ikTarget.position, newEffectorPosition, smoothingRate * dt);

        if (stepHeightSpeedMultiplier != 0)
        {
            //wanting feet to point straight down is spider-specific, I guess
            var r = bodyFacingRight ? -bodyUp : bodyUp;//r needs to be normalized before lerp (in case you change r in future)
            footBone.right = Vector2.Lerp(footBone.right, r, footRotationSpeed * dt);
        }
    }

    public void UpdateRest(float dt, float smoothingRate)
    {
        ikTarget.position = Vector2.Lerp(ikTarget.position, stepGoalPosition, smoothingRate * dt);
        //since we're only allowing feet to drag slightly -- i.e. they stay pretty near lastComputedStepGoal
        //-- don't worry about raycasting to ground to correct position
    }

    //can be bodyRight or bodyMovementRight, makes no difference (since bodyMovementRight gets passed to the other methods by LegSynchronizer,
    //that's probably what will get used)
    public void UpdateRestStaticMode(float dt, Vector2 bodyPos, Vector2 bodyRight, Vector2 bodyUp, float smoothingRate)
    {
        stepStartPosition = bodyPos + stepStartLocalPosition;
        stepGoalPosition = bodyPos + Vector2.Dot(stepGoalPosition - bodyPos, bodyRight) * bodyRight
            + Vector2.Dot(stepGoalLocalPosition, bodyUp) * bodyUp;
            //using the LOCAL position for up component (so step goal only drifts horizontally as body moves around)
        UpdateRest(dt, smoothingRate);
    }

    public void OnBodyChangedDirection(Vector2 bodyPosition, Vector2 bodyRight, Vector2 bodyUp)
    {
        stepGoalPosition = bodyPosition + (stepGoalPosition - bodyPosition).ReflectAcrossHyperplane(bodyRight);
        var r = Physics2D.Raycast(stepGoalPosition + 3 * bodyUp, -bodyUp, Mathf.Infinity, groundLayer);
        if (r)
        {
            stepGoalPosition = r.point;
        }

        stepStartPosition = bodyPosition + (stepStartPosition - bodyPosition).ReflectAcrossHyperplane(bodyRight);
        var s = Physics2D.Raycast(stepStartPosition + 3 * bodyUp, -bodyUp, Mathf.Infinity, groundLayer);
        if (s)
        {
            stepStartPosition = s.point;
        }

        //CaptureLocalPositions(bodyPosition);
    }

    public void OnBodyChangedDirectionStaticMode(Vector2 bodyPos, Vector2 bodyRight)
    {
        stepGoalLocalPosition = stepGoalLocalPosition.ReflectAcrossHyperplane(bodyRight);
        stepGoalPosition = bodyPos + stepGoalLocalPosition;
        stepStartLocalPosition = stepStartLocalPosition.ReflectAcrossHyperplane(bodyRight);
        stepStartPosition = bodyPos + stepStartPosition;
        //stepGoalPosition = bodyPosition + (stepGoalPosition - bodyPosition).ReflectAcrossHyperplane(bodyRight);
        //var r = Physics2D.Raycast(stepGoalPosition + 3 * bodyUp, -bodyUp, Mathf.Infinity, groundLayer);
        //if (r)
        //{
        //    stepGoalPosition = r.point;
        //}

        //stepStartPosition = bodyPosition + (stepStartPosition - bodyPosition).ReflectAcrossHyperplane(bodyRight);
        //var s = Physics2D.Raycast(stepStartPosition + 3 * bodyUp, -bodyUp, Mathf.Infinity, groundLayer);
        //if (s)
        //{
        //    stepStartPosition = s.point;
        //}

        //CaptureLocalPositions(bodyPosition);
    }

    private void CaptureLocalPositions(Vector2 bodyPos)
    {
        stepStartLocalPosition = stepStartPosition - bodyPos;
        stepGoalLocalPosition = stepGoalPosition - bodyPos;
    }

    RaycastHit2D CurrentGroundRaycast(Vector2 bodyUp)
    {
        //return FootToGroundRaycast(ikTarget.position, bodyUp);
        return Physics2D.Raycast((Vector2)ikTarget.position + 3 * bodyUp, -bodyUp, Mathf.Infinity, groundLayer);
        //adding + 2*up to make sure we raycast from above grd
    }

    RaycastHit2D StepGoalRaycast(Vector2 bodyMovementRight, Vector2 bodyUp, float restProgress = 0, float maxStrideLength = 0)
    {
        return Physics2D.Raycast((Vector2)hipBone.position + AdjustedStepMax(restProgress, maxStrideLength) * bodyMovementRight, -bodyUp, Mathf.Infinity, groundLayer);
    }

    Vector2 BackupStepGoal(Vector2 bodyPos, Vector2 bodyMovementRight, Vector2 bodyUp, float restProgress = 0, float maxStrideLength = 0)
    {
        Vector2 h = hipBone.position;
        var l = stepStartPosition - h;
        l = h + Vector2.Dot(l, bodyUp) * bodyUp;
        return l + AdjustedStepMax(restProgress, maxStrideLength) * bodyMovementRight;
    }

    float AdjustedStepMax(float restProgress, float maxStrideLength)
    {
        if (restProgress == 0)
        {
            return stepMax;
        }
        return stepMax - restProgress * maxStrideLength;
    }
}
