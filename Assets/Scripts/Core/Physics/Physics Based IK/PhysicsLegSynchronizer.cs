using UnityEngine;
using System.Linq;
using System;

public class PhysicsLegSynchronizer : MonoBehaviour
{
    [SerializeField] float restDistance;
    [SerializeField] float stepDistance;
    [SerializeField] float stepHeightSpeed0;//at or below this speed, stepHeight is zero
    [SerializeField] float stepHeightSpeed1;//at or above this speed, use full stepHeight
    [SerializeField] PhysicsBasedIKLeg[] leg;
    [SerializeField] Transform[] castDirectionSource;
    [SerializeField] int[] castDirectionSourceIndex;
    [SerializeField] PhysicsLegSettings[] stdSettings;
    [SerializeField] PhysicsLegSettings[] jumpSettings;
    [SerializeField] PhysicsLegSettings[] freefallSettings;
    [SerializeField] PhysicsLegSettings[] limpSettings;
    [SerializeField] float[] timeOffset;
    [SerializeField] int fabrikIterations;
    [SerializeField] float fabrikTolerance;
    [SerializeField] float reachTolerance;
    [SerializeField] float groundContactRadius;
    [SerializeField] float collisionResponse;
    [SerializeField] float maxExtensionFraction;
    [SerializeField] float maxAngularVelocity;

    LegTimer[] timer;
    LegState state;
    float legCountInverse;

    [NonSerialized] public float bodyGroundSpeedSign;
    [NonSerialized] public float absoluteBodyGroundSpeed;
    [NonSerialized] public float timeScale = 1;
    [NonSerialized] public float stepHeightFraction;
    [NonSerialized] public float strideMultiplier = 1;

    float fabrikToleranceSqrd;
    float reachToleranceSqrd;

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

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            fabrikToleranceSqrd = fabrikTolerance * fabrikTolerance;
            reachToleranceSqrd = reachToleranceSqrd * reachTolerance;
            UpdateSettings();
        }
    }

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

    public void UpdateAllLegs(float dt, GroundMap map, bool grounded, float simulateContactWeight = 0)
    {
        var speedFraction = absoluteBodyGroundSpeed < stepHeightSpeed0 ? 0 : absoluteBodyGroundSpeed / stepHeightSpeed1;
        var speedScaledDt = timeScale * speedFraction * dt;
        var stepHeightSpeedMultiplier = grounded ? Mathf.Min(speedFraction, 1) : 1;
        var count = 0;

        for (int i = 0; i < leg.Length; i++)
        {
            var t = timer[i];
            var l = leg[i];

            t.Update(bodyGroundSpeedSign * speedScaledDt);

            if (t.Stepping)
            {
                l.UpdateTargetStepping(map, -castDirectionSource[castDirectionSourceIndex[i]].up,
                    stepHeightSpeedMultiplier, stepHeightFraction, t.StateProgress, 
                    strideMultiplier * t.StepTime, strideMultiplier * t.RestTime);
            }
            else
            {
                l.UpdateTargetResting(map, -castDirectionSource[castDirectionSourceIndex[i]].up,
                    t.StateProgress, strideMultiplier * t.RestTime);
            }

            if (grounded && speedFraction == 0)
            {
                l.ClampTargetPosition(map, maxExtensionFraction);
            }

            l.UpdateJoints(map, dt, fabrikIterations, fabrikToleranceSqrd, reachToleranceSqrd,
                groundContactRadius, collisionResponse, maxAngularVelocity, simulateContactWeight);

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
        fabrikToleranceSqrd = fabrikTolerance * fabrikTolerance;
        reachToleranceSqrd = reachTolerance * reachTolerance;
        InitializeTimers();
        InitializeLegs();
    }

    private void InitializeTimers()
    {
        float randomOffset = MathTools.RandomFloat(0, stepDistance + restDistance);//add to all timers, to randomize initial position
        timer = timeOffset.Select(o => new LegTimer(o + randomOffset, stepDistance, restDistance)).ToArray();
    }

    private void InitializeLegs()
    {
        var settings = NoContactSettings(state);
        for (int i = 0; i < leg.Length; i++)
        {
            leg[i].Initialize();
            leg[i].contactSettings = stdSettings[i];//this is the only time contact settings get set! do not delete! (update settings only does noContactSettings)
            leg[i].noContactSettings = settings[i];
        }
    }

    private void UpdateSettings()
    {
        var settings = NoContactSettings(state);
        for (int i = 0; i < leg.Length; i++)
        {
            leg[i].noContactSettings = settings[i];
        }
    }

    PhysicsLegSettings[] NoContactSettings(LegState state)
    {
        switch(state)
        {
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
