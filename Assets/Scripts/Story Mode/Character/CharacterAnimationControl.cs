using UnityEngine;

public class CharacterAnimationControl : AnimationControl
{
    [SerializeField] AnimatorOverrideController sideAC;
    [SerializeField] float moveSpeedDampTime;
    [SerializeField] float moveSpeedMultiplier;//easier to set it here than set it in multiple blend trees

    public void OnOrientationChanged(MathTools.OrientationXZ o)
    {
        var ac = GetAC(o);
        if (ac != animator.runtimeAnimatorController)
        {
            SetRuntimeAnimatorController(ac);
        }
    }

    public void UpdateMoveSpeed(float moveSpeed, float dt)
    {
        SetFloat("moveSpeed", moveSpeed * moveSpeedMultiplier, moveSpeedDampTime, dt);
    }

    private RuntimeAnimatorController GetAC(MathTools.OrientationXZ o)
    {
        return o == MathTools.OrientationXZ.front || o == MathTools.OrientationXZ.back ? defaultAC : sideAC;
    }
}