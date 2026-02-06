using UnityEngine;
using System.Linq;
using UnityEngine.U2D.IK;

public class LegSynchronizer : MonoBehaviour
{
    [SerializeField] Rigidbody2D bodyRb;
    [SerializeField] float restTime;
    [SerializeField] float stepTime;
    [SerializeField] float speedCapMin;//at or below this speed, stepHeight is zero
    [SerializeField] float speedCapMax;//at or above this speed, use full stepHeight
    [SerializeField] float baseStepHeightMultiplier;
    [SerializeField] float stepSmoothingRate;
    [SerializeField] float freeHangSmoothingRate;
    [SerializeField] float freeHangStepHeightMultiplier;
    [SerializeField] float driftWeightSmoothingRate;
    [SerializeField] Solver2D[] ikSolver;
    [SerializeField] LegTarget[] legTarget;
    [SerializeField] float[] timeOffset;

    LegTimer[] timer;
    bool freeHanging;
    int legCount;
    float legCountInverse;

    public float bodyGroundSpeedSign;
    public float absoluteBodyGroundSpeed;
    public float timeScale = 1;
    public float stepHeightFraction;
    public float strideMultiplier = 1;

    public bool FreeHanging//freeHanging = ikTargets gradually catch up to body as it moves to make legs seem like they're dangling freely
    {
        get => freeHanging;
        set
        {
            if (value != freeHanging)
            {
                freeHanging = value;
                if (freeHanging)
                {
                    CacheFreeHangPositions();
                }
                else
                {
                    SetDriftWeights(0);
                }
            }
        }
    }
    public float FractionTouchingGround { get; private set; }
    public bool AnyTouchingGround => FractionTouchingGround > 0;

    float SmoothingRate => FreeHanging ? freeHangSmoothingRate : stepSmoothingRate;

    public bool AnyGroundedLegsUnderextended(float threshold)
    {
        if (!AnyTouchingGround)
        {
            return false;
        }

        for (int i = 0; i < legTarget.Length; i++)
        {
            var l = legTarget[i];
            if (l.IsTouchingGround && l.LegExtensionFraction < threshold)
            {
                return true;
            }
        }

        return false;
    }

    public void UpdateAllLegs(float dt, GroundMap map)
    {
        var facingRight = bodyRb.transform.localScale.x > 0; 
        var sf = absoluteBodyGroundSpeed < speedCapMin ? 0 : absoluteBodyGroundSpeed / speedCapMax;

        dt *= timeScale;
        var speedScaledDt = sf * dt;
        dt = sf < 1 ? dt : speedScaledDt;
        var stepHeightSpeedMultiplier = Mathf.Min(sf, 1);
        var baseStepHeightMultiplier = (FreeHanging ? freeHangStepHeightMultiplier : this.baseStepHeightMultiplier) * stepHeightFraction;

        int count = 0;

        for (int i = 0; i < legCount; i++)
        {
            var t = timer[i];
            var l = legTarget[i];

            t.Update(bodyGroundSpeedSign * speedScaledDt);

            if (t.Stepping)
            {
                l.UpdateStep(dt, map, facingRight, FreeHanging,
                    baseStepHeightMultiplier, stepHeightSpeedMultiplier,
                    SmoothingRate, t.StateProgress,
                    strideMultiplier == 1 ? t.StepTime : strideMultiplier * t.StepTime,
                    strideMultiplier == 1 ? t.RestTime : strideMultiplier * t.RestTime);
            }
            else
            {
                l.UpdateRest(dt, map, facingRight, FreeHanging,
                    SmoothingRate, t.StateProgress,
                    strideMultiplier == 1 ? t.RestTime : strideMultiplier * t.RestTime);
            }

            if (l.IsTouchingGround)
            {
                count++;
            }
        }

        FractionTouchingGround = count * legCountInverse;
    }

    public void OnBodyChangedDirectionFreeHanging(Vector2 position0, Vector2 position1, Vector2 flipNormal)
    {
        for (int i = 0; i < legCount; i++)
        {
            legTarget[i].OnBodyChangedDirectionFreeHanging(position0, position1, flipNormal);
        }
    }

    public void LerpDriftWeights(float goal)
    {
        for (int i = 0; i < legCount; i++)
        {
            var l = legTarget[i];
            l.driftWeight = Mathf.Lerp(l.driftWeight, l.IsTouchingGround ? 0 : goal, driftWeightSmoothingRate);
        }
    }

    public void SetDriftWeights(float goal)
    {
        for (int i = 0; i < legCount; i++)
        {
            legTarget[i].driftWeight = goal;
        }
    }

    private void CacheFreeHangPositions()
    {
        for (int i = 0; i < legCount; i++)
        {
            legTarget[i].CacheFreeHangPosition();
        }
    }

    public void Initialize(float bodyPosGroundHeight, bool bodyFacingRight)
    {
        legCount = legTarget.Length;
        legCountInverse = 1 / (float)legCount;
        InitializeTimers();
        InitializeTargets();
        InitializeLegPositions(bodyFacingRight, bodyPosGroundHeight);
    }

    private void InitializeTargets()
    {
        for (int i = 0; i < legCount; i++)
        {
            legTarget[i].Initialize(ikSolver[i].GetChain(0));
        }
    }

    private void InitializeTimers()
    {
        float randomOffset = MathTools.RandomFloat(0, stepTime + restTime);
        timer = timeOffset.Select(o => new LegTimer(o + randomOffset, stepTime, restTime/*, RandomFreeHangPerturbation()*/)).ToArray();
    }

    private void InitializeLegPositions(bool bodyFacingRight, float preferredBodyPosGroundHeight)
    {
        Vector2 bodyPos = bodyRb.transform.position;
        Vector2 bodyMovementRight = bodyFacingRight ? bodyRb.transform.right : -bodyRb.transform.right;
        Vector2 bodyUp = bodyRb.transform.up;

        int count = 0;
        for (int i = 0; i < legCount; i++)
        {
            var t = timer[i];
            var l = legTarget[i];
            l.InitializePosition(preferredBodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, 
                t.Stepping, t.StateProgress, t.StepTime, t.RestTime);
            if (l.IsTouchingGround)
            {
                count++;
            }
        }

        FractionTouchingGround = count * legCountInverse;
    }
}
