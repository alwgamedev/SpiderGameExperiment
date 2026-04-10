using System;
using Unity.U2D.Physics;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public class LegSynchronizer
{
    public UnityEvent[] footHitGround;
    public float[] stepAccel;
    internal float bodyGroundSpeedSign;
    internal float absoluteBodyGroundSpeed;
    internal float timeScale;
    internal float stepHeightFraction;
    internal float strideMultiplier;

    [SerializeField] JointedChainDefinition chainDef;
    [SerializeField] JointedChainSettings[] chainSettings;
    [SerializeField] ArrayContainer<Transform>[] chainTransform;
    [SerializeField] float[] stepMax;
    [SerializeField] float[] stepLength;
    [SerializeField] float[] stepSpeed;
    [SerializeField] float[] restSpeed;
    [SerializeField] float[] stepHeight;
    [SerializeField] float[] timeOffset;
    [SerializeField] int[] bodyDirectionIndex;
    [SerializeField] float stepContactPoint;
    [SerializeField] float stepDamping;
    [SerializeField] float speed0;
    [SerializeField] float speed1;
    [SerializeField] bool drawBodyGizmos;
    [SerializeField] bool[] drawAngleLimitGizmos;

    JointedChain[] leg;
    //LegTimer[] legTimer;
    //Vector2[] target;//for now these are world position; we may want them to be local position (relative to hip anchor)
    //Vector2[] stepCache;
    bool[] stepping;
    Vector2[] restPosition;
    float legCountInverse;
    float totalMass;
    //bool facingRight;

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
        //if (drawEffectorGizmos && target != null)
        //{
        //    for (int i = 0; i < target.Length; i++)
        //    {
        //        Gizmos.color = Color.yellow;
        //        Gizmos.DrawSphere(leg[i].EffectorPosition, 0.05f);

        //        Gizmos.color = Color.red;
        //        Gizmos.DrawSphere(target[i], 0.05f);
        //    }
        //}

        if (drawBodyGizmos)
        {
            for (int i = 0; i < chainTransform.Length; i++)
            {
                JointedChain.DrawBodyGizmos(chainTransform[i].array, chainDef.width);
            }
        }

        if (drawAngleLimitGizmos != null && chainTransform != null && drawAngleLimitGizmos.Length == chainTransform.Length)
        {
            for (int i = 0; i < chainTransform.Length; i++)
            {
                if (drawAngleLimitGizmos[i])
                {
                    JointedChain.DrawAngleGizmos(chainTransform[i].array, chainSettings[i]);
                }
            }
        }
    }

    public void Initialize()
    {
        var numLegs = timeOffset.Length;
        legCountInverse = 1f / numLegs;

        //initialize timers
        //float randomOffset = MathTools.RandomFloat(0, 2 * stepDistance/*stepTime + restTime*/);//add to all timers, to randomize initial position
        ////var s = stepTime;
        ////var r = restTime;
        //var s = stepDistance;
        //legTimer = timeOffset.Select(o => new LegTimer(o + randomOffset, s, s/*r*/)).ToArray();

        leg = new JointedChain[numLegs];
        restPosition = new Vector2[numLegs];
        stepping = new bool[numLegs];
        //stepCache = new Vector2[numLegs];
        //target = new Vector2[numLegs];



        //this.facingRight = facingRight;
    }

    public void InitializeLeg(int i, PhysicsBody body, Vector2 bodyDirection, float rideHeight, bool facingRight)
    {
        leg[i].Initialize(chainTransform[i].array, body, chainDef, chainSettings[i]);
        //target[i] = leg[i].EffectorPosition;

        stepping[i] = false;
        var bodyUp = facingRight ? bodyDirection.CCWPerp() : bodyDirection.CWPerp();
        restPosition[i] = leg[i].BasePosition + StepMin(i) * bodyDirection - rideHeight * bodyUp;
    }

    public void OnDirectionChanged(bool facingRight)
    {
        //a) all bodies undergo same world reflection as main body, EXCEPT we use different convention for rotation (i.e. rotation reflects over same axis as position, keeping orientation)
        //b) reflect hip anchors to other side of their anchored bodies
        //c) negate joint angle limits

        //also reflect stepStart positions

        //this.facingRight = facingRight;
    }

    public void UpdateAllLegs(GroundMap map, Vector2[] bodyDirection, bool grounded, bool facingRight)
    {
        //var speedFraction = absoluteBodyGroundSpeed < speed0 ? 0 : absoluteBodyGroundSpeed / speed1;
        //var timeScale = bodyGroundSpeedSign * this.timeScale * speedFraction;
        //var speedScaledDt = timeScale * dt;
        //var stepHeightSpeedMultiplier = grounded ? Mathf.Min(speedFraction, 1) : 1;

        var hipSpeed = bodyGroundSpeedSign * timeScale * absoluteBodyGroundSpeed;
        var speedFraction = absoluteBodyGroundSpeed < speed0 ? 0 : absoluteBodyGroundSpeed / speed1;

        for (int i = 0; i < leg.Length; i++)
        {
            var ht = grounded ? Mathf.Min(speedFraction, 1) * stepHeight[i] : stepHeight[i];
            UpdateLeg(i, ht, hipSpeed, map, bodyDirection[bodyDirectionIndex[i]], facingRight);
        }
    }

    private void UpdateLeg(int i, float stepHeight, float hipSpeed, GroundMap map, Vector2 bodyDirection, bool facingRight)
    {
        ref var l = ref leg[i];
        var x = EffectorRelativeX(i, bodyDirection);
        var effectorPos = l.EffectorPosition;
        var bodyUp = facingRight ? bodyDirection.CCWPerp() : bodyDirection.CWPerp();

        if (stepping[i] && x > stepMax[i])
        {
            stepping[i] = false;
            restPosition[i] = map.LineCastToGround(effectorPos, -bodyUp, out var hit) ? hit.point
                : map.TrueClosestPoint(effectorPos, out _, out _, out _);
        }
        else if (!stepping[i] && x < stepMax[i] - stepLength[i])
        {
            stepping[i] = true;
        }

        //2DO:
        //A) when resting, do the same thing as with stepping but with a negative x-speed (based on restSpeedArray), and with stepHt of 0
        //B) since we do the same thing, turn it into a single method
        //C) try doing both wrt ground frame?
        //D) start offsets... 
            //maybe what we can do is for each group (front legs, hind legs -- or even just pairs of legs) we have a "leader" who moves based on actual x,
            //then rest of group uses a timer based on leader and initial offset
            //probably best if we just have leader-follower pairs (not groups) and rule is that follower does not start step until it is properly synced
            //with leader, based on its offset.
            //and while it is waiting it just pulls towards stepMin point (or just let it continue to rest so it doesn't visibly drag)
            //maybe most natural way to do it is to always have the stepping leg be the leader and the resting leg the follower (out of a pair)
            //so resting leg will slide towards stepMin while stepping leg steps (i.e. when stepping leg is at x-position 0 < t < 1, the
            //resting leg will pull towards 1 - t)
            //this then eliminates need for rest speed -- although you may want separate restAccel
            //^^ give this a try
            //++ see if doing everything in ground frame makes the resting motion more natural (also probably will be more natural when slowed down
        //E) do we properly handle negative hipSpeed and "scaled" hipSpeed?
            //+ simplify the things that need to be passed in from mover now that system has changed
        //F) changing direction lol
        //G) jump and freehang
        //H) clean up leg (get rid of unused fields, excess parameters, etc.)
            //+ clean up groundMap (move to job? (burst compile could do wonders for gdMap -- even if synchronous) + more efficient access (boxing?))
        //I) grabber arm with joints will be sweet
        //J) but keep an eye on performance -- I haven't been watching the impact of all these joints yet (we can always try multithreaded physics,
            //or reducing time step now)


        if (stepping[i])
        {
            var t = Mathf.Clamp((x - StepMin(i)) / stepLength[i], 0, 1);

            //compute dX
            var dX = hipSpeed * (stepSpeed[i] + 1) - Vector2.Dot(l.body[^1].linearVelocity, bodyDirection);
            //goal speed - cur speed, where goal speed = hipSpeed * stepSpeed[i] + hipSpeed

            //compute dY
            stepHeight = 4 * stepHeight * t * (stepContactPoint - t);
            Vector2 gdPt = map.LineCastToGround(effectorPos, -bodyUp, out var hit) ? hit.point
                : map.TrueClosestPoint(effectorPos, out _, out _, out _);
            var dY = stepHeight - Vector2.Dot(effectorPos - gdPt, bodyUp);
            if (t > 0.5f && dY > 0)
            {
                dY = 0;
            }

            //accelerate leg
            var aX = stepAccel[i] * dX;
            var aY = stepAccel[i] * dY;


            var dotX = Vector2.Dot(l.body[^1].linearVelocity, bodyDirection);
            aX -= stepDamping * Mathf.Abs(dotX) * dotX;
            var dotY = Vector2.Dot(l.body[^1].linearVelocity, bodyUp);
            aY -= stepDamping * Mathf.Abs(dotY) * dotY;

            //var gX = Vector2.Dot(l.AnchorBody.world.gravity, bodyDirection);
            //var gY = Vector2.Dot(l.AnchorBody.world.gravity, bodyUp);
            //if (MathTools.OppositeSigns(aX, gX))
            //{
            //    aX -= gX;
            //}
            //if (MathTools.OppositeSigns(aY, gY))
            //{
            //    aY -= gY;
            //}

            //var aVecX = aX * bodyDirection;
            //var aVecY = aY * bodyUp;
            //l.AccelerateEnd(0, aVecY);
            //l.AccelerateBase(l.JointCount - 1, aVecX);
            PullLeg(ref l, aX * bodyDirection + aY * bodyUp);
        }
        else
        {
            //2do: when resting we'll 
            Vector2 gdPt = map.TrueClosestPoint(restPosition[i], out _, out _, out _);
            var err = gdPt - effectorPos;
            var a = stepAccel[i] * err;
            var u = err.normalized;
            if (u != Vector2.zero)
            {
                var dot = Vector2.Dot(l.body[^1].linearVelocity, u);
                a -= stepDamping * Mathf.Abs(dot) * dot * u;
            }
            PullLeg(ref l, a);
            //l.AccelerateEnd(l.JointCount - 1, stepAccel[i] * err);
        }

        static void PullLeg(ref JointedChain l, Vector2 a)
        {
            for (int j = 0; j < l.JointCount; j++)
            {
                l.AccelerateEnd(j, a);
            }
        }

        //^2do: better method for pull leg (maybe only pull uniformly when pulling up and pull just effector when pulling down? or always just do effector... we can see how it looks and experiment)
    }

    private float StepMin(int i) => stepMax[i] - stepLength[i];

    private float EffectorRelativeX(int i, Vector2 bodyDirection)
    {
        return Vector2.Dot(leg[i].EffectorPosition - leg[i].BasePosition, bodyDirection);
    }
}