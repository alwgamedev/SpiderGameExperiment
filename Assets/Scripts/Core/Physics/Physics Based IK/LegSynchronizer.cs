using System;
using Unity.Mathematics;
using Unity.U2D.Physics;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public class LegSynchronizer
{
    public UnityEvent[] footHitGround;
    internal float[] stepStrength;//public multiplier (0 - 1) for step and rest forces (set to 0 if you want the leg to go limp)
    internal float bodyGroundSpeedSign;
    internal float absoluteBodyGroundSpeed;
    internal float timeScale;
    internal float stepHeightFraction;
    internal float strideMultiplier;

    [SerializeField] JointedChainDefinition chainDef;
    [SerializeField] JointedChainSettings[] chainSettings;
    [SerializeField] ArrayContainer<Transform>[] chainTransform; 
    [SerializeField] Vector2[] stepAccel;
    [SerializeField] Vector2[] restAccel;
    [SerializeField] float[] stepMax;
    [SerializeField] float[] stepLength;
    [SerializeField] float[] stepSpeed;
    [SerializeField] float[] stepHeight;
    [SerializeField] int[] castDirectionIndex;
    [SerializeField] float stepDropPoint;//no upward forces will be applied beyond this point
    [SerializeField] float stepDamping;
    [SerializeField] float speed0;
    [SerializeField] float speed1;
    [SerializeField] bool drawBodyGizmos;
    [SerializeField] bool[] drawAngleLimitGizmos;

    JointedChain[] leg;
    //LegTimer[] legTimer;
    //Vector2[] target;//for now these are world position; we may want them to be local position (relative to hip anchor)
    //Vector2[] stepCache;
    //bool[] stepping;
    int stepping;//used as bit mask (bit i tells whether leg i is stepping)
    //Vector2[] restPosition;
    float legCountInverse;
    float totalMass;
    //bool facingRight;

    public int LegCount => leg.Length;
    public float TotalMass => totalMass;

    public bool Stepping(int i) => (stepping & (1 << i)) != 0;
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

    public void Initialize(ReadOnlySpan<PhysicsBody> anchorBody)
    {
        var numLegs = chainTransform.Length;
        legCountInverse = 1f / numLegs;

        leg = new JointedChain[numLegs];
        stepStrength = new float[numLegs];
        Array.Fill(stepStrength, 1);

        stepping = 0;

        for (int i = 0; i < chainTransform.Length / 2; i++)
        {
            int j = 2 * i;
            stepping |= 1 << j;//even legs start as stepping, odd legs start as resting
            InitializeLeg(j, anchorBody[j]);
            InitializeLeg(j + 1, anchorBody[j + 1]);
        }

        void InitializeLeg(int i, PhysicsBody body)
        {
            leg[i].Initialize(chainTransform[i].array, body, chainDef, chainSettings[i]);
        }
    }

    public void OnDirectionChanged(bool facingRight)
    {
        //a) all bodies undergo same world reflection as main body, EXCEPT we use different convention for rotation (i.e. rotation reflects over same axis as position, keeping orientation)
        //b) reflect hip anchors to other side of their anchored bodies
        //c) negate joint angle limits

        //also reflect stepStart positions

        //this.facingRight = facingRight;
    }

    public void UpdateAllLegs(GroundMap map, Vector2[] castDirection, bool grounded, bool facingRight)
    {
        var hipSpeed = bodyGroundSpeedSign * timeScale * absoluteBodyGroundSpeed;
        var speedFraction = absoluteBodyGroundSpeed < speed0 ? 0 : absoluteBodyGroundSpeed / speed1;

        for (int i = 0; i < leg.Length / 2; i++)
        {
            var j = 2 * i;
            int stepper = Stepping(j) ? j : j + 1;
            int rester = stepper ^ 1;//flips first bit (so 2i => 2i + 1 and 2i + 1 => 2i)

            var castDir = castDirection[castDirectionIndex[stepper]];
            var stepperX = EffectorRelativeX(stepper, castDirection[castDirectionIndex[stepper]]);
            if (stepperX > stepMax[stepper])
            {
                stepping ^= (3 << j);//flip bits j & j + 1
                stepper ^= 1;
                rester ^= 1;
            }

            var stepHt = grounded ? Mathf.Min(speedFraction, 1) * stepHeight[i] : stepHeight[i];
            UpdateLegStepping(stepper, stepHt, hipSpeed, map, castDir, facingRight, out var stepFraction);
            UpdateLegResting(rester, map, castDir, facingRight, 1 - stepFraction);
        }
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
        //or increasing time step now)

    private void UpdateLegStepping(int i, float stepHeight, float hipSpeed, GroundMap map, 
        Vector2 castDir, bool facingRight, out float stepFraction)
    {
        var bodyDir = facingRight ? castDir.CCWPerp() : castDir.CWPerp();
        var effectorX = EffectorRelativeX(i, bodyDir);
        stepFraction = StepFraction(i, effectorX);

        if (stepStrength[i] == 0)
        {
            return;
        }

        ref var l = ref leg[i];
        var effectorPos = l.EffectorPosition;
        var gdPt = CastToGroundPoint(map, effectorPos, castDir, out var gdUp);
        var gdDir = facingRight ? gdUp.CWPerp() : gdUp.CCWPerp();

        //compute dX
        var dX = hipSpeed * (stepSpeed[i] + 1) - Vector2.Dot(l.body[^1].linearVelocity, gdDir);
        //this goal speed - cur speed, where goal speed = hipSpeed * stepSpeed[i] + hipSpeed
        //note that step speed is measured along ground!
        //(alternatively we could measure it along bodyDir, but still apply force along gdDirection to keep movement smooth
        //(scaling by a dot product to get desired speed along bodyDir))

        //compute dY
        stepHeight = 4 * stepHeight * stepFraction * (1 - stepFraction);

        var dY = stepHeight - Vector2.Dot(effectorPos - (Vector2)gdPt, gdUp);
        if (stepFraction > stepDropPoint && dY > 0)
        {
            dY = 0;
            //if past stepDropPoint, never apply upward forces. this makes sure foot comes back to ground in time
            //(use e.g. stepDropPoint = 0.75f)
        }

        //accelerate leg
        var aX = stepStrength[i] * stepAccel[i].x * dX;
        var aY = stepStrength[i] * stepAccel[i].y * dY;


        var dotX = Vector2.Dot(l.body[^1].linearVelocity, gdDir);
        aX -= stepDamping * Mathf.Abs(dotX) * dotX;
        var dotY = Vector2.Dot(l.body[^1].linearVelocity, gdUp);
        aY -= stepDamping * Mathf.Abs(dotY) * dotY;

        //var gX = Vector2.Dot(l.AnchorBody.world.gravity, gdDir);
        //var gY = Vector2.Dot(l.AnchorBody.world.gravity, gdUp);
        //if (MathTools.OppositeSigns(aX, gX))
        //{
        //    aX -= gX;
        //}
        //if (MathTools.OppositeSigns(aY, gY))
        //{
        //    aY -= gY;
        //}

        PullLeg(ref l, aX * gdDir + aY * gdUp);
    }

    //2do:
    //A) apply force along gdDir to keep movement smooth, but take direction between bodyDir and gdDir into account
        //(bc goal is position along bodyDir -- you will have to be careful when angle gets close to 90 e.g. moving past a small ledge)
    //B) we can extract the pull leg method that takes dX, dY and directions as parameters
    private void UpdateLegResting(int i, GroundMap map,
        Vector2 castDir, bool facingRight, float goalStepFraction)
    {
        if (stepStrength[i] == 0)
        {
            return;
        }

        ref var l = ref leg[i];
        var bodyDir = facingRight ? castDir.CCWPerp() : castDir.CWPerp();
        var effectorX = EffectorRelativeX(i, bodyDir);
        var effectorPos = l.EffectorPosition;
        var stepFraction = StepFraction(i, effectorX);

        var gdPt = CastToGroundPoint(map, effectorPos, castDir, out var gdUp);
        var gdDir = facingRight ? gdUp.CWPerp() : gdUp.CCWPerp();

        var dX = goalStepFraction - stepFraction;
        var dY = Mathf.Min(Vector2.Dot((Vector2)gdPt - effectorPos, gdUp), 0);
        //^take min with 0, i.e. never apply upward forces (don't want to stack with collision forces)

        var aX = stepStrength[i] * restAccel[i].x * dX;
        var aY = stepStrength[i] * restAccel[i].y * dY;

        var dotX = Vector2.Dot(l.body[^1].linearVelocity, gdDir);
        aX -= stepDamping * Mathf.Abs(dotX) * dotX;
        var dotY = Vector2.Dot(l.body[^1].linearVelocity, gdUp);
        aY -= stepDamping * Mathf.Abs(dotY) * dotY;

        PullLeg(ref l, aX * gdDir + aY * gdUp);
    }

    private static float2 CastToGroundPoint(GroundMap map, float2 p, float2 castDir, out float2 normal)
    {
        float2 gdPt;

        if (map.LineCastToGround(p, castDir, out var hit))
        {
            gdPt = hit.point;
            normal = hit.normal;
        }
        else
        {
            //line cast really never fails unless p is outside of the gdMap
            //but we'll keep this just in case
            gdPt = map.TrueClosestPoint(p, out _, out normal, out _);
        }

        return gdPt;
    }

    private static void PullLeg(ref JointedChain l, Vector2 a)
    {
        for (int j = 0; j < l.JointCount; j++)
        {
            l.AccelerateEnd(j, a);
        }
    }

    private float StepFraction(int i, float effectorX) => Mathf.Clamp((effectorX - StepMin(i)) / stepLength[i], 0, 1);

    private float StepMin(int i) => stepMax[i] - stepLength[i];

    private float EffectorRelativeX(int i, Vector2 bodyDirection)
    {
        return Vector2.Dot(leg[i].EffectorPosition - leg[i].BasePosition, bodyDirection);
    }
}