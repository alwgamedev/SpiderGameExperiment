using System;
using System.Reflection.Metadata.Ecma335;
using Unity.Mathematics;
using Unity.U2D.Physics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public class LegSynchronizer
{
    public UnityEvent[] footHitGround;
    internal float[] stepStrength;//public multiplier (0 - 1) for step and rest forces (set to 0 if you want the leg to go limp)
    //internal float bodyGroundSpeedSign;
    //internal float absoluteBodyGroundSpeed;
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
    [SerializeField] float stepPeakTime;
    [SerializeField] float stepDropPoint;//no upward forces will be applied beyond this point
    [SerializeField] float stepStopPoint;
    [SerializeField] float restDropPoint;
    [SerializeField] float restStopPoint;
    [SerializeField] float speed0;
    [SerializeField] float speed1;
    [SerializeField] float footGroundContactRadius;
#if UNITY_EDITOR
    [SerializeField] bool drawBodyGizmos;
    [SerializeField] bool[] drawAngleLimitGizmos;
    [SerializeField] bool[] drawFootGizmo;
    [SerializeField] bool[] debugStepStats;
#endif

    JointedChain[] leg;
    PhysicsQuery.QueryFilter footQueryFilter;
    int stepping;//used as bit mask (bit i = whether leg i is stepping)
    float legCountInverse;
    float totalMass;

    public int LegCount => leg.Length;
    public float TotalMass => totalMass;

    public bool Stepping(int i) => (stepping & (1 << i)) != 0;
    public bool FootPadIsTouchingGround(int i)
    {
        var cg = new CircleGeometry()
        {
            center = leg[i].EffectorPosition,
            radius = footGroundContactRadius
        };
        return leg[i].body[^1].world.TestOverlapGeometry(cg, footQueryFilter);
    }
    public bool LegIsTouchingGround(int i)
    {
        if (FootPadIsTouchingGround(i))
        {
            return true;
        }

        ref var l = ref leg[i];
        for (int j = l.JointCount - 1; j > -1; j--)
        {
            if (l.body[j].GetContacts().Length > 0)
            {
                return true;
            }
        }

        return false;
    }
    public bool AnyLegIsTouchingGround()
    {
        for (int i = 0; i < leg.Length; i++)
        {
            if (LegIsTouchingGround(i))
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
            if (LegIsTouchingGround(i)/*FootIsTouchingGround(i)*/)
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
                    if (leg[i].reversed)
                    {
                        FlipAngleLimits(i);
                    }
                }
            }

            footQueryFilter = chainDef.shapeDef.contactFilter.ToQueryFilter(PhysicsWorld.IgnoreFilter.IgnoreTriggerShapes);
        }
    }

    public void OnDrawGizmos()
    {
        if (drawBodyGizmos)
        {
            for (int i = 0; i < bones.Length; i++)
            {
                JointedChain.DrawBodyGizmos(bones[i].array, chainDef.width);
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
            else if (bones != null && drawAngleLimitGizmos.Length == bones.Length)//for edit mode
            {
                for (int i = 0; i < bones.Length; i++)
                {
                    if (drawAngleLimitGizmos[i])
                    {
                        JointedChain.DrawAngleGizmos(bones[i].array, chainSettings[i]);
                    }
                }
            }
        }

        if (bones != null && drawFootGizmo != null && drawFootGizmo.Length == bones.Length)
        {
            for (int i = 0; i < bones.Length; i++)
            {
                if (drawFootGizmo[i] && bones[i].array != null)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireSphere(bones[i].array[^1].position, footGroundContactRadius);
                }
            }
        }
    }

    //for one-time use
    public void CreatePhysicsTransforms(MonoBehaviour owner)
    {
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Create Leg Physics Transforms");
        int groupId = Undo.GetCurrentGroup();

        bones = new ArrayContainer<Transform>[chainTransform.Length];
        Undo.RecordObject(owner, "Create Leg Physics Transforms");

        for (int i = 0; i < chainTransform.Length; i++)
        {
            var oldBones = chainTransform[i].array;
            var parent = oldBones[0].parent;

            bones[i] = new()
            {
                array = new Transform[oldBones.Length]
            };
            Array.Copy(oldBones, bones[i].array, bones[i].array.Length);

            for (int j = 0; j < oldBones.Length; j++)
            {
                var physBody = new GameObject(oldBones[j].name + " Phys Body");
                Undo.RegisterCreatedObjectUndo(physBody, "Create Phys Transform");
                Undo.SetTransformParent(physBody.transform, parent, "Set Phys Transform Parent");

                physBody.transform.localScale = Vector3.one;
                physBody.transform.SetPositionAndRotation(oldBones[j].position, oldBones[j].rotation);
                Undo.SetTransformParent(oldBones[j], physBody.transform, "Set Bone Parent");

                chainTransform[i].array[j] = physBody.transform;
                PrefabUtility.RecordPrefabInstancePropertyModifications(oldBones[j]);
            }
        }

        PrefabUtility.RecordPrefabInstancePropertyModifications(owner);

        Undo.CollapseUndoOperations(groupId);
    }

    public void CenterPhysicsTransforms()
    {
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Center Leg Bodies");
        int groupId = Undo.GetCurrentGroup();

        for (int i = 0; i < bones.Length; i++)
        {
            var physBodies = chainTransform[i].array;
            var bones = this.bones[i].array;
            for (int j = 0; j < bones.Length - 1; j++)
            {
                var p0 = bones[j].position;

                Undo.RecordObject(physBodies[j], "Set phys body position");
                Undo.RecordObject(bones[j], "Set bone position");
                physBodies[j].position = 0.5f * (bones[j].position + bones[j + 1].position);
                bones[j].position = p0;

                PrefabUtility.RecordPrefabInstancePropertyModifications(physBodies[j]);
                PrefabUtility.RecordPrefabInstancePropertyModifications(bones[j]);
            }
        }

        Undo.CollapseUndoOperations(groupId);
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
            leg[i].Initialize(chainTransform[i].array, bones[i].array, body, chainDef, chainSettings[i]);
        }
    }

    public void OnDirectionChanged(PhysicsTransform bodyReflection, bool facingRight)
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
                l.body[j].transform = l.body[j].transform.ReflectAndFlip(bodyReflection, Vector2.zero);
                l.body[j].linearVelocity = l.body[j].linearVelocity.ReflectAcrossHyperplane(bodyReflection.rotation.direction);
                l.body[j].angularVelocity = -l.body[j].angularVelocity;
                l.body[j].SyncTransform();
                bones[i].array[j].ReflectAndFlip(l.body[j].transform);//2do: need to create physics transform > bone Transform and "center" physics transform
                ((PhysicsJoint)l.joint[j]).ReflectAndFlipAnchors();
            }

            if (l.reversed == facingRight)
            {
                l.reversed = !l.reversed;
                FlipAngleLimits(i);
            }
        }
    }

    public void UpdateAllLegs(GroundMap map, Vector2[] castDirection, bool grounded, bool facingRight)
    {
        //var hipSpeed = bodyGroundSpeedSign * timeScale * absoluteBodyGroundSpeed;
        //var speedFraction = absoluteBodyGroundSpeed < speed0 ? 0 : absoluteBodyGroundSpeed / speed1;

        for (int i = 0; i < leg.Length / 2; i++)
        {
            var j = 2 * i;
            int stepper = Stepping(j) ? j : j + 1;
            int rester = stepper ^ 1;//flips first bit (so 2i => 2i + 1 and 2i + 1 => 2i)

            //should be same for stepper and rester
            var castDir = castDirection[castDirectionIndex[stepper]];

            var stepperGdPos = EffectorRelativeGroundPosition(ref leg[stepper], map, castDir, facingRight,
                out var stepperEffPos, out var stepperGdPt, out var stepperGdUp, out var hipGdUp);
            var resterGdPos = EffectorRelativeGroundPosition(ref leg[rester], map, castDir, facingRight,
                out var resterEffPos, out var resterGdPt, out var resterGdUp, out _);

            var hipGdDir = facingRight ? hipGdUp.CWPerp() : hipGdUp.CCWPerp();
            var hipSpeed = HipSpeed(ref leg[stepper], hipGdDir);
            //var speedFraction = SpeedFraction(hipSpeed);

            var stepperStepFraction = StepFraction(stepper, stepperGdPos);
            var resterStepFraction = StepFraction(rester, resterGdPos);

            if (resterStepFraction < 0 && stepperStepFraction > 1)
            {
                //check both conditions or else we could have both stepper and rester past stepMax and then they just flicker back and forth
                //btwn which is stepper, causing a stall
                stepping ^= (3 << j);//flip bits j & j + 1
                stepper ^= 1;
                rester ^= 1;

                var tempEffPos = stepperEffPos;
                var tempStepFraction = stepperStepFraction;
                var tempGdPt = stepperGdPt;
                var tempGdUp = stepperGdUp;

                stepperEffPos = resterEffPos;
                stepperStepFraction = resterStepFraction;
                stepperGdPt = resterGdPt;
                stepperGdUp = resterGdUp;

                resterEffPos = tempEffPos;
                resterStepFraction = tempStepFraction;
                resterGdPt = tempGdPt;
                resterGdUp = tempGdUp;
            }

            UpdateLegStepping(stepper, hipSpeed, facingRight, grounded, stepperEffPos, stepperStepFraction, stepperGdPt, stepperGdUp);
            UpdateLegResting(rester, hipSpeed, facingRight, resterEffPos, resterStepFraction, resterGdPt, resterGdUp);
        }
    }

    private void UpdateLegStepping(int i, float hipSpeed,
        bool facingRight, bool grounded, Vector2 effectorPos, float stepFraction, Vector2 gdPt, Vector2 gdUp)
    {
        if (stepStrength[i] == 0)
        {
            return;
        }

        ref var l = ref leg[i];
        Vector2 gdDir = facingRight ? gdUp.CWPerp() : gdUp.CCWPerp();
        var speedFraction = AbsoluteSpeedFraction(hipSpeed);
        var stepHt = StepHeight(i, grounded, speedFraction);

        //compute dX
        var goalRelSpd = stepFraction > stepDropPoint ?
            stepSpeed[i] * Mathf.Clamp01((stepStopPoint - stepFraction) / (stepStopPoint - stepDropPoint))
            : stepSpeed[i];
        var dX = hipSpeed * (goalRelSpd + 1) - Vector2.Dot(l.body[^1].linearVelocity, gdDir);
        //note that step speed is measured along ground!
        //stepDropPt = past this point, no upward forces will be applied and horizontal speed will begin to drop
        //stepStopPoint = at this point, leg goal horizontal speed is zero (set to slightly > 1 so leg doesn't stop before it reaches stepMax)

        //compute dY
        var c = 1 - 2 * stepPeakTime;
        var denom = 1 / (1 - stepPeakTime);
        denom *= denom;
        var curStepHt = Vector2.Dot(effectorPos - gdPt, gdUp);
        var goalStepHt = stepHt * Mathf.Clamp01(denom * (stepFraction + c) * (1 - stepFraction));
        var dY = goalStepHt - curStepHt;
        if (stepFraction > stepDropPoint && dY > 0)
        {
            dY = 0;
            //if past stepDropPoint, never apply upward forces. this ensures foot comes back to ground in time
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

        var a = aX * gdDir + aY * gdUp;
        AccelerateLegEnds(i, a);

#if UNITY_EDITOR
        if (debugStepStats[i])
        {
            Debug.Log($"LEG {i}");
            Debug.Log($"-State: stepping ({stepFraction})");
            Debug.Log($"-Hip Speed: {hipSpeed}");
            Debug.Log($"-Max step ht: {stepHt}");
            Debug.Log($"-Cur step ht: {curStepHt}");
            Debug.Log($"-Goal step ht: {goalStepHt}");
        }
#endif
    }

    //2do:
    //A) apply force along gdDir to keep movement smooth, but take direction between bodyDir and gdDir into account
    //(bc goal is position along bodyDir -- you will have to be careful when angle gets close to 90 e.g. moving past a small ledge)
    //B) we can extract the pull leg method that takes dX, dY and directions as parameters
    private void UpdateLegResting(int i, float hipSpeed, bool facingRight, 
        Vector2 effectorPos, float stepFraction, Vector2 gdPt, Vector2 gdUp)
    {
        if (stepStrength[i] == 0)
        {
            return;
        }

        ref var l = ref leg[i];
        Vector2 gdDir = facingRight ? gdUp.CWPerp() : gdUp.CCWPerp();
        //var hipSpeed = HipSpeed(ref l, gdDir);

        var goalRelSpd = stepFraction < restDropPoint ?
            stepSpeed[i] * Mathf.Clamp01((restStopPoint - stepFraction) / (restStopPoint - restDropPoint))
            : stepSpeed[i];
        var dX = hipSpeed * (-goalRelSpd + 1) - Vector2.Dot(l.body[^1].linearVelocity, gdDir);
        var dY = Mathf.Min(Vector2.Dot(gdPt - effectorPos, gdUp), 0);
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

        var a = aX * gdDir + aY * gdUp;
        AccelerateLegEnds(i, a);
    }

    private static float2 CastToGroundPoint(GroundMap map, float2 p, float2 castDir, out float arcLengthPosition, out float2 normal)
    {
        (int i, float t) = map.LineCastOrClosest(p, castDir, GroundMap.DEFAULT_PRECISION);
        normal = map.NormalFromReducedPosition(i, t);
        arcLengthPosition = map.ArcLengthPos(i) + t;

        return map.PointFromReducedPosition(i, t);
    }

    private void AccelerateLegEnds(int i, Vector2 a)
    {
        for (int j = 0; j < leg[i].JointCount; j++)
        {
            leg[i].AccelerateEnd(j, a);
#if UNITY_EDITOR
            if (debugStepStats[i])
            {
                var p = leg[i].body[j].worldCenterOfMass;
                Debug.DrawLine(p, p + 0.4f * a.normalized, Color.deepPink);
            }
#endif
        }
    }

    private void AccelerateLegBases(int i, Vector2 a)
    {
        for (int j = 1; j < leg[i].JointCount; j++)
        {
            leg[i].AccelerateBase(j, a);
#if UNITY_EDITOR
            if (debugStepStats[i])
            {
                var p = leg[i].body[j].worldCenterOfMass;
                Debug.DrawLine(p, p + 0.4f * a.normalized, Color.deepPink);
            }
#endif
        }
    }

    private void FlipAngleLimits(int i)
    {
        for (int j = 0; j < leg[i].JointCount; j++)
        {
            leg[i].FlipAngleLimits(j);
        }
    }

    private void AccelerateLegCenters(int i, Vector2 a)
    {
        for (int j = 0; j < leg[i].JointCount; j++)
        {
            leg[i].AccelerateCenter(j, a);
#if UNITY_EDITOR
            if (debugStepStats[i])
            {
                var p = leg[i].body[j].worldCenterOfMass;
                Debug.DrawLine(p, p + 0.4f * a.normalized, Color.deepPink);
            }
#endif
        }
    }

    private float AbsoluteSpeedFraction(float speed)
    {
        speed = Mathf.Abs(speed);
        return speed < speed0 ? 0 : speed / speed1;
    }

    private float HipSpeed(ref JointedChain l, Vector2 gdDir)
    {
        return Vector2.Dot(l.body[0].GetWorldPointVelocity(l.JointPosition(0)), gdDir);
    }

    private float StepHeight(int i, bool grounded, float speedFraction)
    {
        return grounded ? Mathf.Min(speedFraction, 1) * stepHeight[i] : stepHeight[i];
    }

    private float StepFraction(int i, float effectorX) => (effectorX - StepMin(i)) / (stepLength[i]);

    private float StepMin(int i) => stepMax[i] - stepLength[i];

    private float EffectorRelativeGroundPosition(ref JointedChain leg, GroundMap map, Vector2 castDir, bool facingRight,
        out Vector2 effectorPos, out float2 gdPt, out float2 gdUp, out float2 hipGdUp)
    {
        effectorPos = leg.EffectorPosition;
        gdPt = CastToGroundPoint(map, effectorPos, castDir, out var effectorGroundPos, out gdUp);
        CastToGroundPoint(map, leg.body[0].position, castDir, out var hipGroundPos, out hipGdUp);
        return facingRight ? effectorGroundPos - hipGroundPos : hipGroundPos - effectorGroundPos;
    }

    //private float EffectorRelativeX(int i, Vector2 bodyDirection)
    //{
    //    return Vector2.Dot(leg[i].EffectorPosition - leg[i].BasePosition, bodyDirection);
    //}
}