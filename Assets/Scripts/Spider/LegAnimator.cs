using UnityEngine;

public class LegAnimator : MonoBehaviour
{
    [SerializeField] Transform hipBone;
    [SerializeField] Transform footBone;
    [SerializeField] Transform ikTarget;
    [SerializeField] float stepMax;

    Vector2 stepStartLocalPosition;
    Vector2 lastComputedStepGoal;
    int groundLayer;

    public float footRaycastLength;
    public float hipRaycastLength;

    public Transform HipBone => hipBone;
    public Transform FootBone => footBone;
    public Vector2 UpLegUnitRay => (hipBone.position - footBone.position).normalized;

    private void Awake()
    {
        groundLayer = LayerMask.GetMask("Ground");
    }

    //private void Start()
    //{
    //    var g = CurrentGroundRaycast(Vector2.up);
    //    var s = StepGoalRaycast(Vector2.right, Vector2.up);
    //    stepStartPosition = g ? g.point : ikTarget.position;
    //    lastComputedStepGoal = s ? s.point : BackupStepGoal(Vector2.right, Vector2.up);
    //}

    //very useful for identifying issues
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(ikTarget.position, .1f);
    }

    public void RepositionStepping(Vector2 bodyPos, Vector2 bodyRight, Vector2 bodyUp, float maxStrideLength)
    {
        //approximate step start position and step goal
        var start = StepGoalRaycast(bodyRight, bodyUp, 1, maxStrideLength);
        var goal = StepGoalRaycast(bodyRight, bodyUp, 0, maxStrideLength);
        stepStartLocalPosition = start ? start.point : BackupStepGoal(bodyPos, bodyRight, bodyUp, 1, maxStrideLength);
        stepStartLocalPosition -= bodyPos;
        lastComputedStepGoal = goal ? goal.point : BackupStepGoal(bodyPos, bodyRight, bodyUp, 0, maxStrideLength);
    }

    public void RepositionResting(Vector2 bodyPos, Vector2 bodyRight, Vector2 bodyUp, float restProgress, float maxStrideLength)
    {
        var s = StepGoalRaycast(bodyRight, bodyUp, restProgress, maxStrideLength);
        lastComputedStepGoal = s ? s.point : BackupStepGoal(bodyPos, bodyRight, bodyUp, restProgress, maxStrideLength);
    }

    public void BeginStep(Rigidbody2D body)
    {
        var bRight = body.transform.right;
        var bUp = body.transform.up;
        var bPos = body.transform.position;
        stepStartLocalPosition = ikTarget.position - bPos;
        var stepGoalRay = StepGoalRaycast(bRight, bUp);
        lastComputedStepGoal = stepGoalRay ? stepGoalRay.point : BackupStepGoal(bPos, bRight, bUp);
    }

    /// <param name="stepProgress">btwn 0 & 1</param
    /// <param name="bodyRight">multiplied by sign of body local scale (i.e. points in direction body is facing)</param>
    public void UpdateStep(float dt, float stepProgress, Vector2 bodyPos, Vector2 bodyRight, Vector2 bodyUp, bool bodyFacingRight,
        float stepHeightSpeedMultiplier, float baseStepHeightMultiplier,
        float smoothingRate, float footRotationSpeed)
    {
        stepProgress = Mathf.Clamp(stepProgress, 0.0f, 1.0f);
        var stepGoalRay = StepGoalRaycast(bodyRight, bodyUp);
        lastComputedStepGoal = stepGoalRay ? stepGoalRay.point : BackupStepGoal(bodyPos, bodyRight, bodyUp);
        var curGroundRay = CurrentGroundRaycast(bodyUp);

        var stepStartPosition = bodyPos + stepStartLocalPosition;
        var stepRight = (lastComputedStepGoal - stepStartPosition).normalized;
        var stepUp = bodyFacingRight ? stepRight.CCWPerp() : stepRight.CWPerp();
        var stepCenter = 0.5f * (lastComputedStepGoal + stepStartPosition);
        var stepRadius = Vector2.Dot(lastComputedStepGoal - stepCenter, stepRight);
        var t = Mathf.PI * stepProgress;
        var newEffectorPosition = stepCenter - stepRadius * Mathf.Cos(t) * stepRight + stepRadius * baseStepHeightMultiplier * Mathf.Sin(t) * stepUp;
        if (stepHeightSpeedMultiplier < 1 && curGroundRay)
        {
            newEffectorPosition = Vector2.Lerp(curGroundRay.point, newEffectorPosition, stepHeightSpeedMultiplier);
        }

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
        ikTarget.position = Vector2.Lerp(ikTarget.position, lastComputedStepGoal, smoothingRate * dt);
        //so that legs don't get dragged along the ground with the body and stay planted where they stepped down
        //(although the lerp will allow them to drag a little)
    }

    public void OnBodyChangedDirection(Vector2 bodyPosition, Vector2 bodyRight, Vector2 bodyUp)
    {
        lastComputedStepGoal = bodyPosition + (lastComputedStepGoal - bodyPosition).ReflectAcrossHyperplane(bodyRight);
        var r = Physics2D.Raycast(lastComputedStepGoal + 3 * bodyUp, -bodyUp, Mathf.Infinity, groundLayer);
        if (r)
        {
            lastComputedStepGoal = r.point;
        }

        stepStartLocalPosition = stepStartLocalPosition.ReflectAcrossHyperplane(bodyRight);//bodyPosition + (stepStartLocalPosition - bodyPosition).ReflectAcrossHyperplane(bodyRight);
        var s = Physics2D.Raycast(bodyPosition + stepStartLocalPosition + 3 * bodyUp, -bodyUp, Mathf.Infinity, groundLayer);
        if (s)
        {
            stepStartLocalPosition = s.point - bodyPosition;
        }
    }

    RaycastHit2D CurrentGroundRaycast(Vector2 bodyUp)
    {
        return Physics2D.Raycast((Vector2)ikTarget.position + 2 * bodyUp, -bodyUp, footRaycastLength + 2, groundLayer);
        //adding + 2*up to make sure we raycast from above grd
    }

    RaycastHit2D StepGoalRaycast(Vector2 bodyRight, Vector2 bodyUp, float restProgress = 0, float maxStrideLength = 0)
    {
        return Physics2D.Raycast((Vector2)hipBone.position + AdjustedStepMax(restProgress, maxStrideLength) * bodyRight, -bodyUp, hipRaycastLength, groundLayer);
    }

    Vector2 BackupStepGoal(Vector2 bodyPos, Vector2 bodyRight, Vector2 bodyUp, float restProgress = 0, float maxStrideLength = 0)
    {
        Vector2 h = hipBone.position;
        var l = bodyPos + stepStartLocalPosition - h;
        l = h + Vector2.Dot(l, bodyUp) * bodyUp;
        return l + AdjustedStepMax(restProgress, maxStrideLength) * bodyRight;
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
