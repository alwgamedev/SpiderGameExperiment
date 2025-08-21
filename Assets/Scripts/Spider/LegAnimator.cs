using Unity.Cinemachine;
using UnityEngine;

public class LegAnimator : MonoBehaviour
{
    [SerializeField] Transform hipBone;
    [SerializeField] Transform footBone;
    [SerializeField] Transform ikTarget;
    [SerializeField] float stepMax;

    Vector2 stepStartPosition;
    Vector2 lastComputedStepGoal;
    int groundLayer;

    private void Awake()
    {
        groundLayer = LayerMask.GetMask("Ground");
    }

    private void Start()
    {
        var g = CurrentGroundRaycast(Vector2.up);
        lastComputedStepGoal = g ? g.point : ikTarget.position;
    }

    //VERY useful for identifying issues
    //private void OnDrawGizmos()
    //{
    //    Gizmos.color = Color.blue;
    //    Gizmos.DrawSphere(ikTarget.position, .1f);
    //}

    public void BeginStep(Rigidbody2D body)
    {
        var bRight = body.transform.right;
        var bUp = body.transform.up;
        stepStartPosition = ikTarget.position;
        var stepGoalRay = StepGoalRaycast(bRight, bUp);
        lastComputedStepGoal = stepGoalRay ? stepGoalRay.point : BackupStepGoal(bRight, bUp);
    }

    /// <param name="stepProgress">btwn 0 & 1</param
    /// <param name="bodyRight">multiplied by sign of body local scale (i.e. points in direction body is facing)</param>
    public void UpdateStep(float dt, float stepProgress, Vector2 bodyRight, Vector2 bodyUp, bool bodyFacingRight,
        float stepHeightSpeedMultiplier, float baseStepHeightMultiplier,
        float smoothingRate, float footRotationSpeed)
    {
        stepProgress = Mathf.Clamp(stepProgress, 0.0f, 1.0f);
        var stepGoalRay = StepGoalRaycast(bodyRight, bodyUp);
        lastComputedStepGoal = stepGoalRay ? stepGoalRay.point : BackupStepGoal(bodyRight, bodyUp);
        var curGroundRay = CurrentGroundRaycast(bodyUp);

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

        stepStartPosition = bodyPosition + (stepStartPosition - bodyPosition).ReflectAcrossHyperplane(bodyRight);
        var s = Physics2D.Raycast(stepStartPosition + 3 * bodyUp, -bodyUp, Mathf.Infinity, groundLayer);
        if (s)
        {
            stepStartPosition = s.point;
        }
    }

    RaycastHit2D CurrentGroundRaycast(Vector2 bodyUp)
    {
        return Physics2D.Raycast((Vector2)ikTarget.position + 2 * bodyUp, -bodyUp, Mathf.Infinity, groundLayer);
        //adding + 2*up to make sure we raycast from above grd
    }

    RaycastHit2D StepGoalRaycast(Vector2 bodyRight, Vector2 bodyUp)
    {
        return Physics2D.Raycast((Vector2)hipBone.position + stepMax * bodyRight, -bodyUp, Mathf.Infinity, groundLayer);
    }

    Vector2 BackupStepGoal(Vector2 bodyRight, Vector2 bodyUp)
    {
        Vector2 h = hipBone.position;
        var l = stepStartPosition - h;
        l = h + Vector2.Dot(l, bodyUp) * bodyUp;
        return l + stepMax * bodyRight;
    }
}
