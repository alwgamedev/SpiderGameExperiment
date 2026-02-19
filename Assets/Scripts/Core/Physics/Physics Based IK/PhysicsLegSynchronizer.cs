using UnityEngine;
using System.Linq;

public class PhysicsLegSynchronizer : MonoBehaviour
{
    [SerializeField] float restDistance;
    [SerializeField] float stepDistance;
    [SerializeField] float stepHeightSpeed0;//at or below this speed, stepHeight is zero
    [SerializeField] float stepHeightSpeed1;//at or above this speed, use full stepHeight
    [SerializeField] PhysicsBasedIKLeg[] leg;
    [SerializeField] PhysicsLegSettings[] stdSettings;
    [SerializeField] PhysicsLegSettings[] jumpSettings;
    [SerializeField] PhysicsLegSettings[] freefallSettings;
    [SerializeField] PhysicsLegSettings[] limpSettings;
    [SerializeField] float[] timeOffset;
    [SerializeField] int fabrikIterations;
    [SerializeField] float fabrikTolerance;
    [SerializeField] float targetSmoothingRate;
    [SerializeField] float groundContactRadius;
    [SerializeField] float collisionResponse;

    LegTimer[] timer;
    LegState state;
    float legCountInverse;

    public float bodyGroundSpeedSign;
    public float absoluteBodyGroundSpeed;
    public float timeScale = 1;
    public float stepHeightFraction;
    public float reachFraction;
    public float strideMultiplier = 1;

    public enum LegState
    {
        standard, jumping, freefall, limp
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

    public void UpdateAllLegs(float dt, GroundMap map, float simulateContactWeight = 0)
    {
        var speedFraction = absoluteBodyGroundSpeed < stepHeightSpeed0 ? 0 : absoluteBodyGroundSpeed / stepHeightSpeed1;
        var speedScaledDt = timeScale * speedFraction * dt;
        var stepHeightSpeedMultiplier = Mathf.Min(speedFraction, 1);
        var reachFraction = Mathf.Lerp(this.reachFraction, 1, simulateContactWeight);
        var count = 0;

        for (int i = 0; i < leg.Length; i++)
        {
            var t = timer[i];
            var l = leg[i];

            t.Update(bodyGroundSpeedSign * speedScaledDt);

            if (t.Stepping)
            {
                l.UpdateTargetStepping(map, dt, stepHeightSpeedMultiplier, stepHeightFraction, /*reachFraction,*/ t.StateProgress, 
                    strideMultiplier * t.StepTime, strideMultiplier * t.RestTime, targetSmoothingRate);
            }
            else
            {
                l.UpdateTargetResting(map, dt, reachFraction, t.StateProgress, strideMultiplier * t.RestTime, targetSmoothingRate);
            }

            l.UpdateJoints(map, fabrikIterations, fabrikTolerance, groundContactRadius, collisionResponse, dt, simulateContactWeight);

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
            leg[i].contactSettings = stdSettings[i];
        }
    }

    private void UpdateSettings()
    {
        var settings = Settings(state);
        for (int i = 0; i < leg.Length; i++)
        {
            leg[i].noContactSettings = settings[i];
        }
    }

    PhysicsLegSettings[] Settings(LegState state)
    {
        switch(state)
        {
            //case LegState.airborne:
            //    return airborneSettings;
            case LegState.jumping:
                return jumpSettings;
            case LegState.freefall:
                return freefallSettings;
            case LegState.limp:
                return limpSettings;
            default:
                return stdSettings;
        }
    }
}
