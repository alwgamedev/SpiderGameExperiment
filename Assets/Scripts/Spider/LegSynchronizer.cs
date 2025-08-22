using System.Linq;
using UnityEngine;

public class LegSynchronizer : MonoBehaviour
{
    [SerializeField] float restTime;
    [SerializeField] float stepTime;
    [SerializeField] float speedCapMin;//below this speed, legs will be on ground;
    [SerializeField] float speedCapMax;//at or above this speed, legs will use full step height;(may want to make this public so not indpt of mover)
    [SerializeField] float baseStepHeightMultiplier;
    [SerializeField] float stepSmoothingRate;
    [SerializeField] float restSmoothingRate;
    [SerializeField] float footRotationSpeed;
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
        public float StepProgress => Timer / StepTime;

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
        public bool Update(float dt)
        {
            timer += dt;
            if (timer > goalTime)
            {
                timer -= goalTime;
                stepping = !stepping;
                if (stepping)
                {
                    goalTime = stepTime;
                    return true;
                }

                goalTime = restTime;
                return false;
            }

            return false;
        }
    }

    Rigidbody2D body;
    LegTimer[] timers;

    public float bodyGroundSpeed;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        Initialize();        
    }

    private void LateUpdate()
    {
        var facingRight = body.transform.localScale.x > 0;
        var bodyRight = body.transform.right;
        if (!facingRight)
        {
            bodyRight *= -1;
        }
        var bodyUp = body.transform.up;
        var dt = Time.deltaTime;

        var speedFrac = bodyGroundSpeed < speedCapMin ? 0 : bodyGroundSpeed / speedCapMax;
        var stepHeightSpeedMultiplier = Mathf.Min(speedFrac, 1);
        var speedScaledDt = speedFrac * dt;
        dt = Mathf.Max(speedScaledDt, dt);

        for (int i = 0; i < timers.Length; i++)
        {
            var t = timers[i];
            var l = synchronizedLegs[i].Leg;
            if (t.Update(speedScaledDt))
            {
                l.BeginStep(body);
            }
            if (t.Stepping)
            {
                l.UpdateStep(dt, t.StepProgress, bodyRight, bodyUp, facingRight,
                    stepHeightSpeedMultiplier, baseStepHeightMultiplier,
                    stepSmoothingRate, footRotationSpeed);
            }
            else
            {
                l.UpdateRest(dt, restSmoothingRate);
            }
        }
    }

    public void OnBodyChangedDirection()
    {
        Vector2 p = body.transform.position;
        Vector2 tRight = body.transform.right;
        Vector2 tUp = body.transform.up;

        foreach (var l in synchronizedLegs)
        {
            l.Leg.OnBodyChangedDirection(p, tRight, tUp);
        }
    }

    private void Initialize()
    {
        float randomOffset = MathTools.RandomFloat(0, stepTime + restTime);
        timers = synchronizedLegs.Select(l => new LegTimer(l.TimeOffset + randomOffset, stepTime, restTime)).ToArray();
    }
}
