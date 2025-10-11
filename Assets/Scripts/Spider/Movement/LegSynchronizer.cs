using System.Linq;
using UnityEngine;

public class LegSynchronizer : MonoBehaviour
{
    [SerializeField] Rigidbody2D bodyRb;
    [SerializeField] float restTime;
    [SerializeField] float stepTime;
    [SerializeField] float speedCapMin;//below this speed, legs will be on ground (no step height);
    [SerializeField] float speedCapMax;//at or above this speed, legs will use full step height;(may want to make this public so not indpt of mover)
    [SerializeField] float baseStepHeightMultiplier;
    [SerializeField] float stepSmoothingRate;
    //[SerializeField] float speedFracSmoothingRate;
    //[SerializeField] float restSmoothingRate;
    //[SerializeField] float staticModeGroundCollisionSmoothingRate;
    //[SerializeField] float extensionSmoothingRate;
    //[SerializeField] float groundCollisionSmoothingRate;
    //[SerializeField] float staticModeGroundDetectionOffsetRate;
    //[SerializeField] float staticModeGroundDectectionOffsetMax;
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
    //bool staticMode;

    public float bodyGroundSpeed;
    public float preferredBodyPosGroundHeight;
    public float timeScale = 1;
    public float stepHeightFraction;
    public float outwardDrift;
    public Vector2 outwardDriftWeights;

    public void UpdateAllLegs(float dt, GroundMap map)
    {
        var facingRight = bodyRb.transform.localScale.x > 0;
        dt *= timeScale;

        var sf = bodyGroundSpeed / speedCapMax;
        var groundSpeedFrac = bodyGroundSpeed < speedCapMin ? 0 : sf;

        var baseStepHeightMultiplier = this.baseStepHeightMultiplier * stepHeightFraction;
        var stepHeightSpeedMultiplier = Mathf.Min(groundSpeedFrac, 1);
        var speedScaledDt = groundSpeedFrac * dt;
        dt = Mathf.Max(speedScaledDt, dt);

        for (int i = 0; i < timers.Length; i++)
        {
            var t = timers[i];
            var l = synchronizedLegs[i].Leg;

            t.Update(speedScaledDt);

            if (t.Stepping)
            {
                l.UpdateStep(dt, map, facingRight,
                    baseStepHeightMultiplier, stepHeightSpeedMultiplier,
                    stepSmoothingRate, t.StateProgress, t.StepTime, t.RestTime);
            }
            else
            {
                l.UpdateRest(dt, map, facingRight,
                    stepSmoothingRate, t.StateProgress, t.RestTime);
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
