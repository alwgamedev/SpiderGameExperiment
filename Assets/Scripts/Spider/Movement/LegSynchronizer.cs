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
    [SerializeField] Vector2 freeHangPerturbMin;
    [SerializeField] Vector2 freeHangPerturbMax;
    [SerializeField] float freeHangPerturbationSmoothingRate;
    [SerializeField] float freeHangingTimeScale;
    //[SerializeField] float freeHangPerturbSmoothingRate;
    [SerializeField] SynchronizedLeg[] synchronizedLegs;

    class LegTimer
    {
        readonly float stepTime;
        readonly float restTime;

        bool stepping;
        float timer;
        float goalTime;
        Vector2 freeHangPerturbation;
        Vector2 goalFreeHangPerturbation;

        public bool Stepping => stepping;
        public float Timer => timer;
        public float StepTime => stepTime;
        public float RestTime => restTime;
        public float StateProgress => stepping ? Timer / StepTime : Timer / RestTime;
        public Vector2 FreeHangPerturbation => freeHangPerturbation;

        public LegTimer(float offset, float stepTime, float restTime, Vector2 freeHangPerturbation)
        {
            this.stepTime = stepTime;
            this.restTime = restTime;
            timer = offset;
            this.freeHangPerturbation = freeHangPerturbation;

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

        public void UpdateFreeHanging(float dt, Func<Vector2> getRandomFreeHangPerturbation, float freeHangPerturbationSmoothingRate)
        {
            timer += dt;
            while (timer > goalTime)
            {
                timer -= goalTime;
                stepping = !stepping;
                goalTime = stepping ? stepTime : restTime;
                goalFreeHangPerturbation = getRandomFreeHangPerturbation();
            }
            while (timer < 0)
            {
                stepping = !stepping;
                goalTime = stepping ? stepTime : restTime;
                timer += goalTime;
                goalFreeHangPerturbation = getRandomFreeHangPerturbation();
            }

            freeHangPerturbation = Vector2.Lerp(FreeHangPerturbation, goalFreeHangPerturbation, freeHangPerturbationSmoothingRate * dt);
        }
    }

    LegTimer[] timers;

    public bool freeHanging;
    public float bodyGroundSpeedSign;
    public float absoluteBodyGroundSpeed;
    public float preferredBodyPosGroundHeight;
    public float timeScale = 1;
    public float stepHeightFraction;
    public float strideMultiplier = 1;
    public Vector2 driftWeight;

    public void UpdateAllLegs(float dt, GroundMap map)
    {
        var facingRight = bodyRb.transform.localScale.x > 0; 
        var sf = absoluteBodyGroundSpeed < speedCapMin ? 0 : absoluteBodyGroundSpeed / speedCapMax;
        //dt *= timeScale;


        if (freeHanging)
        {
            var d = -bodyRb.transform.up;
            var r = facingRight ? bodyRb.transform.right : -bodyRb.transform.right;
            var sDt = freeHangingTimeScale * (sf < 1 ? sf * dt : dt);//Min(sf * dt, dt)
            var sDt1 = sf < 1 ? dt : sf * dt;//Max(sf * dt, dt)

            for (int i = 0; i < synchronizedLegs.Length; i++)
            {
                var t = timers[i];
                t.UpdateFreeHanging(sDt, RandomFreeHangPerturbation, freeHangPerturbationSmoothingRate);
                synchronizedLegs[i].Leg.UpdateFreeHang(sDt1, map, Vector2.down, t.FreeHangPerturbation.ApplyTransformation(d, r), d, r, freeHangSmoothingRate);
            }
            return;
        }

        dt *= timeScale;
        var speedScaledDt = sf * dt;
        dt = sf < 1 ? dt : speedScaledDt;
        var stepHeightSpeedMultiplier = Mathf.Min(sf, 1);
        var baseStepHeightMultiplier = this.baseStepHeightMultiplier * stepHeightFraction;

        for (int i = 0; i < timers.Length; i++)
        {
            var t = timers[i];
            var l = synchronizedLegs[i].Leg;

            t.Update(bodyGroundSpeedSign * speedScaledDt);

            if (t.Stepping)
            {
                l.UpdateStep(dt, map, facingRight,
                    baseStepHeightMultiplier, stepHeightSpeedMultiplier,
                    stepSmoothingRate, t.StateProgress,
                    strideMultiplier == 1 ? t.StepTime : strideMultiplier * t.StepTime,
                    strideMultiplier == 1 ? t.RestTime : strideMultiplier * t.RestTime,
                    driftWeight);
            }
            else
            {
                l.UpdateRest(dt, map, facingRight,
                    stepSmoothingRate, t.StateProgress,
                    strideMultiplier == 1 ? t.RestTime : strideMultiplier * t.RestTime,
                    driftWeight);
            }
        }
    }

    //public void CacheFreeHangPositions()
    //{
    //    for (int i = 0; i < synchronizedLegs.Length; i++)
    //    {
    //        synchronizedLegs[i].Leg.OnBeginFreeHang();
    //    }
    //}

    public void Initialize(float bodyPosGroundHeight, bool bodyFacingRight)
    {
        InitializeTimers();
        preferredBodyPosGroundHeight = bodyPosGroundHeight;
        InitializeLegPositions(bodyFacingRight);
    }

    public Vector2 RandomFreeHangPerturbation()
    {
        return new Vector2(MathTools.RandomFloat(freeHangPerturbMin.x, freeHangPerturbMax.x), MathTools.RandomFloat(freeHangPerturbMin.y, freeHangPerturbMax.y));
    }

    private void InitializeTimers()
    {
        float randomOffset = MathTools.RandomFloat(0, stepTime + restTime);
        timers = synchronizedLegs.Select(l => new LegTimer(l.TimeOffset + randomOffset, stepTime, restTime, RandomFreeHangPerturbation())).ToArray();
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
