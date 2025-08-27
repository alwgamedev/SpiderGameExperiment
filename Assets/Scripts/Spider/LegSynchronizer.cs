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
        public float RestTime => restTime;// = max stride length
        public float StepProgress => Timer / StepTime;
        public float RestProgress => Timer / RestTime;

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
    bool staticMode;

    public float bodyGroundSpeed;
    public float preferredBodyPosGroundHeight;
    public float timeScale = 1;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    //private void Start()
    //{
    //    InitializeTimers();
    //    RepositionAllLegs(body.transform.right, body.transform.localScale.x > 0);
    //}

    private void LateUpdate()
    {
        var facingRight = body.transform.localScale.x > 0;
        Vector2 bodyMovementRight = facingRight ? body.transform.right : - body.transform.right;
        Vector2 bodyUp = body.transform.up;
        Vector2 bodyPos = body.transform.position;
        var dt = Time.deltaTime * timeScale;

        var speedFrac = bodyGroundSpeed < speedCapMin ? 0 : bodyGroundSpeed / speedCapMax;
        var stepHeightSpeedMultiplier = Mathf.Min(speedFrac, 1);
        var speedScaledDt = speedFrac * dt;
        dt = Mathf.Max(speedScaledDt, dt);

        if (staticMode)
        {
            for (int i = 0; i < timers.Length; i++)
            {
                var t = timers[i];
                var l = synchronizedLegs[i].Leg;
                t.Update(speedScaledDt);
                //if (t.Update(speedScaledDt))
                //{
                //    l.BeginStepStaticMode(bodyPos, bodyMovementRight, bodyUp, t.RestTime);
                //}
                if (t.Stepping)
                {
                    l.UpdateStepStaticMode(dt, t.StepProgress, preferredBodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, facingRight, t.RestTime,
                        baseStepHeightMultiplier, stepHeightSpeedMultiplier,
                        stepSmoothingRate, footRotationSpeed);
                }
                else
                {
                    l.UpdateRestStaticMode(dt, t.RestProgress, preferredBodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, t.RestTime, restSmoothingRate);
                }
            }
        }
        else
        {
            for (int i = 0; i < timers.Length; i++)
            {
                var t = timers[i];
                var l = synchronizedLegs[i].Leg;
                if (t.Update(speedScaledDt))
                {
                    l.BeginStep(preferredBodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp);
                }
                if (t.Stepping)
                {
                    l.UpdateStep(dt, t.StepProgress, preferredBodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, facingRight,
                        baseStepHeightMultiplier, stepHeightSpeedMultiplier, 
                        stepSmoothingRate, footRotationSpeed);
                }
                else
                {
                    l.UpdateRest(dt, restSmoothingRate);
                }
            }
        }
    }

    public void EnterStaticMode()
    {
        staticMode = true;
        //if (!staticMode)
        //{
        //    var p = body.transform.position;
        //    for (int i = 0; i < synchronizedLegs.Length; i++)
        //    {
        //        synchronizedLegs[i].Leg.OnEnterStaticMode(p);
        //    }
        //    staticMode = true;
        //}
    }

    public void EndStaticMode(bool bodyFacingRight, Vector2 bodyRight)
    {
        if (staticMode)
        {
            staticMode = false;
            RepositionAllLegs(bodyFacingRight, bodyRight);
            //LateUpdate();
        }
    }

    //yes, legSynch has access to the rigidbody (so could get bodyRight) but you could e.g. pass groundDirection here instead
    public void RepositionAllLegs(bool bodyFacingRight, Vector2 bodyRight)
    {
        //Vector2 up = right.CCWPerp();
        Vector2 bPos = body.transform.position;
        Vector2 bodyMovementRight = bodyFacingRight ? bodyRight : -bodyRight;
        Vector2 bodyUp = bodyRight.CCWPerp();
        for (int i = 0; i < synchronizedLegs.Length; i++)
        {
            var t = timers[i];
            if (t.Stepping)
            {
                synchronizedLegs[i].Leg.RepositionStepping(preferredBodyPosGroundHeight, bPos, bodyMovementRight, bodyUp, t.RestTime);
            }
            else
            {
                synchronizedLegs[i].Leg.RepositionResting(preferredBodyPosGroundHeight, bPos, bodyMovementRight, bodyUp, t.RestProgress, t.RestTime); 
            }
        }
    }

    public void OnBodyChangedDirection()
    {
        Vector2 p = body.transform.position;
        Vector2 tRight = body.transform.right;
        Vector2 tUp = body.transform.up;

        for (int i = 0; i < synchronizedLegs.Length; i++)
        {
            synchronizedLegs[i].Leg.OnBodyChangedDirection(p, tRight, tUp);
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
        Vector2 bodyPos = body.transform.position;
        Vector2 bodyMovementRight = bodyFacingRight ? body.transform.right : -body.transform.right;
        Vector2 bodyUp = body.transform.up;
        for (int i = 0; i < synchronizedLegs.Length; i++)
        {
            var t = timers[i];
            var restProgress = t.Stepping ? 1 - t.StepProgress : t.RestProgress;
            synchronizedLegs[i].Leg.InitializePosition(preferredBodyPosGroundHeight, bodyPos, bodyMovementRight, bodyUp, restProgress, t.RestTime);
        }
    }
}
