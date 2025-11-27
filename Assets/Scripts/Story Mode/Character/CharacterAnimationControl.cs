using UnityEngine;

public class CharacterAnimationControl : AnimationControl
{
    [SerializeField] AnimatorOverrideController sideAC;
    [SerializeField] float moveSpeedDampTime;
    [SerializeField] float moveSpeedMultiplier;//easier to set it here than set it in multiple blend trees

    //2do: swapping AC "smoothly" (maintaining parameter values and animation state like we did in rpg)

    //you may want to just give fb and side models separate Animator components instead of swapping AC's,
    //in case e.g. walk gets out of sync

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