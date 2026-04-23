using System;
using Unity.Mathematics;
using Unity.U2D.Physics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public struct LegSynchSettings
{
    public float[] stepStrength;
    public float stepHeightFraction;
    public float timeScale;
    public float stepDamping;
    public float gravityScale;
    public float strideMultiplier;
    public bool scaleStepHeightWithSpeed;

    public LegSynchSettings Clone()
    {
        var stepStrengthCopy = new float[stepStrength.Length];
        Array.Copy(stepStrength, stepStrengthCopy, stepStrength.Length);
        return new()
        {
            stepStrength = stepStrengthCopy,
            timeScale = timeScale,
            stepDamping = stepDamping,
            stepHeightFraction = stepHeightFraction,
            gravityScale = gravityScale,
            strideMultiplier = strideMultiplier,
            scaleStepHeightWithSpeed = scaleStepHeightWithSpeed
        };
    }

    public void CopyFrom(in LegSynchSettings settings)
    {
        Array.Copy(settings.stepStrength, stepStrength, stepStrength.Length);
        timeScale = settings.timeScale;
        stepDamping = settings.stepDamping;
        stepHeightFraction = settings.stepHeightFraction;
        gravityScale = settings.gravityScale;
        strideMultiplier = settings.strideMultiplier;
        scaleStepHeightWithSpeed = settings.scaleStepHeightWithSpeed;
    }
}

[Serializable]
public class LegSynchronizer
{
    public UnityEvent[] footHitGround;
    internal LegSynchSettings settings;
    //internal LegSynchSettings settings;

    [SerializeField] JointedChainDefinition chainDef;
    [SerializeField] JointedChainSettings[] chainSettings;
    [SerializeField] ArrayContainer<Transform>[] chainTransform;
    [SerializeField] ArrayContainer<Transform>[] bones;
    [SerializeField] float[] timer;//for initial positions: use [0, 1) to begin stepping, and use (1, 2] to begin resting (will subtract 1 off at start)
    [SerializeField] int beginStepping;
    [SerializeField] float[] stepMax;
    [SerializeField] float[] stepLength;
    [SerializeField] float[] stepSpeed;
    [SerializeField] float[] restSpeed;
    [SerializeField] float[] stepHeight;
    [SerializeField] int[] castDirectionIndex;
    [SerializeField] float stepAccel;
    [SerializeField] float stepPeakTime;
    [SerializeField] float stepDropPoint;//no upward forces will be applied beyond this point
    [SerializeField] float stepStopPoint;
    [SerializeField] float restDropPoint;
    [SerializeField] float restStopPoint;
    [SerializeField] float restingStepHeight;//make this a small negative number to give legs some extra encouragement to reach ground
    [SerializeField] float speed0;
    [SerializeField] float speed1;
#if UNITY_EDITOR
    [SerializeField] bool drawBodyGizmos;
    [SerializeField] bool[] drawAngleLimitGizmos;
    [SerializeField] bool[] debugStepStats;
#endif

    JointedChain[] leg;
    int stepping;//used as bit mask (bit i = whether leg i is stepping)
    int grounded;
    float totalMass;

    public float TotalMass => totalMass;

    public bool Stepping(int i) => (stepping & (1 << i)) != 0;

    public bool LegGrounded(int i) => (grounded & (1 << i)) != 0;

    public bool AnyLegGrounded() => grounded != 0;

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

            RecalculateMass();
            SetGravityScale(settings.gravityScale);
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

        leg = new JointedChain[numLegs];
        stepping = 0;
        grounded = 0;

        for (int i = 0; i < chainTransform.Length; i++)
        {
            InitializeLeg(i, anchorBody[i]);
            if ((beginStepping & 1 << i) != 0)
            {
                stepping |= 1 << i;
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

    public void UpdateSettings(in LegSynchSettings settings)
    {
        this.settings.CopyFrom(settings);
        SetGravityScale(settings.gravityScale);
    }

    public void SetGravityScale(float gravityScale)
    {
        for (int i = 0; i < leg.Length; i++)
        {
            for (int j = 0; j < leg[i].JointCount; j++)
            {
                leg[i].body[j].gravityScale = settings.gravityScale;
            }
        }
    }

    public void UpdateAllLegs(float dt, GroundMap map, Vector2[] castDirection, bool facingRight)
    {
        for (int i = 0; i < leg.Length; i++)
        {
            var castDir = castDirection[castDirectionIndex[i]];
            var (j, s) = map.LineCastOrClosest(leg[i].JointPosition(0), castDir, GroundMap.DEFAULT_PRECISION);//hip ground point

            float hipSpeed;
            var n = map.NormalFromReducedPosition(j, s);//normal at hip ground point
            var hipGroundDir = facingRight ? n.CWPerp() : n.CCWPerp();
            hipSpeed = HipSpeed(ref leg[i], hipGroundDir);

            if (Mathf.Abs(hipSpeed) < speed0)
            {
                hipSpeed = 0;
            }
            else
            {
                hipSpeed *= settings.timeScale;
            }

            var t = timer[i];

            bool goalForward = Stepping(i) ^ hipSpeed < 0;
            if (goalForward ? t > 1 : t < 0)
            {
                stepping ^= 1 << i;//flip i-th bit
            }

            if (Stepping(i))
            {
                var goalRelSpeed = GoalRelSpeedStepping(t, stepSpeed[i], hipSpeed);
                var speedFraction = settings.scaleStepHeightWithSpeed ? Mathf.Min(Mathf.Abs(hipSpeed) / speed1, 1) : 1;
                var goalStepHt = restingStepHeight + StepHeight(t, speedFraction * settings.stepHeightFraction * stepHeight[i] - restingStepHeight);
                UpdateLeg(i, dt, map, facingRight, (j, s), goalRelSpeed, goalStepHt);
            }
            else
            {
                var goalRelSpeed = GoalRelSpeedResting(t, restSpeed[i], hipSpeed);
                UpdateLeg(i, dt, map, facingRight, (j, s), goalRelSpeed, restingStepHeight);
            }
        }
    }

    private void UpdateLeg(int i, float dt, GroundMap map, bool facingRight, (int, float) hipGroundMapPosition,
        float goalRelSpd, float goalStepHt)
    {
        ref var l = ref leg[i];
        float2 effectorPos = l.EffectorPosition;

        timer[i] += dt * goalRelSpd / stepLength[i];

        var targetRelPos = settings.strideMultiplier * StepPosition(stepMax[i], stepLength[i], timer[i]);//target position along ground map, relative to hip
        var (j, a) = map.AddArcLength(hipGroundMapPosition.Item1, hipGroundMapPosition.Item2 + (facingRight ? targetRelPos : -targetRelPos));//target ground map position
        var legGrounded = map.HitGround(j);
        if (legGrounded)
        {
            grounded |= 1 << i;
        }
        else
        {
            grounded &= ~(1 << i);
        }

        var gdPt = map.PointFromReducedPosition(j, a);
        var gdUp = map.NormalFromReducedPosition(j, a);
        var gdRight = gdUp.CWPerp();
        var target = gdPt + goalStepHt * gdUp;
        var error = target - effectorPos;
        var accel = settings.stepStrength[i] * stepAccel * error;
        AccelerateLegWithDamping(ref l, accel, gdRight, gdUp, settings.stepDamping);

#if UNITY_EDITOR
        if (debugStepStats[i])
        {
            Vector2 hipPos = leg[i].JointPosition(0);
            Vector2 stepPos = map.PointFromReducedPosition(j, a) + goalStepHt * gdUp;
            Debug.DrawLine(hipPos, (Vector2)map.PointFromReducedPosition(hipGroundMapPosition.Item1, hipGroundMapPosition.Item2), Color.orange);
            Debug.DrawLine(hipPos, (Vector2)effectorPos, Color.yellow);
            Debug.DrawLine(hipPos, stepPos, Color.green);

            Debug.Log($"Leg {i}: stepping {Stepping(i)}, errorX {math.dot(error, gdRight)}, errorY {math.dot(error, gdUp)}");
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

    private static void AccelerateLegWithDamping(ref JointedChain l, float2 accel, float2 dirX, float2 dirY, float damping)
    {
        for (int j = 0; j < l.JointCount; j++)
        {
            AccelerateLegSegmentWithDamping(ref l, j, accel, dirX, dirY, damping);
        }
    }

    private static void AccelerateLegSegmentWithDamping(ref JointedChain l, int j, float2 accel, float2 dirX, float2 dirY, float damping)
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
        return Vector2.Dot(l.AnchorBody.GetWorldPointVelocity(l.JointPosition(0)), gdDir);
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

    private float StepHeight(float stepTime, float maxStepHeight)
    {
        var c = 1 - 2 * stepPeakTime;
        var denom = 1 / (1 - stepPeakTime);
        denom *= denom;
        return maxStepHeight * Mathf.Clamp01(denom * (stepTime + c) * (1 - stepTime));
    }

    private static float StepPosition(float stepMax, float stepLength, float stepTime) => stepMax + (stepTime - 1) * stepLength;
}