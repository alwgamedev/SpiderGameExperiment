using UnityEngine;
using System.Linq;

public class PhysicsLegSynchronizer : MonoBehaviour
{
    [SerializeField] float restDistance;
    [SerializeField] float stepDistance;
    [SerializeField] float stepHeightSpeed0;//at or below this speed, stepHeight is zero
    [SerializeField] float stepHeightSpeed1;//at or above this speed, use full stepHeight
    [SerializeField] PhysicsBasedIKLeg[] leg;
    [SerializeField] PhysicsLegSettings stdSettings;
    [SerializeField] PhysicsLegSettings airborneSettings;
    [SerializeField] PhysicsLegSettings limpSettings;
    [SerializeField] float[] timeOffset;
    [SerializeField] int fabrikIterations;
    [SerializeField] float fabrikTolerance;
    [SerializeField] float groundContactRadius;

    LegTimer[] timer;
    LegState state;
    float legCountInverse;
    float groundContactRadiusSqrd;

    public float bodyGroundSpeedSign;
    public float absoluteBodyGroundSpeed;
    public float timeScale = 1;
    public float stepHeightFraction;
    public float strideMultiplier = 1;

    public enum LegState
    {
        standard, airborne, limp
    }

    public LegState State
    {
        get => state;
        set
        {
            if (value != state)
            {
                state = value;
                UpdateSettings();
            }
        }
    }

    public float FractionTouchingGround { get; private set; }
    public bool AnyTouchingGround => FractionTouchingGround > 0;

    public bool AnyGroundedLegsUnderextended(float threshold)
    {
        if (!AnyTouchingGround)
        {
            return false;
        }

        for (int i = 0; i < leg.Length; i++)
        {
            var l = leg[i];
            if (l.EffectorIsTouchingGround && l.LegExtensionFraction < threshold)
            {
                return true;
            }
        }

        return false;
    }

    public void UpdateAllLegs(float dt, GroundMap map)
    {
        var sf = absoluteBodyGroundSpeed < stepHeightSpeed0 ? 0 : absoluteBodyGroundSpeed / stepHeightSpeed1;

        dt *= timeScale;
        var speedScaledDt = sf * dt;
        dt = sf < 1 ? dt : speedScaledDt;
        var stepHeightSpeedMultiplier = Mathf.Min(sf, 1);

        int count = 0;

        for (int i = 0; i < leg.Length; i++)
        {
            var t = timer[i];
            var l = leg[i];

            t.Update(bodyGroundSpeedSign * speedScaledDt);

            if (t.Stepping)
            {
                l.UpdateTargetStepping(map, stepHeightSpeedMultiplier, stepHeightFraction, t.StateProgress, strideMultiplier * t.StepTime, strideMultiplier * t.RestTime);
            }
            else
            {
                l.UpdateTargetResting(map, t.StateProgress, strideMultiplier * t.RestTime);
            }

            l.UpdateJoints(map, fabrikIterations, fabrikTolerance, groundContactRadiusSqrd, dt);

            if (l.EffectorIsTouchingGround)
            {
                count++;
            }
        }

        FractionTouchingGround = count * legCountInverse;
    }

    public void OnBodyChangedDirection(Vector2 position0, Vector2 position1, Vector2 flipNormal)
    {
        for (int i = 0; i < leg.Length; i++)
        {
            leg[i].OnBodyChangedDirection(position0, position1, flipNormal);
        }
    }

    public void Initialize()
    {
        legCountInverse = 1f / leg.Length;
        groundContactRadiusSqrd = groundContactRadius * groundContactRadius;
        InitializeTimers();
        InitializeLegs();
        UpdateSettings();
    }

    private void InitializeTimers()
    {
        float randomOffset = MathTools.RandomFloat(0, stepDistance + restDistance);//add to all timers, to randomize initial position
        timer = timeOffset.Select(o => new LegTimer(o + randomOffset, stepDistance, restDistance)).ToArray();
    }

    private void InitializeLegs()
    {
        for (int i = 0; i < leg.Length; i++)
        {
            leg[i].Initialize();
        }
    }

    private void UpdateSettings()
    {
        ref var settings = ref Settings(state);
        for (int i = 0; i < leg.Length; i++)
        {
            leg[i].settings = settings;
        }
    }

    ref PhysicsLegSettings Settings(LegState state)
    {
        switch(state)
        {
            case LegState.airborne:
                return ref airborneSettings;
            case LegState.limp:
                return ref limpSettings;
            default:
                return ref stdSettings;
        }
    }
}
