using System;
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
    internal float timeScale;
    internal float stepHeightFraction;
    internal float strideMultiplier;

    [SerializeField] JointedChainDefinition chainDef;
    [SerializeField] JointedChainSettings[] chainSettings;
    [SerializeField] ArrayContainer<Transform>[] chainTransform;
    [SerializeField] ArrayContainer<Transform>[] bones;
    [SerializeField] float[] timer;//for initial positions: use [0, 1) to begin stepping, and use (1, 2] to begin resting (will subtract 1 off at start)
    [SerializeField] bool[] beginStepping;
    [SerializeField] float[] stepMax;
    [SerializeField] float[] stepLength;
    [SerializeField] float[] stepSpeed;
    [SerializeField] float[] restSpeed;
    [SerializeField] float[] stepHeight;
    [SerializeField] int[] castDirectionIndex;
    [SerializeField] float stepAccel;
    [SerializeField] float stepDamping;
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
            if (LegIsTouchingGround(i))
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

            RecalculateMass();
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

        for (int i = 0; i < chainTransform.Length; i++)
        {
            InitializeLeg(i, anchorBody[i]);
            if (beginStepping[i])
            {
                stepping &= (1 << i);
            }
        }

        void InitializeLeg(int i, PhysicsBody body)
        {
            leg[i].Initialize(chainTransform[i].array, bones[i].array, body, chainDef, chainSettings[i]);
        }

        RecalculateMass();
    }

    public void OnDirectionChanged(PhysicsTransform bodyReflection, bool facingRight)
    {
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

    public void UpdateAllLegs(float dt, GroundMap map, Vector2[] castDirection, bool grounded, bool facingRight)
    {
        for (int i = 0; i < leg.Length; i++)
        {
            var castDir = castDirection[castDirectionIndex[i]];
            var (j, s) = map.LineCastOrClosest(leg[i].JointPosition(0), castDir, GroundMap.DEFAULT_PRECISION);//hip ground point
            var n = map.NormalFromReducedPosition(j, s);//normal at hip ground point
            var hipGroundDir = facingRight ? n.CWPerp() : n.CCWPerp();
            var hipSpeed = HipSpeed(ref leg[i], hipGroundDir);
            if (Mathf.Abs(hipSpeed) < speed0)
            {
                hipSpeed = 0;
            }

            if ((Stepping(i) && timer[i] > 1) || (!Stepping(i) && timer[i] < 0))
            {
                stepping ^= 1 << i;//flip i-th bit
            }

            if (Stepping(i))
            {
                var goalRelSpeed = GoalRelSpeedStepping(timer[i], stepSpeed[i], hipSpeed);
                var goalStepHt = StepHeight(stepHeight[i], timer[i], hipSpeed, grounded);
                UpdateLeg(i, dt, map, facingRight, (j, s), goalRelSpeed, goalStepHt);
            }
            else
            {
                var goalRelSpeed = GoalRelSpeedResting(timer[i], restSpeed[i], hipSpeed);
                UpdateLeg(i, dt, map, facingRight, (j, s), goalRelSpeed, 0);
            }
        }
    }

    private void UpdateLeg(int i, float dt, GroundMap map, bool facingRight, (int, float) hipGroundMapPosition,
        float goalRelSpd, float goalStepHt)
    {
        float2 effectorPos = leg[i].EffectorPosition;

        timer[i] += dt * goalRelSpd / stepLength[i];

        var targetRelPos = StepPosition(stepMax[i], stepLength[i], timer[i]);//target position along ground map, relative to hip
        var (j, a) = map.AddArcLength(hipGroundMapPosition.Item1, hipGroundMapPosition.Item2 + (facingRight ? targetRelPos : -targetRelPos));//target ground map position

        var gdPt = map.PointFromReducedPosition(j, a);
        var gdUp = map.NormalFromReducedPosition(j, a);
        var gdRight = gdUp.CWPerp();
        var target = gdPt + goalStepHt * gdUp;
        var error = target - effectorPos;
        var accel = stepAccel * error;
        AccelerateLegWithDamping(i, accel, gdRight, gdUp, stepDamping);

#if UNITY_EDITOR
        if (debugStepStats[i])
        {
            Vector2 hipPos = leg[i].JointPosition(0);
            Vector2 stepPos = map.PointFromReducedPosition(j, a) + goalStepHt * gdUp;
            Debug.DrawLine(hipPos, (Vector2)map.PointFromReducedPosition(hipGroundMapPosition.Item1, hipGroundMapPosition.Item2), Color.red);
            Debug.DrawLine(hipPos, (Vector2)effectorPos, Color.yellow);
            Debug.DrawLine(hipPos, stepPos, Color.green);

            Debug.Log($"Leg {i}: stepping {Stepping(i)}, dX {math.dot(error, gdRight)}, dY {math.dot(error, gdUp)}");
        }
#endif
    }

    private void FlipAngleLimits(int i)
    {
        for (int j = 0; j < leg[i].JointCount; j++)
        {
            leg[i].FlipAngleLimits(j);
        }
    }

    private void AccelerateLegWithDamping(int i, float2 accel, float2 dirX, float2 dirY, float damping)
    {
        ref var l = ref leg[i];

        for (int j = 0; j < l.JointCount; j++)
        {
            AccelerateLegSegmentWithDamping(ref l, j, accel, dirX, dirY, damping);
        }
    }

    private void AccelerateLegSegmentWithDamping(ref JointedChain l, int j, float2 accel, float2 dirX, float2 dirY, float damping)
    {
        var p = l.NextPosition(j);
        var v = l.body[j].GetWorldPointVelocity(p);
        var vX = Vector2.Dot(v, dirX);
        var vY = Vector2.Dot(v, dirY);

        accel -= damping * (Mathf.Abs(vX) * vX * dirX + Mathf.Abs(vY) * vY * dirY);
        l.body[j].ApplyForce(l.body[j].mass * accel, p);
    }

    private float HipSpeed(ref JointedChain l, Vector2 gdDir)
    {
        return Vector2.Dot(l.body[0].GetWorldPointVelocity(l.JointPosition(0)), gdDir);
    }

    private float GoalRelSpeedStepping(float stepTime, float stepSpeed, float hipSpeed)
    {
        return stepTime > stepDropPoint ?
            hipSpeed * stepSpeed * Mathf.Clamp01((stepStopPoint - stepTime) / (stepStopPoint - stepDropPoint))
            : hipSpeed * stepSpeed;
    }

    private float GoalRelSpeedResting(float stepTime, float restSpeed, float hipSpeed)
    {
        return stepTime < restDropPoint ?
            -hipSpeed * restSpeed * Mathf.Clamp01((restStopPoint - stepTime) / (restStopPoint - restDropPoint))
            : -hipSpeed * restSpeed;

    }

    private float AbsoluteSpeedFraction(float hipSpeed)
    {
        return Mathf.Abs(hipSpeed / speed1);
    }

    private float MaxStepHeight(float baseStepHeight, float speedFraction, bool grounded)
    {
        return grounded ? Mathf.Min(speedFraction, 1) * baseStepHeight : baseStepHeight;
    }

    private float StepHeight(float stepTime, float maxStepHeight)
    {
        var c = 1 - 2 * stepPeakTime;
        var denom = 1 / (1 - stepPeakTime);
        denom *= denom;
        return maxStepHeight * Mathf.Clamp01(denom * (stepTime + c) * (1 - stepTime));
    }

    private float StepHeight(float baseStepHeight, float stepTime, float hipSpeed, bool grounded)
    {
        var speedFrac = AbsoluteSpeedFraction(hipSpeed);
        var maxStepHt = MaxStepHeight(baseStepHeight, speedFrac, grounded);
        return StepHeight(stepTime, maxStepHt);
    }

    private static float StepPosition(float stepMax, float stepLength, float stepTime) => stepMax + (stepTime - 1) * stepLength;
}