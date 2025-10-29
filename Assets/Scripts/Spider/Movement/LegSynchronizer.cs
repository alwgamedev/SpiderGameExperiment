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

    public float bodyGroundSpeedSign;
    public float absoluteBodyGroundSpeed;
    public float preferredBodyPosGroundHeight;
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

    float SmoothingRate => FreeHanging ? freeHangSmoothingRate : stepSmoothingRate;

    public void UpdateAllLegs(float dt, GroundMap map)
    {
        var facingRight = bodyRb.transform.localScale.x > 0; 
        var sf = absoluteBodyGroundSpeed < speedCapMin ? 0 : absoluteBodyGroundSpeed / speedCapMax;

        dt *= timeScale;
        var speedScaledDt = sf * dt;
        dt = sf < 1 ? dt : speedScaledDt;
        var stepHeightSpeedMultiplier = Mathf.Min(sf, 1);
        var baseStepHeightMultiplier = (FreeHanging ? freeHangStepHeightMultiplier : this.baseStepHeightMultiplier) * stepHeightFraction;

        for (int i = 0; i < timers.Length; i++)
        {
            var t = timers[i];
            var l = synchronizedLegs[i].Leg;

            t.Update(bodyGroundSpeedSign * speedScaledDt);

            if (t.Stepping)
            {
                l.UpdateStep(dt, map, facingRight, FreeHanging,
                    baseStepHeightMultiplier, stepHeightSpeedMultiplier,
                    SmoothingRate, t.StateProgress,
                    strideMultiplier == 1 ? t.StepTime : strideMultiplier * t.StepTime,
                    strideMultiplier == 1 ? t.RestTime : strideMultiplier * t.RestTime,
                    DriftWeight);
            }
            else
            {
                l.UpdateRest(dt, map, facingRight, FreeHanging,
                    SmoothingRate, t.StateProgress,
                    strideMultiplier == 1 ? t.RestTime : strideMultiplier * t.RestTime,
                    DriftWeight);
            }
        }
    }

    public void Initialize(float bodyPosGroundHeight, bool bodyFacingRight)
    {
        InitializeTimers();
        preferredBodyPosGroundHeight = bodyPosGroundHeight;
        InitializeLegPositions(bodyFacingRight);
    }

    private void CacheFreeHangPositions()
    {
        for (int i = 0; i < synchronizedLegs.Length; i++)
        {
            synchronizedLegs[i].Leg.CacheFreeHangPosition();
        }
    }

    public void OnBodyChangedDirectionFreeHanging(Vector2 position0, Vector2 position1, Vector2 flipNormal)
    {
        for (int i = 0; i < synchronizedLegs.Length; i++)
        {
            synchronizedLegs[i].Leg.OnBodyChangedDirectionFreeHanging(position0, position1, flipNormal);
        }
    }

    private void InitializeTimers()
    {
        float randomOffset = MathTools.RandomFloat(0, stepTime + restTime);
        timers = synchronizedLegs.Select(l => new LegTimer(l.TimeOffset + randomOffset, stepTime, restTime/*, RandomFreeHangPerturbation()*/)).ToArray();
    }

    private void InitializeLegPositions(bool bodyFacingRight)
    {
        Vector2 bodyPos = bodyRb.transform.position;
        Vector2 bodyMovementRight = bodyFacingRight ? bodyRb.transform.right : -bodyRb.transform.right;
        Vector2 bodyUp = bodyRb.transform.up;

        for (int i = 0; i < synchronizedLegs.Length; i++)
        {
            var t = timers[i];
            synchronizedLegs[i].Leg.InitializePosition(preferredBodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, 
                t.Stepping, t.StateProgress, t.StepTime, t.RestTime);
        }
    }
}
