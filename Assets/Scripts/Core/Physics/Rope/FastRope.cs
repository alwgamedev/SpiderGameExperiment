using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.U2D.Physics;
using UnityEngine;
using UnityEngine.Rendering;
using static MiscTools;

[System.Serializable]
public struct RopeSettings
{
    public float width;//full width of the rope (not half of it)
    public float minNodeSpacing;
    public float maxNodeSpacing;

    public float drag;//all nodes have same drag, and all nodes have same mass except terminus
    public float nodeMass;
    public float sourceMass;
    public float terminusMass;
    public float dynamicAnchorPullForce;
    public float breakThreshold;
    public float carryForceSlackThreshold;
    public float carryForceInterval;
    public float constraintIterations;

    public float collisionSearchRadius;
    public float tunnelEscapeRadius;
    public float collisionBounciness;
    public PhysicsMask collisionMask;

    public readonly float CollisionThreshold => 0.5f * width;
    public readonly PhysicsQuery.QueryFilter CollisionFilter => new(PhysicsMask.All, collisionMask, PhysicsWorld.IgnoreFilter.IgnoreTriggerShapes);
}

public class FastRope
{
    public enum TerminusAnchorMode
    {
        notAnchored, staticAnchor, dynamicAnchor
    }

    //ROPE DATA

    public RopeSettings settings;

    JobHandle jobHandle;
    float lastJobStartTime;
    float lastDt;

    float requestedNodeSpacing;
    NativeReference<float> nodeSpacing;

    AlwaysAccessibleNativeReference<int> sourceIndex;//effective beginning of rope (we set some nodes at beginning of rope to sleep to maintain reasonable node spacing)
    NativeReference<PhysicsShape> terminusAnchor;
    NativeReference<float2> terminusAnchorLocalPos;
    NativeReference<float2> forceToApplyToDynamicAnchor;
    AlwaysAccessibleNativeReference<TerminusAnchorMode> terminusAnchorMode;

    AlwaysAccessibleNativeReference<float2> carryForceDirection;
    AlwaysAccessibleNativeReference<float> carryForceMagnitude;
    AlwaysAccessibleNativeReference<float> maxTension;


    //NODE DATA

    NativeArray<float2> lastCollisionNormal;
    NativeArray<bool> nearCollision;
    NativeArray<bool> hadCollision;
    AlwaysAccessibleNativeReference<bool> collisionIsFailing;

    NativeArray<float2> position;
    NativeArray<float2> lastPosition;
    NativeArray<float2> positionBuffer;//for reparametrizing
    NativeArray<float2> lastPositionBuffer;

    NativeArray<float2> raycastDirections;


    //PROPERTIES

    public bool Enabled { get; private set; }
    public int NumNodes { get; private set; }
    public int TerminusIndex { get; private set;}
    public float Length { get; private set; }//recompute whenever length gets set via SetLength

    //the following need to be recomputed after each simulation step:
    public float MaxTension => maxTension.Value;
    public float2 CarryForceDirection => carryForceDirection.Value;
    public float CarryForceMagnitude => carryForceMagnitude.Value;//separate from direction so you can do what you want with it (e.g. clamp it)
    public bool CollisionIsFailing => collisionIsFailing.Value;
    public bool TerminusAnchored => terminusAnchorMode.Value != TerminusAnchorMode.notAnchored;
    public float2 GrappleExtent { get; private set; }


    //LIFE CYCLE

    public FastRope(RopeSettings settings, float2 position, float length, int numNodes)
    {
        this.settings = settings;
        InitializeNAs(position, numNodes, settings.minNodeSpacing, settings.maxNodeSpacing, length);
        RecomputeLength();

        lastJobStartTime = Time.time;
        lastDt = Time.fixedDeltaTime;
        requestedNodeSpacing = nodeSpacing.Value;

        Enabled = true;
    }

    //to do: only allow changing num nodes in editor
    public void Respawn(float2 position, float length, int numNodes)
    {
        terminusAnchorMode.Value = TerminusAnchorMode.notAnchored;
        terminusAnchor.Value = default;

        if (this.position.Length != numNodes)
        {
            Dispose();
            InitializeNAs(position, numNodes, settings.minNodeSpacing, settings.maxNodeSpacing, length);
        }
        else
        {
            nodeSpacing.Value = math.clamp(length / TerminusIndex, settings.minNodeSpacing, settings.maxNodeSpacing);
            this.position.FillArray(position, 0, numNodes);
            lastPosition.CopyFrom(this.position);
            nearCollision.FillArray(false, 0, numNodes);
            hadCollision.CopyFrom(nearCollision);
            lastCollisionNormal.FillArray(0, 0, numNodes);
        }

        UpdateLength();
        requestedNodeSpacing = nodeSpacing.Value;

        positionBuffer.CopyFrom(this.position);
        lastPositionBuffer.CopyFrom(this.position);

        maxTension.Value = 0;
        carryForceDirection.Value = 0;
        carryForceMagnitude.Value = 0;
        collisionIsFailing.Value = false;
        GrappleExtent = 0;

        lastJobStartTime = Time.time;
        lastDt = Time.fixedDeltaTime;

        Enabled = true;
    }

    public void Disable()
    {
        jobHandle.Complete();
        UnlockWrappers();
        Enabled = false;
    }

    public void Dispose()
    {
        jobHandle.Complete();

        DisposeWrappers();

        if (nodeSpacing.IsCreated)
        {
            nodeSpacing.Dispose();
        }

        if (terminusAnchor.IsCreated)
        {
            terminusAnchor.Dispose();
        }
        if (terminusAnchorLocalPos.IsCreated)
        {
            terminusAnchorLocalPos.Dispose();
        }
        if (forceToApplyToDynamicAnchor.IsCreated)
        {
            forceToApplyToDynamicAnchor.Dispose();
        }

        if (position.IsCreated)
        {
            position.Dispose();
        }
        if (lastPosition.IsCreated)
        {
            lastPosition.Dispose();
        }
        if (positionBuffer.IsCreated)
        {
            positionBuffer.Dispose();
        }
        if (lastPositionBuffer.IsCreated)
        {
            lastPositionBuffer.Dispose();
        }

        if (lastCollisionNormal.IsCreated)
        {
            lastCollisionNormal.Dispose();
        }
        if (nearCollision.IsCreated)
        {
            nearCollision.Dispose();
        }
        if (hadCollision.IsCreated)
        {
            hadCollision.Dispose();
        }

        if (raycastDirections.IsCreated)
        {
            raycastDirections.Dispose();
        }
    }

    private void InitializeNAs(float2 position, int numNodes, float minNodeSpacing, float maxNodeSpacing, float length)
    {
        TerminusIndex = numNodes - 1;
        NumNodes = numNodes;
        nodeSpacing = new(math.clamp(length / TerminusIndex, minNodeSpacing, maxNodeSpacing), Allocator.Persistent);
        sourceIndex = new(TerminusIndex - math.clamp((int)(length / nodeSpacing.Value), 1, TerminusIndex), Allocator.Persistent);

        terminusAnchor = new(Allocator.Persistent);
        terminusAnchorMode = new(TerminusAnchorMode.notAnchored, Allocator.Persistent);
        terminusAnchorLocalPos = new(Allocator.Persistent);
        forceToApplyToDynamicAnchor = new(Allocator.Persistent);

        this.position = new(numNodes, Allocator.Persistent);
        this.position.FillArray(position, 0, numNodes);
        lastPosition = new(numNodes, Allocator.Persistent);
        lastPosition.CopyFrom(this.position);

        positionBuffer = new(numNodes, Allocator.Persistent);
        positionBuffer.CopyFrom(this.position);
        lastPositionBuffer = new(numNodes, Allocator.Persistent);
        lastPositionBuffer.CopyFrom(this.position);

        maxTension = new(Allocator.Persistent);
        carryForceDirection = new(Allocator.Persistent);
        carryForceMagnitude = new(Allocator.Persistent);

        lastCollisionNormal = new(numNodes, Allocator.Persistent);
        nearCollision = new(numNodes, Allocator.Persistent);
        hadCollision = new(numNodes, Allocator.Persistent);
        collisionIsFailing = new(Allocator.Persistent);

        raycastDirections = new(8, Allocator.Persistent);
        raycastDirections[0] = new(0, -1);
        raycastDirections[1] = new(0, 1);
        raycastDirections[2] = new(1, 0);
        raycastDirections[3] = new(-1, 0);
        raycastDirections[4] = new(MathTools.cos45, -MathTools.cos45);
        raycastDirections[5] = new(-MathTools.cos45, MathTools.cos45);
        raycastDirections[6] = new(-MathTools.cos45, -MathTools.cos45);
        raycastDirections[7] = new(MathTools.cos45, MathTools.cos45);
    }

    private void LockWrappers()
    {
        sourceIndex.Locked = true;
        terminusAnchorMode.Locked = true;
        carryForceDirection.Locked = true;
        carryForceMagnitude.Locked = true;
        maxTension.Locked = true;
        collisionIsFailing.Locked = true;
    }

    private void UnlockWrappers()
    {
        sourceIndex.Locked = false;
        terminusAnchorMode.Locked = false;
        carryForceDirection.Locked = false;
        carryForceMagnitude.Locked = false;
        maxTension.Locked = false;
        collisionIsFailing.Locked = false;
    }

    private void DisposeWrappers()
    {
        sourceIndex.Dispose();
        terminusAnchorMode.Dispose();
        carryForceDirection.Dispose();
        carryForceMagnitude.Dispose();
        maxTension.Dispose();
        collisionIsFailing.Dispose();
    }


    //ROPE FUNCTIONS

    public void DrawGizmos()
    {
        Gizmos.color = Color.red;
        for (int i = 0; i < position.Length; i++)
        {
            Gizmos.DrawSphere((Vector2)position[i], 0.5f * settings.width);
        }
    }

    public void SetRenderPositions(Vector4[] renderData, float2 sourcePosition, float taperBaseScale, float taperLength)
    {
        float taperMult = taperBaseScale;
        var taperRate = (1 - taperBaseScale) / taperLength;

        for (int i = 0; i < positionBuffer.Length; i++)
        {
            float2 p;
            if (!(i > sourceIndex.Value))
            {
                p = sourcePosition;
            }
            else
            {
                if (taperMult < 1)
                {
                    taperMult += Mathf.Min(taperRate * Vector2.Distance(positionBuffer[i - 1], positionBuffer[i]), 1);
                }
                p = positionBuffer[i];
            }

            renderData[i] = new(p.x, p.y, taperMult, 0);
        }
    }

    public void Shoot(float2 shootVelocity, float dt)
    {
        shootVelocity *= dt;
        lastPosition[TerminusIndex] -= shootVelocity;

        var denom = 1f / (TerminusIndex - sourceIndex.Value);
        for (int i = sourceIndex.Value + 1; i < TerminusIndex; i++)
        {
            var t = Mathf.Lerp(0.25f, 0.75f, denom * (i - sourceIndex.Value));
            lastPosition[i] -= t * shootVelocity;
        }
    }

    public void RequestLengthChange(float length)
    {
        requestedNodeSpacing = length / (TerminusIndex - sourceIndex.Value);
    }

    public void Update(float2 sourcePosition)
    {
        if (!jobHandle.IsCompleted)
        {
            return;
        }

        jobHandle.Complete();
        UnlockWrappers();

        //PROCESS LAST JOB'S RESULTS

        var dt = Time.time - lastJobStartTime;
        var timeScale = dt / lastDt;
        lastJobStartTime = Time.time;
        lastDt = dt;

        CalculateMaxTension().Run();//done as jobs for burst compile
        CalculateCarryForceDirection().Run();
        CalculateCarryForceMagnitude().Run();
        CheckForCollisionFailure().Run();

        HandleLengthRequest();
        positionBuffer.CopyFrom(position);

        GrappleExtent = position[TerminusIndex] - position[sourceIndex.Value];


        //PREPARE NEXT JOB

        SetSourcePosition(position, sourcePosition);

        if (TerminusAnchored)
        {
            //in case someday we have an anchor that could get destroyed or change rigidbody type...
            terminusAnchorMode.Value = terminusAnchor.Value.isValid ? terminusAnchor.Value.body.type == PhysicsBody.BodyType.Dynamic ?
                TerminusAnchorMode.dynamicAnchor : TerminusAnchorMode.staticAnchor : TerminusAnchorMode.notAnchored;
        }

        LockWrappers();

        switch (terminusAnchorMode.Value)
        {
            case TerminusAnchorMode.notAnchored:
                jobHandle = IntegrateRope(dt * dt, timeScale).Schedule(TerminusIndex - sourceIndex.Value, 32, jobHandle);
                Constraints();
                break;
            case TerminusAnchorMode.staticAnchor:
                jobHandle = IntegrateRope(dt * dt, timeScale).Schedule(TerminusIndex - sourceIndex.Value, 32, jobHandle);
                Constraints();
                jobHandle = CompleteConstraintsWithStaticAnchor().Schedule(jobHandle);
                break;
            case TerminusAnchorMode.dynamicAnchor:
                jobHandle = IntegrateRope(dt * dt, timeScale).Schedule(TerminusIndex - sourceIndex.Value, 32, jobHandle);
                jobHandle = PrepareForConstraintsWithDynamicAnchor(dt).Schedule(jobHandle);
                Constraints();
                jobHandle = CompleteConstraintsWithDynamicAnchor().Schedule(jobHandle);
                break;
        }

        void Constraints()
        {
            int numActive = TerminusIndex - sourceIndex.Value;
            jobHandle = ResolveCollision().Schedule(TerminusAnchored ? numActive : numActive - 1, 16, jobHandle);

            for (int i = 0; i < settings.constraintIterations; i++)
            {
                jobHandle = ConstraintIteration(0).Schedule((numActive + 1) / 2, 16, jobHandle);
                jobHandle = ConstraintIteration(1).Schedule(numActive / 2, 16, jobHandle);
            }
        }
    }

    private void SetSourcePosition(NativeArray<float2> position, float2 sourcePosition)
    {
        for (int i = 0; i < sourceIndex.Value + 1; i++)
        {
            position[i] = sourcePosition;
        }
    }

    public void SetTerminusPosition()
    {
        if (terminusAnchor.Value.isValid)
        {
            var dp = position[TerminusIndex] - lastPosition[TerminusIndex];
            float2 p = terminusAnchor.Value.transform.TransformPoint(terminusAnchorLocalPos.Value);
            position[TerminusIndex] = p;
            lastPosition[TerminusIndex] = p - dp;//keep velocity the same
        }
    }

    private void HandleLengthRequest()
    {
        if (requestedNodeSpacing != nodeSpacing.Value)
        {
            nodeSpacing.Value = requestedNodeSpacing;
            UpdateLength();
        }

    }

    private void UpdateLength()
    {
        RecomputeLength();

        if (nodeSpacing.Value < settings.minNodeSpacing || nodeSpacing.Value > settings.maxNodeSpacing)
        {
            float goalSpacing = nodeSpacing.Value < settings.minNodeSpacing ? settings.minNodeSpacing : settings.maxNodeSpacing;
            int newSourceIndex = TerminusIndex - Mathf.Clamp((int)(Length / goalSpacing), 1, TerminusIndex);
            //note: length / nodeSpacing = num nodes past source index

            if (sourceIndex.Value != newSourceIndex)
            {
                ReparametrizationJob(newSourceIndex).Run();
                RecomputeLength();
                requestedNodeSpacing = nodeSpacing.Value;
            }
        }
    }

    private void RecomputeLength()
    {
        Length = nodeSpacing.Value * (TerminusIndex - sourceIndex.Value);
    }

    private IntegrateRope IntegrateRope(float dt2, float timeScale)
    {
        return new(position, lastPosition, PhysicsWorld.defaultWorld.gravity, settings.drag, dt2, timeScale, sourceIndex.Value);
    }

    private ResolveRopeCollision ResolveCollision()
    {
        return new(position, lastPosition, lastCollisionNormal, nearCollision, hadCollision, raycastDirections, 
            terminusAnchor, terminusAnchorLocalPos, terminusAnchorMode.native,
            PhysicsWorld.defaultWorld, settings.CollisionFilter, settings.collisionSearchRadius, settings.CollisionThreshold, settings.tunnelEscapeRadius, settings.collisionBounciness,
            sourceIndex.Value);
    }

    private RopeConstraintIteration ConstraintIteration(int batch)
    {
        return new(position, lastPosition, lastCollisionNormal, nearCollision, hadCollision, raycastDirections,
            terminusAnchor, terminusAnchorLocalPos, forceToApplyToDynamicAnchor, terminusAnchorMode.native,
            PhysicsWorld.defaultWorld, settings.CollisionFilter, settings.collisionSearchRadius, settings.CollisionThreshold, settings.tunnelEscapeRadius,
            settings.collisionBounciness, nodeSpacing.Value, settings.nodeMass, settings.terminusMass, settings.dynamicAnchorPullForce,
            sourceIndex.Value, batch);
    }

    private CompleteRopeConstraintsWithStaticAnchor CompleteConstraintsWithStaticAnchor()
    {
        return new(position, lastPosition, terminusAnchor, terminusAnchorLocalPos);
    }

    private PrepareForRopeConstraintsWithDynamicAnchor PrepareForConstraintsWithDynamicAnchor(float dt)
    {
        return new(position, positionBuffer, lastPosition, terminusAnchor, terminusAnchorLocalPos, dt);
    }

    private CompleteRopeConstraintsWithDynamicAnchor CompleteConstraintsWithDynamicAnchor()
    {
        return new(position, lastPositionBuffer, lastPosition);//use lastPositionBuffer here and we'll use positionBuffer for render positions
    }

    private RopeReparametrization ReparametrizationJob(int newSourceIndex)
    {
        return new(position, lastPosition, positionBuffer, lastPositionBuffer, lastCollisionNormal, nearCollision, hadCollision, nodeSpacing,
            sourceIndex.native, Length, newSourceIndex);
    }

    private CalculateRopeMaxTension CalculateMaxTension()
    {
        return new(position, maxTension.native, nodeSpacing.Value, sourceIndex.Value);
    }

    private CalculateRopeCarryForceDirection CalculateCarryForceDirection()
    {
        return new(position, nearCollision, carryForceDirection.native, sourceIndex.Value);
    }

    private CalculateRopeCarryForceMagnitude CalculateCarryForceMagnitude()
    {
        return new(position, nearCollision, carryForceMagnitude.native, nodeSpacing.Value, settings.carryForceSlackThreshold, settings.carryForceInterval,
            sourceIndex.Value);
    }

    private CheckForRopeCollisionFailure CheckForCollisionFailure()
    {
        return new(position, terminusAnchor, PhysicsWorld.defaultWorld, collisionIsFailing.native, settings.CollisionFilter, 
            settings.CollisionThreshold, settings.breakThreshold, sourceIndex.Value);
    }
}