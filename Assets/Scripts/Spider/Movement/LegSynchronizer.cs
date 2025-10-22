using System.Linq;
using UnityEngine;

public class LegSynchronizer : MonoBehaviour
{
    [SerializeField] Rigidbody2D bodyRb;
    [SerializeField] float restTime;
    [SerializeField] float stepTime;
    [SerializeField] float speedCapMin;//at or below this speed, stepHeight is zero
    [SerializeField] float speedCapMax;//at or above this speed, use full stepHeight
    [SerializeField] float baseStepHeightMultiplier;
    [SerializeField] float stepSmoothingRate;
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

        //returns whether stepping just turned to true
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

    public float bodyGroundSpeedSign;
    public float absoluteBodyGroundSpeed;
    public float preferredBodyPosGroundHeight;
    public float timeScale = 1;
    public float stepHeightFraction;
    public float strideMultiplier = 1;
    public float driftWeight;
    //public Vector2 outwardDriftWeights;

    public void UpdateAllLegs(float dt, GroundMap map)
    {
        var facingRight = bodyRb.transform.localScale.x > 0;
        dt *= timeScale;

        var sf = absoluteBodyGroundSpeed < speedCapMin ? 0 : absoluteBodyGroundSpeed / speedCapMax;
        var baseStepHeightMultiplier = this.baseStepHeightMultiplier * stepHeightFraction;
        var stepHeightSpeedMultiplier = Mathf.Min(sf, 1);
        var speedScaledDt = sf * dt;
        dt = Mathf.Max(speedScaledDt, dt);

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

    public void Initialize(float bodyPosGroundHeight, bool bodyFacingRight)
    {
        InitializeTimers();
        preferredBodyPosGroundHeight = bodyPosGroundHeight;
        InitializeLegPositions(bodyFacingRight);
    }

    private void InitializeTimers()
    {
        float randomOffset = MathTools.RandomFloat(0, stepTime + restTime);
        timers = synchronizedLegs.Select(l => new LegTimer(l.TimeOffset + randomOffset, stepTime, restTime)).ToArray();
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
