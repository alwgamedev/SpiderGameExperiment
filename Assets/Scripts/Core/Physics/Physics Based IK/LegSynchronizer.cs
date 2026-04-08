using System;
using System.Linq;
using Unity.U2D.Physics;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public class LegSynchronizer
{
    public UnityEvent[] footHitGround;
    public float[] reachForce;
    internal float bodyGroundSpeedSign;
    internal float absoluteBodyGroundSpeed;
    internal float timeScale;
    internal float stepHeightFraction;
    internal float strideMultiplier;

    [SerializeField] JointedChainDefinition chainDef;
    [SerializeField] JointedChainSettings[] chainSettings;
    [SerializeField] ArrayContainer<Transform>[] chainTransform;
    [SerializeField] float[] stepMax;
    [SerializeField] float[] timeOffset;
    [SerializeField] int[] castDirectionIndex;
    [SerializeField] float restTime;
    [SerializeField] float stepTime;
    [SerializeField] float speed0;
    [SerializeField] float speed1;
    [SerializeField] float stepHeight;
    [SerializeField] bool drawEffectorGizmos;
    [SerializeField] bool drawBodyGizmos;

    JointedChain[] leg;
    LegTimer[] legTimer;
    Vector2[] target;//for now these are world position; we may want them to be local position (relative to hip anchor)
    float legCountInverse;
    float totalMass;
    bool facingRight;

    public int LegCount => leg.Length;
    public float TotalMass => totalMass;

    public bool FootIsTouchingGround(int i) => leg[i].body[^1].GetContacts().Length > 0;
    public bool AnyFootIsTouchingGround()
    {
        for (int i = 0; i < leg.Length; i++)
        {
            if (FootIsTouchingGround(i))
            {
                return true;
            }
        }

        return false;
    }
    public float FractionTouchingGround()
    {
        var ct = 0;
        for (int i = 0; i < leg.Length; i++)
        {
            if (FootIsTouchingGround(i))
            {
                ct++;
            }
        }

        return ct * legCountInverse;
    }

    public void RecalculateMass()
    {
        totalMass = 0;
        for (int i = 0; i < leg.Length; i++)
        {
            totalMass += leg[i].Mass;
        }
    }

    public void OnValidate()
    {
        if (leg != null)
        {
            for (int i = 0; i < leg.Length; i++)
            {
                if (leg[i].body != null)
                {
                    leg[i].UpdateDefAndSettings(chainDef, chainSettings[i]);
                }
            }
        }
    }

    public void OnDrawGizmos()
    {
        if (drawEffectorGizmos && target != null)
        {
            for (int i = 0; i < target.Length; i++)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(leg[i].EffectorPosition, 0.05f);

                Gizmos.color = Color.red;
                Gizmos.DrawSphere(target[i], 0.05f);
            }
        }
        if (drawBodyGizmos)
        {
            for (int i = 0; i < chainTransform.Length; i++)
            {
                JointedChain.DrawGizmos(chainTransform[i].array, chainDef.width);
            }
        }
    }

    public void Initialize(bool facingRight)
    {
        var numLegs = timeOffset.Length;
        legCountInverse = 1f / numLegs;

        //initialize timers
        float randomOffset = MathTools.RandomFloat(0, stepTime + restTime);//add to all timers, to randomize initial position
        var s = stepTime;
        var r = restTime;
        legTimer = timeOffset.Select(o => new LegTimer(o + randomOffset, s, r)).ToArray();

        leg = new JointedChain[numLegs];
        target = new Vector2[numLegs];

        this.facingRight = facingRight;
    }

    public void InitializeLeg(int i, PhysicsBody body)
    {
        leg[i].Initialize(chainTransform[i].array, body, chainDef, chainSettings[i]);
        target[i] = leg[i].EffectorPosition;
    }

    public void SetOrientation(bool facingRight)
    {
        //a) all bodies undergo same world reflection as main body, EXCEPT we use different convention for rotation (i.e. rotation reflects over same axis as position, keeping orientation)
        //b) reflect hip anchors to other side of their anchored bodies
        //c) negate joint angle limits

        this.facingRight = facingRight;
    }

    public void UpdateAllLegs(float dt, GroundMap map, Vector2[] castDirection, bool grounded)
    {
        var speedFraction = absoluteBodyGroundSpeed < speed0 ? 0 : absoluteBodyGroundSpeed / speed1;
        var speedScaledDt = timeScale * speedFraction * dt;
        var stepHeightSpeedMultiplier = grounded ? Mathf.Min(speedFraction, 1) : 1;

        for (int i = 0; i < leg.Length; i++)
        {
            UpdateLeg(i, speedScaledDt, stepHeightSpeedMultiplier, map, castDirection[castDirectionIndex[i]]);
        }
    }

    private void UpdateLeg(int i, float speedScaledDt, float stepHeightSpeedMultiplier, GroundMap map, Vector2 castDirection)
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

        var a = reachForce[i] * (target[i] - l.EffectorPosition);
        l.PullUniformly(a);
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
        if (!map.LineCastToGround(hipPosition, castDir, out var p) /*|| Mathf.Abs(p.arcLengthPosition) < Mathf.Abs(dot)*/)
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
        return map.PointFromCenterByArcLength(h, out _, out _);
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