using UnityEngine;

public class CharacterAnimationControl : AnimationControl
{
    [SerializeField] AnimatorOverrideController sideAC;
    [SerializeField] float moveSpeedDampTime;

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
        SetFloat("moveSpeed", moveSpeed, moveSpeedDampTime, dt);
    }

    private RuntimeAnimatorController GetAC(MathTools.OrientationXZ o)
    {
        return o == MathTools.OrientationXZ.front || o == MathTools.OrientationXZ.back ? defaultAC : sideAC;
    }
}