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
    [SerializeField] ArrayContainer<Transform>[] bones;
    [SerializeField] float[] stepMax;
    [SerializeField] float[] stepLength;
    [SerializeField] float[] stepSpeed;
    [SerializeField] float[] stepHeight;
    [SerializeField] int[] castDirectionIndex;
    [SerializeField] Vector2 stepAccel;
    [SerializeField] Vector2 restAccel;
    //[SerializeField] Vector2 stepImpulse;
    [SerializeField] Vector2 stepDamping;
    [SerializeField] float stepPeakTime;
    [SerializeField] float stepDropPoint;//no upward forces will be applied beyond this point
    [SerializeField] float stepStopPoint;
    [SerializeField] float speed0;
    [SerializeField] float speed1;
    [SerializeField] float footGroundContactRadius;
#if UNITY_EDITOR
    [SerializeField] bool drawBodyGizmos;
    [SerializeField] bool[] drawAngleLimitGizmos;
    [SerializeField] bool[] drawFootGizmo;
#endif

    JointedChain[] leg;
    PhysicsQuery.QueryFilter footQueryFilter;
    int stepping;//used as bit mask (bit i = whether leg i is stepping)
    float legCountInverse;
    float totalMass;

    public int LegCount => leg.Length;
    public float TotalMass => totalMass;

    public bool Stepping(int i) => (stepping & (1 << i)) != 0;
    public bool FootIsTouchingGround(int i)
    {
        ref var foot = ref leg[i].body[^1];
        return foot.GetContacts().Length > 0
            || foot.world.TestOverlapGeometry(new CircleGeometry() { center = leg[i].EffectorPosition, radius = footGroundContactRadius }, footQueryFilter);
    }
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

#if UNITY_EDITOR

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

            footQueryFilter = chainDef.shapeDef.contactFilter.ToQueryFilter(PhysicsWorld.IgnoreFilter.IgnoreTriggerShapes);
        }
    }

    public void OnDrawGizmos()
    {
        if (drawBodyGizmos)
        {
            for (int i = 0; i < chainTransform.Length; i++)
            {
                JointedChain.DrawBodyGizmos(chainTransform[i].array, chainDef.width);
            }
        }

        if (drawAngleLimitGizmos != null)
        {
            if (Application.isPlaying && leg != null)//for runtime
            {
                for (int i = 0; i < leg.Length; i++)
                {
                    if (drawAngleLimitGizmos[i])
                    {
                        leg[i].DrawAngleGizmos();
                    }
                }
            }
            else if (chainTransform != null && drawAngleLimitGizmos.Length == chainTransform.Length)//for edit mode
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

        if (chainTransform != null && drawFootGizmo != null && drawFootGizmo.Length == chainTransform.Length)
        {
            for (int i = 0; i < chainTransform.Length; i++)
            {
                if (drawFootGizmo[i] && chainTransform[i].array != null)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireSphere(chainTransform[i].array[^1].position, footGroundContactRadius);
                }
            }
        }
    }
#endif

    public void Initialize(ReadOnlySpan<PhysicsBody> anchorBody)
    {
        var numLegs = chainTransform.Length;
        legCountInverse = 1f / numLegs;

        leg = new JointedChain[numLegs];
        stepStrength = new float[numLegs];
        Array.Fill(stepStrength, 1);

        stepping = 0;
        footQueryFilter = chainDef.shapeDef.contactFilter.ToQueryFilter(PhysicsWorld.IgnoreFilter.IgnoreTriggerShapes);

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

    public void OnDirectionChanged(PhysicsTransform bodyReflection)
    {
        //a) all bodies undergo same world reflection as main body, EXCEPT we use different convention for rotation (i.e. rotation reflects over same axis as position, keeping orientation)
        //b) reflect hip anchors to other side of their anchored bodies
        //c) negate joint angle limits

        //also reflect stepStart positions

        for (int i = 0; i < leg.Length; i++)
        {
            ref var l = ref leg[i];
            
            for (int j = 0; j < leg[i].body.Length; j++)
            {
                l.body[j].transform = l.body[j].transform.ReflectAndFlip(bodyReflection, l.body[j].localCenterOfMass);
                l.body[j].linearVelocity = l.body[j].linearVelocity.ReflectAcrossHyperplane(bodyReflection.rotation.direction);
                l.body[j].angularVelocity = -l.body[j].angularVelocity;
                l.body[j].SyncTransform();
                bones[i].array[j].ReflectAndFlip(l.body[j].transform);//2do: need to create physics transform > bone Transform and "center" physics transform
                ((PhysicsJoint)l.joint[j]).ReflectAndFlipAnchors();
            }

            l.reversed = !l.reversed;
        }
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

            //should be same for stepper and rester
            var castDir = castDirection[castDirectionIndex[stepper]];

            var stepperGdPos = EffectorRelativeGroundPosition(ref leg[stepper], map, castDir,
                out var stepperEffPos, out var stepperGdPt, out var stepperGdUp);
            var resterGdPos = EffectorRelativeGroundPosition(ref leg[rester], map, castDir,
                out var resterEffPos, out var resterGdPt, out var resterGdUp);

            if (stepperGdPos > stepMax[stepper] && resterGdPos < StepMin(rester))
            {
                //check both conditions or else we could have both stepper and rester past stepMax and then they just flicker back and forth
                //btwn which is stepper, causing a stall
                stepping ^= (3 << j);//flip bits j & j + 1
                stepper ^= 1;
                rester ^= 1;

                var tempEffPos = stepperEffPos;
                var tempGdPos = stepperGdPos;
                var tempGdPt = stepperGdPt;
                var tempGdUp = stepperGdUp;

                stepperEffPos = resterEffPos;
                stepperGdPos = resterGdPos;
                stepperGdPt = resterGdPt;
                stepperGdUp = resterGdUp;

                resterEffPos = tempEffPos;
                resterGdPos = tempGdPos;
                resterGdPt = tempGdPt;
                resterGdUp = tempGdUp;
            }

            var stepHt = grounded ? Mathf.Min(speedFraction, 1) * stepHeight[stepper] : stepHeight[stepper];
            UpdateLegStepping(stepper, map, stepHt, hipSpeed, facingRight, stepperEffPos, stepperGdPos, stepperGdPt, stepperGdUp);
            UpdateLegResting(rester, map, hipSpeed, facingRight, resterEffPos, resterGdPos, resterGdPt, resterGdUp);
        }
    }

    //2DO:
    //A) initial offsets
    //B) when hipSpeed = 0, should freeze stepping leg's x-position (can you just use the UpdateResting method for both legs in that case
    //-- except maybe cache x when hipSpeed initially becomes zero)
    //C) extract accel(dx, dy, etc.) into a method (same thing in both stepping and resting method)
    //D) still breaks without spring... -- behavior is erratic (which we can fix by fine tuning forces and damping,
    //but it also stalls. so i'd be interested in investigating the source of stall in that case)
    //D.5) doesn't work well over uneven terrain
    //D.75) falling off walls and upside down -- maybe we should use a slightly fattened overlapBox/circle to detect groundedness
    //E) do we properly handle negative hipSpeed and "scaled" hipSpeed?
    //+ simplify the things that need to be passed in from mover now that system has changed
    //F) changing direction lol
    //G) jump and freehang
    //H) clean up leg (get rid of unused fields, excess parameters, etc.)
    //+ clean up groundMap (move to job? (burst compile could do wonders for gdMap -- even if synchronous) + more efficient access (boxing?))
    //I) grabber arm with joints will be sweet
    //J) but keep an eye on performance -- I haven't been watching the impact of all these joints yet (we can always try multithreaded physics,
    //or increasing time step now)

    private void UpdateLegStepping(int i, GroundMap map, float stepHeight, float hipSpeed, 
        bool facingRight, Vector2 effectorPos, float gdPos, Vector2 gdPt, Vector2 gdUp)
    {
        if (stepStrength[i] == 0)
        {
            return;
        }

        ref var l = ref leg[i];
        var stepFraction = StepFraction(i, gdPos);
        var gdDir = facingRight ? gdUp.CWPerp() : gdUp.CCWPerp();

        //compute dX
        var goalRelSpd = stepFraction > stepDropPoint ? stepSpeed[i] * (stepStopPoint - stepFraction) / (stepStopPoint - stepDropPoint) : stepSpeed[i];
        var dX = hipSpeed * (goalRelSpd + 1) - Vector2.Dot(l.body[^1].linearVelocity, gdDir);
        //note that step speed is measured along ground!
        //stepDropPt = past this point, no upward forces will be applied and horizontal speed will begin to drop
        //stepStopPoint = at this point, leg goal horizontal speed is zero (set to slightly > 1 so leg doesn't stop before it reaches stepMax)

        //compute dY
        var c = 1 - 2 * stepPeakTime;
        var denom = 1 / (1 - stepPeakTime);
        denom *= denom;
        var goalStepHeight = denom * stepHeight * (stepFraction + c) * (1 - stepFraction);
        var dY = goalStepHeight - Vector2.Dot(effectorPos - (Vector2)gdPt, gdUp);
        if (stepFraction > stepDropPoint && dY > 0)
        {
            dY = 0;
            //if past stepDropPoint, never apply upward forces. this makes sure foot comes back to ground in time
        }

        //accelerate leg
        var aX = stepStrength[i] * stepAccel.x * dX;
        var aY = stepStrength[i] * stepAccel.y * dY;

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

        AccelerateLegEndsWithDamping(ref l, aX, aY, gdDir, gdUp, stepDamping);
    }

    //2do:
    //A) apply force along gdDir to keep movement smooth, but take direction between bodyDir and gdDir into account
    //(bc goal is position along bodyDir -- you will have to be careful when angle gets close to 90 e.g. moving past a small ledge)
    //B) we can extract the pull leg method that takes dX, dY and directions as parameters
    private void UpdateLegResting(int i, GroundMap map, float hipSpeed, bool facingRight, 
        Vector2 effectorPos, float gdPos, Vector2 gdPt, Vector2 gdUp)
    {
        if (stepStrength[i] == 0)
        {
            return;
        }

        ref var l = ref leg[i];
        var stepFraction = StepFraction(i, gdPos);
        var gdDir = facingRight ? gdUp.CWPerp() : gdUp.CCWPerp();

        var stepDropPoint = 1 - this.stepDropPoint;
        var stepStopPoint = 1 - this.stepStopPoint;
        var goalRelSpd = stepFraction < stepDropPoint ? stepSpeed[i] * (stepStopPoint - stepFraction) / (stepStopPoint - stepDropPoint) : stepSpeed[i];
        var dX = hipSpeed * (-goalRelSpd + 1) - Vector2.Dot(l.body[^1].linearVelocity, gdDir);
        //var dX = (goalStepFraction - stepFraction) * stepLength[i];
        var dY = Mathf.Min(Vector2.Dot((Vector2)gdPt - effectorPos, gdUp), 0);
        //^take min with 0, i.e. never apply upward forces (don't want to stack with collision forces)

        var aX = stepStrength[i] * restAccel.x * dX;
        var aY = stepStrength[i] * restAccel.y * dY;

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

        AccelerateLegEndsWithDamping(ref l, aX, aY, gdDir, gdUp, stepDamping);
    }

    //private void ApplyInitialStepImpulse(int i, GroundMap map, float stepHeight, float hipSpeed,
    //    Vector2 castDir, bool facingRight)
    //{
    //    if (stepStrength[i] == 0)
    //    {
    //        return;
    //    }

    //    ref var l = ref leg[i];

    //    var gdPt = CastToGroundPoint(map, l.EffectorPosition, castDir, out var gdUp);
    //    var gdDir = facingRight ? gdUp.CWPerp() : gdUp.CCWPerp();

    //    var a = hipSpeed * (stepImpulse.x * gdDir + stepHeight * stepImpulse.y * gdUp);
    //    for (int j = 0; j < l.JointCount; j++)
    //    {
    //        l.ImpulseEnd(j, a);
    //    }
    //}

    private static float2 CastToGroundPoint(GroundMap map, float2 p, float2 castDir, out float arcLengthPosition, out float2 normal)
    {
        float2 gdPt;

        if (map.LineCastToGround(p, castDir, out var hit))
        {
            gdPt = hit.point;
            normal = hit.normal;
            arcLengthPosition = hit.arcLengthPosition;
        }
        else
        {
            //line cast really never fails unless p is outside of the gdMap
            //but we'll keep this just in case
            gdPt = map.TrueClosestPoint(p, out arcLengthPosition, out normal, out _);
        }

        return gdPt;
    }

    private static void AccelerateLegEndsWithDamping(ref JointedChain l, float aX, float aY, Vector2 dirX, Vector2 dirY, Vector2 damping)
    {
        for (int j = 0; j < l.JointCount; j++)
        {
            var v = l.body[j].GetWorldPointVelocity(l.NextPosition(j));//linearVelocity;
            var vX = Vector2.Dot(v, dirX);
            var vY = Vector2.Dot(v, dirY);
            var a = (aX - damping.x * Mathf.Abs(vX) * vX) * dirX + (aY - damping.y * Mathf.Abs(vY) * vY) * dirY;
            //l.AccelerateCenter(j, a);
            l.AccelerateEnd(j, a);
        }
    }

    private static void AccelerateLegEnds(ref JointedChain l, Vector2 a)
    {
        for (int j = 0; j < l.JointCount; j++)
        {
            l.AccelerateEnd(j, a);
        }
    }

    private static void AccelerateLegBases(ref JointedChain l, Vector2 a)
    {
        for (int j = 1; j < l.JointCount; j++)
        {
            l.AccelerateBase(j, a);
        }
    }

    private static void AccelerateLegCenters(ref JointedChain l, Vector2 a)
    {
        for (int j = 0; j < l.JointCount; j++)
        {
            l.AccelerateCenter(j, a);
        }
    }

    private float StepFraction(int i, float effectorX) => Mathf.Clamp((effectorX - StepMin(i)) / stepLength[i], 0, 1);

    private float StepMin(int i) => stepMax[i] - stepLength[i];

    private float EffectorRelativeGroundPosition(ref JointedChain leg, GroundMap map, Vector2 castDir, 
        out Vector2 effectorPos, out float2 gdPt, out float2 gdUp)
    {
        effectorPos = leg.EffectorPosition;
        gdPt = CastToGroundPoint(map, effectorPos, castDir, out var effectorGroundPos, out gdUp);
        CastToGroundPoint(map, leg.body[0].position, castDir, out var hipGroundPos, out _);
        return effectorGroundPos - hipGroundPos;
    }

    //private float EffectorRelativeX(int i, Vector2 bodyDirection)
    //{
    //    return Vector2.Dot(leg[i].EffectorPosition - leg[i].BasePosition, bodyDirection);
    //}
}