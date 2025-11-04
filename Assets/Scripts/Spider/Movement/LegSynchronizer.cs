using UnityEngine;
using System.Linq;
using System;

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
    [SerializeField] SynchronizedLeg[] synchronizedLegs;

    class LegTimer
    {
        readonly float stepTime;
        readonly float restTime;

        bool stepping;
        float timer;
        float goalTime;

        public bool Stepping => stepping;
        public float Timer => timer;
        public float StepTime => stepTime;
        public float RestTime => restTime;
        public float StateProgress => stepping ? Timer / StepTime : Timer / RestTime;

        public LegTimer(float offset, float stepTime, float restTime)
        {
            this.stepTime = stepTime;
            this.restTime = restTime;
            timer = offset;

            var cycleTime = stepTime + restTime;
            while (timer < 0)
            {
                timer += cycleTime;
            }
            while (timer > cycleTime)
            {
                timer -= cycleTime;
            }

            if (timer >= stepTime)
            {
                timer -= stepTime;
                stepping = false;
                goalTime = restTime;
            }
            else
            {
                stepping = true;
                goalTime = stepTime;
            }
        }

        public void Update(float dt)
        {
            timer += dt;
            while (timer > goalTime)
            {
                timer -= goalTime;
                stepping = !stepping;
                goalTime = stepping ? stepTime : restTime;
            }
            while (timer < 0)
            {
                stepping = !stepping;
                goalTime = stepping ? stepTime : restTime;
                timer += goalTime;
            }
        }
    }

    LegTimer[] timers;
    bool freeHanging;
    Vector2 driftWeight;
    int legCount;
    int halfLegCount;
    float legCountInverse;

    public float bodyGroundSpeedSign;
    public float absoluteBodyGroundSpeed;
    public float timeScale = 1;
    public float stepHeightFraction;
    public float strideMultiplier = 1;
    
    public Vector2 DriftWeight
    {
        get => driftWeight;
        set
        {
            driftWeight = FreeHanging ? Vector2.Lerp(driftWeight, value, driftWeightSmoothingRate * Time.deltaTime) : value;
        }
    }

    public bool FreeHanging
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
            }
        }
    }

    float GoalSmoothingRate => FreeHanging ? freeHangSmoothingRate : stepSmoothingRate;

    public float FractionTouchingGround { get; private set; }
    public bool AnyTouchingGround => FractionTouchingGround > 0;
    public bool AnyFrontLegsTouchingGround { get; private set; }
    public bool AnyHindLegsTouchingGround { get; private set; }

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
        AnyFrontLegsTouchingGround = false;

        for (int i = 0; i < legCount; i++)
        {
            var t = timers[i];
            var l = synchronizedLegs[i].Leg;

            t.Update(bodyGroundSpeedSign * speedScaledDt);

            if (t.Stepping)
            {
                l.UpdateStep(dt, map, facingRight, FreeHanging,
                    baseStepHeightMultiplier, stepHeightSpeedMultiplier,
                    GoalSmoothingRate, t.StateProgress,
                    strideMultiplier == 1 ? t.StepTime : strideMultiplier * t.StepTime,
                    strideMultiplier == 1 ? t.RestTime : strideMultiplier * t.RestTime,
                    DriftWeight);
            }
            else
            {
                l.UpdateRest(dt, map, facingRight, FreeHanging,
                    GoalSmoothingRate, t.StateProgress,
                    strideMultiplier == 1 ? t.RestTime : strideMultiplier * t.RestTime,
                    DriftWeight);
            }

            if (l.IsTouchingGround)
            {
                count++;
            }
        }

        FractionTouchingGround = count * legCountInverse;

        AnyFrontLegsTouchingGround = false;
        AnyHindLegsTouchingGround = false;

        for (int i = 0; i < halfLegCount; i++)
        {
            if (synchronizedLegs[i].Leg.IsTouchingGround)
            {
                AnyFrontLegsTouchingGround = true;
                break;
            }
        }

        for (int i = halfLegCount; i < legCount; i++)
        {
            if (synchronizedLegs[i].Leg.IsTouchingGround)
            {
                AnyHindLegsTouchingGround = true;
                break;
            }
        }
    }

    public void Initialize(float bodyPosGroundHeight, bool bodyFacingRight, GroundMap groundMap)
    {
        legCount = synchronizedLegs.Length;
        halfLegCount = legCount / 2;
        legCountInverse = 1 / (float)legCount;
        InitializeTimers();
        InitializeLegPositions(bodyFacingRight, bodyPosGroundHeight);
    }

    private void CacheFreeHangPositions()
    {
        for (int i = 0; i < legCount; i++)
        {
            synchronizedLegs[i].Leg.CacheFreeHangPosition();
        }
    }

    public void OnBodyChangedDirectionFreeHanging(Vector2 position0, Vector2 position1, Vector2 flipNormal)
    {
        for (int i = 0; i < legCount; i++)
        {
            synchronizedLegs[i].Leg.OnBodyChangedDirectionFreeHanging(position0, position1, flipNormal);
        }
    }

    private void InitializeTimers()
    {
        float randomOffset = MathTools.RandomFloat(0, stepTime + restTime);
        timers = synchronizedLegs.Select(l => new LegTimer(l.TimeOffset + randomOffset, stepTime, restTime/*, RandomFreeHangPerturbation()*/)).ToArray();
    }

    private void InitializeLegPositions(bool bodyFacingRight, float preferredBodyPosGroundHeight)
    {
        Vector2 bodyPos = bodyRb.transform.position;
        Vector2 bodyMovementRight = bodyFacingRight ? bodyRb.transform.right : -bodyRb.transform.right;
        Vector2 bodyUp = bodyRb.transform.up;

        int count = 0;
        for (int i = 0; i < legCount; i++)
        {
            var t = timers[i];
            var l = synchronizedLegs[i].Leg;
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
