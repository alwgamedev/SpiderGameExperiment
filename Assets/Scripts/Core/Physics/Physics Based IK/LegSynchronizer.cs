using System;
using System.Linq;
using Unity.U2D.Physics;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public struct LegSynchronizer
{
    public UnityEvent[] footHitGround;
    public float[] reachForce;
    [NonSerialized] public float bodyGroundSpeedSign;
    [NonSerialized] public float absoluteBodyGroundSpeed;
    [NonSerialized] public float timeScale;
    [NonSerialized] public float stepHeightFraction;
    [NonSerialized] public float strideMultiplier;

    [SerializeField] JointedChain[] leg;
    [SerializeField] float[] stepMax;
    [SerializeField] float[] timeOffset;
    [SerializeField] int[] castDirectionIndex;
    [SerializeField] float restTime;
    [SerializeField] float stepTime;
    [SerializeField] float speed0;
    [SerializeField] float speed1;
    [SerializeField] float stepHeight;

    LegTimer[] legTimer;
    Vector2[] target;//for now these are world position; we may want them to be local position (relative to hip anchor)
    //float[] legLengthInverse;
    bool[] footIsTouchingGround;
    float legCountInverse;
    bool facingRight;

    public int LegCount => leg.Length;

    public bool FootIsTouchingGround(int i) => footIsTouchingGround[i];
    public bool AnyFootIsTouchingGround()
    {
        for (int i = 0; i < footIsTouchingGround.Length; i++)
        {
            if (footIsTouchingGround[i])
            {
                return true;
            }
        }

        return false;
    }
    public float FractionTouchingGround()
    {
        var ct = 0;
        for (int i = 0; i < footIsTouchingGround.Length; i++)
        {
            if (footIsTouchingGround[i])
            {
                ct++;
            }
        }

        return ((float)ct) / footIsTouchingGround.Length;
    }

    //initialize individual legs just to get around having to pass an array of different bodies
    public void Initialize()
    {
        legCountInverse = 1f / leg.Length;
        footIsTouchingGround = new bool[leg.Length];

        //initialize timers
        float randomOffset = MathTools.RandomFloat(0, stepTime + restTime);//add to all timers, to randomize initial position
        var s = stepTime;
        var r = restTime;
        legTimer = timeOffset.Select(o => new LegTimer(o + randomOffset, s, r)).ToArray();

        //legLengthInverse = new float[leg.Length];
        //for (int i = 0; i < leg.Length; i++)
        //{
        //    var l = 0f;
        //    for (int j = 0; j < leg[i].body.Length - 1; j++)
        //    {
        //        l += Vector2.Distance(leg[i].body[j].position, leg[i].body[j + 1].position);
        //    }
        //    l += Vector2.Distance(leg[i].body[^1].position, leg[i].EffectorPosition);
        //    legLengthInverse[i] = 1 / l;
        //}
    }

    public void InitializeLeg(int i, PhysicsBody body)
    {
        leg[i].Initialize(body);
        target[i] = leg[i].EffectorPosition;
    }

    public void SetOrientation(bool facingRight)
    {
        //a) all bodies undergo same world reflection as main body, EXCEPT we use different convention for rotation (i.e. rotation reflects over same axis as position, keeping orientation)
        //b) reflect hip anchors to other side of their anchored bodies
        //c) negate joint angle limits

        this.facingRight = facingRight;
    }

    public void EnableAngleLimit(int i, bool val)
    {
        for (int j = 0; j < leg[i].joint.Length; j++)
        {
            leg[i].joint[j].enableLimit = val;
        }
    }

    //2do:
    //public bool HasContact -- not sure if should be every link in the leg, just the last link, or just a circle around the foot?
    //maybe if we make it the entire last link, then we can relax/make more natural the groundedness condition in spider
    //e.g. we've got some weird "AnyGroundedLegsUnderextended" for free hanging -- we probably don't have to try so hard now
    //(plus settings are much less complicated now, so spider state doesn't matter as much)
    //then we just need three main things:
    //a) update ik target
    //b) pull leg towards target
    //c) main legSynch update that updates all the legs & timers
    //d) on change direction we will have to reflect the hip anchors and joint positions across body transform (maybe even reflect velocity & angular velocity)
        //oof we'll also have to negate angle limits

    public void UpdateAllLegs(float dt, GroundMap map, Vector2[] castDirection, bool grounded)
    {
        var speedFraction = absoluteBodyGroundSpeed < speed0 ? 0 : absoluteBodyGroundSpeed / speed1;
        var speedScaledDt = timeScale * speedFraction * dt;
        var stepHeightSpeedMultiplier = grounded ? Mathf.Min(speedFraction, 1) : 1;

        for (int i = 0; i < leg.Length; i++)
        {
            UpdateLeg(i, dt, speedScaledDt, stepHeightSpeedMultiplier, map, castDirection[castDirectionIndex[i]]);
            footIsTouchingGround[i] = leg[i].body[^1].GetContacts().Length > 0;
        }
    }

    private void UpdateLeg(int i, float dt, float speedScaledDt, float stepHeightSpeedMultiplier, GroundMap map, Vector2 castDirection)
    {
        ref var t = ref legTimer[i];
        ref var l = ref leg[i];

        t.Update(bodyGroundSpeedSign * speedScaledDt);

        if (t.Stepping)
        {
            UpdateTargetStepping(i, map, castDirection,
                stepHeightSpeedMultiplier, stepHeightFraction, t.StateProgress,
                strideMultiplier * t.StepTime, strideMultiplier * t.RestTime);
        }
        else
        {
            UpdateTargetResting(i, map, castDirection,
                t.StateProgress, strideMultiplier * t.RestTime);
        }

        //then pull towards target
        var a = reachForce[i] * (target[i] - l.EffectorPosition);
        l.AccelerateUniformly(a);
        //^2do: better method for pull leg (maybe only pull uniformly when pulling up and pull just effector when pulling down? or always just do effector... we can see how it looks and experiment)
    }


    //TARGETING

    public void UpdateTargetStepping(int i, GroundMap map, Vector2 castDir,
       float stepHeightSpeedMultiplier, float stepHeightFraction, float stepProgress, float stepDistance,
       float restDistance)
    {
        var hipPosition = leg[i].body[0].position;

        var dot = Vector2.Dot(hipPosition - (Vector2)map.Origin, map.OriginRight);
        float h;
        if (!map.LineCastToGround(hipPosition, castDir, out var p) || Mathf.Abs(p.arcLengthPosition) < Mathf.Abs(dot))
        {
            h = dot;
        }
        else
        {
            h = p.arcLengthPosition;
        }

        var stepStart = GetStepStart(i, map, h, facingRight, stepProgress, stepDistance, restDistance);
        var stepGoal = GetStepGoal(i, map, h, facingRight, 0, restDistance);
        var stepRight = stepGoal - stepStart;
        var stepUp = facingRight ? 0.5f * stepRight.CCWPerp() : 0.5f * stepRight.CWPerp();

        //parabola instead of trig fcts
        var newTargetPos = stepStart + stepProgress * stepRight + 4 * stepProgress * (1 - stepProgress) * stepHeightFraction * stepHeight * stepUp;

        if (stepHeightSpeedMultiplier < 1)
        {
            var g = map.ProjectOntoGroundByArcLength(newTargetPos, out _, out _);
            newTargetPos = Vector2.Lerp(g, newTargetPos, stepHeightSpeedMultiplier);
        }

        target[i] = newTargetPos;
    }

    public void UpdateTargetResting(int i, GroundMap map, Vector2 castDir,
        float restProgress, float restDistance)
    {
        var hipPosition = leg[i].body[0].position;
        var dot = Vector2.Dot(hipPosition - (Vector2)map.Origin, map.OriginRight);

        float h;
        if (!map.LineCastToGround(hipPosition, castDir, out var p) || Mathf.Abs(p.arcLengthPosition) < Mathf.Abs(dot))
        {
            h = dot;
        }
        else
        {
            h = p.arcLengthPosition;
        }

        target[i] = GetStepGoal(i,map, h, facingRight, restProgress, restDistance);
    }

    //public void ClampTargetPosition(int i, GroundMap map, float maxExtensionFraction)
    //{
    //    var hipPos = leg[i].body[0].position;
    //    var d = target[i] - hipPos;
    //    var f = d.sqrMagnitude * totalLengthInverse * totalLengthInverse;
    //    if (f > maxExtensionFraction * maxExtensionFraction)
    //    {
    //        var p = hipPos + maxExtensionFraction / Mathf.Sqrt(f) * d;
    //        target[i] = (Vector2)map.TrueClosestPoint(new float2(p.x, p.y), out _, out _, out _);
    //    }
    //}

    //private void UpdateGroundContact(GroundMap groundMap, float groundContactRadius, float collisionResponse, float dt)
    //{
    //    var wasTouchingGround = EffectorIsTouchingGround;
    //    Vector2 q = chain[^1].position;
    //    q -= (Vector2)groundMap.TrueClosestPoint(q, out _, out var n, out var hitGround);

    //    var l = Vector2.Dot(q, n);
    //    EffectorIsTouchingGround = hitGround && l < groundContactRadius;
    //    if (EffectorIsTouchingGround)
    //    {
    //        if (l < 0)
    //        {
    //            var a = -dt * collisionResponse * l * n;
    //            PhysicsBasedIK.ApplyForceUpChain(chain, inverseLength, angularVelocity, a, chain.Length - 1, null, true);
    //        }

    //        if (!wasTouchingGround)
    //        {
    //            HitGround.Invoke();
    //        }
    //    }
    //}

    private Vector2 GetStepStart(int i, GroundMap map, float h, bool bodyFacingRight, float stepProgress, float stepDistance, float restDistance/*, float stepMax*/)
    {
        h = bodyFacingRight ? h + StepStartHorizontalOffset(i, stepProgress, stepDistance, restDistance/*, stepMax*/)
            : h - StepStartHorizontalOffset(i, stepProgress, stepDistance, restDistance/*, stepMax*/);
        return map.PointFromCenterByArcLength(h, out _, out _);
        //return map.PointFromCenterByIntervalWidth(h);
    }

    private Vector2 GetStepGoal(int i, GroundMap map, float h, bool bodyFacingRight, float restProgress, float restDistance)
    {
        h += bodyFacingRight ? StepGoalHorizontalOffset(i, restProgress, restDistance) 
            : -StepGoalHorizontalOffset(i, restProgress, restDistance);
        return map.PointFromCenterByArcLength(h, out var n, out _);
    }

    private float StepGoalHorizontalOffset(int i, float restProgress, float restDistance/*, float stepMax*/)
    {
        return stepMax[i] - restProgress * restDistance;
    }

    private float StepStartHorizontalOffset(int i, float stepProgress, float stepDistance, float restDistance/*, float stepMax*/)
    {
        return stepMax[i] - restDistance - stepProgress * stepDistance;
    }
}