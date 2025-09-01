using System.Linq;
using UnityEngine;

public class LegSynchronizer : MonoBehaviour
{
    [SerializeField] Rigidbody2D bodyRb;
    [SerializeField] float restTime;
    [SerializeField] float stepTime;
    [SerializeField] float speedCapMin;//below this speed, legs will be on ground;
    [SerializeField] float speedCapMax;//at or above this speed, legs will use full step height;(may want to make this public so not indpt of mover)
    [SerializeField] float baseStepHeightMultiplier;
    [SerializeField] float stepSmoothingRate;
    [SerializeField] float restSmoothingRate;
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
        public float RestTime => restTime;// = max stride length
        public float StateProgress => stepping ? Timer / StepTime : Timer / RestTime;
        //public float StepProgress => Timer / StepTime;
        //public float RestProgress => Timer / RestTime;

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

            if (timer > stepTime)
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
            if (timer > goalTime)
            {
                timer -= goalTime;
                stepping = !stepping;
                goalTime = stepping ? stepTime : restTime;
            }
        }
    }

    LegTimer[] timers;
    bool staticMode;

    public float bodyGroundSpeed;
    public float preferredBodyPosGroundHeight;
    public float timeScale = 1;
    public float stepHeightFraction;
    public float outwardDrift;
    public Vector2 outwardDriftWeights;

    private void LateUpdate()
    {
        var facingRight = bodyRb.transform.localScale.x > 0;
        Vector2 bodyMovementRight = facingRight ? bodyRb.transform.right : - bodyRb.transform.right;
        Vector2 bodyUp = bodyRb.transform.up;
        Vector2 bodyPos = bodyRb.transform.position;
        var dt = Time.deltaTime * timeScale;

        var sf = bodyGroundSpeed / speedCapMax;
        var groundSpeedFrac = bodyGroundSpeed < speedCapMin ? 0 : sf;
        var baseStepHeightMultiplier = this.baseStepHeightMultiplier * stepHeightFraction;
        var stepHeightSpeedMultiplier = Mathf.Min(groundSpeedFrac, 1);
        var speedScaledDt = groundSpeedFrac * dt;
        dt = Mathf.Max(speedScaledDt, dt);

        if (staticMode)
        {
            var driftSpeedMultiplier = Mathf.Clamp(sf, 0.5f, 1.25f);
            for (int i = 0; i < timers.Length; i++)
            {
                var t = timers[i];
                var l = synchronizedLegs[i].Leg;

                t.Update(speedScaledDt);

                if (t.Stepping)
                {
                    l.UpdateStepStaticMode(dt,
                        preferredBodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, facingRight,
                        baseStepHeightMultiplier, stepHeightSpeedMultiplier,
                        stepSmoothingRate, t.StateProgress, t.StepTime, t.RestTime,
                        outwardDrift * driftSpeedMultiplier);
                }
                else
                {
                    l.UpdateRestStaticMode(dt, 
                        preferredBodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp,
                        restSmoothingRate, t.StateProgress, t.RestTime,
                        outwardDrift * driftSpeedMultiplier);
                }
            }
        }
        else
        {
            for (int i = 0; i < timers.Length; i++)
            {
                var t = timers[i];
                var l = synchronizedLegs[i].Leg;

                t.Update(speedScaledDt);

                if (t.Stepping)
                {
                    l.UpdateStep(dt, 
                        preferredBodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, facingRight,
                        baseStepHeightMultiplier, stepHeightSpeedMultiplier,
                        stepSmoothingRate, t.StateProgress, t.StepTime, t.RestTime);
                }
                else
                {
                    l.UpdateRest(dt, preferredBodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, 
                        restSmoothingRate, t.StateProgress, t.RestTime);
                }
            }
        }
    }

    public void EnterStaticMode()
    {
        if (!staticMode)
        {
            staticMode = true;
            for (int i = 0; i < synchronizedLegs.Length; i++)
            {
                synchronizedLegs[i].Leg.RandomizeDriftWeights();
            }
        }
    }

    public void EndStaticMode(/*bool bodyFacingRight, Vector2 bodyRight*/)
    {
        if (staticMode)
        {
            staticMode = false;
            //+other stuff that should only be done when exiting static mode
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
