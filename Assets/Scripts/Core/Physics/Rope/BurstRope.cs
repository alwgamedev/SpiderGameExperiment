using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.U2D.Physics;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

public class BurstRope
{
    //ROPE DATA

    public UnityEvent TerminusBecameAnchored;

    public float width;//full width of the rope (not half of it)
    public float minNodeSpacing;
    public float maxNodeSpacing;
    NativeReference<float> nodeSpacing;

    public float drag;//all nodes have same drag, and all nodes have same mass except terminus
    public float nodeMass;
    public float terminusMass;
    public float dynamicAnchorPullForce;
    public float breakThreshold;
    public float carryForceSlackThreshold;
    public float carryForceInterval;
    public float constraintIterations;
    NativeReference<float2> carryForceDirection;
    NativeReference<float> carryForceMagnitude;
    NativeReference<float> maxTension;

    public enum TerminusAnchorMode
    {
        notAnchored, staticAnchor, dynamicAnchor
    }

    NativeReference<int> startIndex;//effective beginning of rope (we set some nodes at beginning of rope to sleep to maintain reasonable node spacing)
    NativeReference<TerminusAnchorMode> terminusAnchorMode;
    NativeReference<PhysicsShape> terminusAnchor;
    NativeReference<float2> terminusAnchorLocalPos;
    NativeReference<float2> forceToApplyToDynamicAnchor;


    //NODE DATA

    public float collisionSearchRadius;
    public float tunnelEscapeRadius;
    public float collisionThreshold;
    public float collisionBounciness;
    public PhysicsQuery.QueryFilter collisionFilter;
    NativeArray<float2> lastCollisionNormal;
    NativeArray<bool> nearCollision;
    NativeArray<bool> hadCollision;
    NativeReference<bool> collisionIsFailing;

    NativeArray<float2> position;
    NativeArray<float2> lastPosition;
    NativeArray<float2> positionBuffer;//for reparametrizing
    NativeArray<float2> lastPositionBuffer;

    NativeArray<float2> raycastDirections;


    //JOB HANDLE

    //JobHandle jobHandle;


    //BASIC PROPERTIES

    public bool Enabled { get; private set; }
    public int NumNodes { get; private set; }
    public int TerminusIndex { get; private set;}
    public float Length { get; private set; }//recompute whenever length gets set via SetLength

    //the following need to be recomputed after each simulation step:
    public float MaxTension { get; private set; }
    public float2 CarryForceDirection { get; private set; }
    public float CarryForceMagnitude { get; private set; }//separate from direction so you can do what you want with it (e.g. clamp it)
    public bool CollisionIsFailing { get; private set; }
    public bool TerminusAnchored { get; private set; }
    public float2 GrappleExtent { get; private set; }


    //LIFE CYCLE

    public BurstRope(float2 position, float width, float length, int numNodes, float minNodeSpacing, float maxNodeSpacing,
        float nodeMass, float terminusMass, float nodeDrag, PhysicsMask collisionMask, float collisionSearchRadius, float tunnelEscapeRadius, float collisionBounciness,
        int constraintIterations, float dynamicAnchorPullForce, float breakThreshold,
        float carryForceSlackThreshold, float carryForceInterval, 
        UnityEvent terminusBecameAnchored)
    {
        this.dynamicAnchorPullForce = dynamicAnchorPullForce;
        this.width = width;
        this.minNodeSpacing = minNodeSpacing;
        this.maxNodeSpacing = maxNodeSpacing;
        this.constraintIterations = constraintIterations;
        this.breakThreshold = breakThreshold;
        this.carryForceSlackThreshold = carryForceSlackThreshold;
        this.carryForceInterval = carryForceInterval;

        drag = nodeDrag;
        this.nodeMass = nodeMass;
        this.terminusMass = terminusMass;

        SetCollisionMask(collisionMask);
        this.collisionSearchRadius = collisionSearchRadius;
        this.tunnelEscapeRadius = tunnelEscapeRadius;
        collisionThreshold = 0.5f * width;
        this.collisionBounciness = collisionBounciness;

        TerminusBecameAnchored = terminusBecameAnchored;

        InitializeNAs(position, numNodes, minNodeSpacing, maxNodeSpacing, length);
        RecomputeLength();

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
            InitializeNAs(position, numNodes, minNodeSpacing, maxNodeSpacing, length);
        }
        else
        {
            nodeSpacing.Value = math.clamp(length / TerminusIndex, minNodeSpacing, maxNodeSpacing);
            this.position.FillArray(position, 0, numNodes);
            lastPosition.FillArray(position, 0, numNodes);
            nearCollision.FillArray(false, 0, numNodes);
            hadCollision.FillArray(false, 0, numNodes);
            lastCollisionNormal.FillArray(0, 0, numNodes);
        }

        RecomputeLength();
        maxTension.Value = 0;
        MaxTension = 0;
        carryForceDirection.Value = 0;
        CarryForceDirection = 0;
        carryForceMagnitude.Value = 0;
        CarryForceMagnitude = 0;
        collisionIsFailing.Value = false;
        GrappleExtent = 0;
        CollisionIsFailing = false;
        TerminusAnchored = false;

        Enabled = true;
    }

    public void Disable()
    {
        //if (!jobHandle.IsCompleted)
        //{
        //    jobHandle.Complete();//we could also
        //}

        Enabled = false;
        //terminusAnchorMode.Value = TerminusAnchorMode.notAnchored;
        //terminusAnchor = default;
    }

    public void Dispose()
    {
        if (startIndex.IsCreated)
        {
            startIndex.Dispose();
        }
        if (nodeSpacing.IsCreated)
        {
            nodeSpacing.Dispose();
        }

        if (terminusAnchor.IsCreated)
        {
            terminusAnchor.Dispose();
        }
        if (terminusAnchorMode.IsCreated)
        {
            terminusAnchorMode.Dispose();
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

        if (maxTension.IsCreated)
        {
            maxTension.Dispose();
        }
        if (carryForceDirection.IsCreated)
        {
            carryForceDirection.Dispose();
        }
        if (carryForceMagnitude.IsCreated)
        {
            carryForceMagnitude.Dispose();
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
        if (collisionIsFailing.IsCreated)
        {
            collisionIsFailing.Dispose();
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
        startIndex = new(TerminusIndex - math.clamp((int)(length / nodeSpacing.Value), 1, TerminusIndex), Allocator.Persistent);

        terminusAnchor = new(Allocator.Persistent);
        terminusAnchorMode = new(TerminusAnchorMode.notAnchored, Allocator.Persistent);
        terminusAnchorLocalPos = new(Allocator.Persistent);
        forceToApplyToDynamicAnchor = new(Allocator.Persistent);

        this.position = new(numNodes, Allocator.Persistent);
        lastPosition = new(numNodes, Allocator.Persistent);
        this.position.FillArray(position, 0, numNodes);
        lastPosition.FillArray(position, 0, numNodes);

        positionBuffer = new(numNodes, Allocator.Persistent);
        lastPositionBuffer = new(numNodes, Allocator.Persistent);

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


    //ROPE FUNCTIONS

    public void DrawGizmos()
    {
        Gizmos.color = Color.red;
        for (int i = 0; i < position.Length; i++)
        {
            Gizmos.DrawSphere((Vector2)position[i], 0.5f * width);
        }
    }

    public void SetRenderPositions(Vector4[] renderData, float taperBaseScale, float taperLength)
    {
        float taperMult = taperBaseScale;
        var taperRate = (1 - taperBaseScale) / taperLength;
        for (int i = 0; i < position.Length; i++)
        {
            if (taperMult < 1 && i > startIndex.Value)
            {
                taperMult += Mathf.Min(taperRate * Vector2.Distance(renderData[i - 1], position[i]), 1);
            }
            renderData[i] = new(position[i].x, position[i].y, taperMult, 0);
        }
    }

    public void SetCollisionMask(PhysicsMask collisionMask)
    {
        collisionFilter = new(PhysicsMask.All, collisionMask, PhysicsWorld.IgnoreFilter.IgnoreTriggerShapes);
    }

    public void Shoot(float2 shootVelocity, float dt)
    {
        lastPosition[^1] -= dt * shootVelocity;
    }

    public void SetStartPosition(float2 position)
    {
        for (int i = 0; i < startIndex.Value + 1; i++)
        {
            this.position[i] = position;
        }
    }

    public void SetTerminusToAnchorPosition()
    {
        var anchor = terminusAnchor.Value;
        if (anchor.isValid)
        {
            var dp = position[TerminusIndex] - lastPosition[TerminusIndex];
            float2 p = anchor.transform.TransformPoint(terminusAnchorLocalPos.Value);
            position[TerminusIndex] = p;
            lastPosition[TerminusIndex] = p - dp;//keep velocity the same
        }
    }

    public void SetLength(float length)
    {
        nodeSpacing.Value = length / (TerminusIndex - startIndex.Value);
        RecomputeLength();

        if (nodeSpacing.Value < minNodeSpacing || nodeSpacing.Value > maxNodeSpacing)
        {
            float goalSpacing = nodeSpacing.Value < minNodeSpacing ? minNodeSpacing : maxNodeSpacing;
            int newStartIndex = TerminusIndex - Mathf.Clamp((int)(Length / goalSpacing), 1, TerminusIndex);
            //note: length / nodeSpacing = num nodes past start index

            if (startIndex.Value != newStartIndex)
            {
                RopeReparametrizationJob(newStartIndex).Run();
                RecomputeLength();
            }
        }
    }

    public void Update(float dt, float dt2)
    {
        bool terminusWasAnchored = TerminusAnchored;
        if (terminusWasAnchored)
        {
            //check every update in case someday we have an anchor that could get destroyed or change rigidbody type...
            terminusAnchorMode.Value = terminusAnchor.Value.body.isValid ? terminusAnchor.Value.body.type == PhysicsBody.BodyType.Dynamic ?
                TerminusAnchorMode.dynamicAnchor : TerminusAnchorMode.staticAnchor : TerminusAnchorMode.notAnchored;
            TerminusAnchored = terminusAnchorMode.Value != TerminusAnchorMode.notAnchored;
        }

        switch (terminusAnchorMode.Value)
        {
            case TerminusAnchorMode.notAnchored:
                UpdateInternal();
                break;
            case TerminusAnchorMode.staticAnchor:
                UpdateInternal();
                float2 p = terminusAnchor.Value.body.transform.TransformPoint(terminusAnchorLocalPos.Value);
                position[TerminusIndex] = p;
                lastPosition[TerminusIndex] = p;
                break;
            case TerminusAnchorMode.dynamicAnchor:
                UpdateWithDynamicAnchor();
                break;
        }

        void UpdateInternal()
        {
            IntegrateRope(dt2).Run(position.Length);
            ResolveCollision().Run(position.Length);
            for (int i = 0; i < constraintIterations; i++)
            {
                ConstraintIteration(0).Run(position.Length);
                ConstraintIteration(1).Run(position.Length);
            }
        }

        void UpdateWithDynamicAnchor()
        {
            IntegrateRope(dt2).Run(position.Length);

            Vector2 p = terminusAnchor.Value.body.transform.TransformPoint(terminusAnchorLocalPos.Value);
            position[TerminusIndex] = p;
            lastPosition[TerminusIndex] = p - dt * terminusAnchor.Value.body.linearVelocity;

            ResolveCollision().Run(position.Length);
            for (int i = 0; i < constraintIterations; i++)
            {
                ConstraintIteration(0).Run(position.Length);
                ConstraintIteration(1).Run(position.Length);
            }

            position[TerminusIndex] = p;
        }

        //recompute "stats"
        CalculateMaxTension().Run();
        CalculateCarryForceDirection().Run();
        CalculateCarryForceMagnitude().Run();
        //CheckForCollisionFailure().Run();

        RecomputeStats();

        if (!terminusWasAnchored && TerminusAnchored)
        {
            TerminusBecameAnchored.Invoke();
        }
    }

    private void RecomputeStats()
    {
        MaxTension = maxTension.Value;
        CarryForceDirection = carryForceDirection.Value;
        CarryForceMagnitude = carryForceMagnitude.Value;
        GrappleExtent = position[TerminusIndex] - position[startIndex.Value];
        CollisionIsFailing = collisionIsFailing.Value;
        TerminusAnchored = terminusAnchorMode.Value != TerminusAnchorMode.notAnchored;
    }

    private void RecomputeLength()
    {
        Length = nodeSpacing.Value * (TerminusIndex - startIndex.Value);
    }

    private IntegrateRope IntegrateRope(float dt2)
    {
        return new(position, lastPosition, PhysicsWorld.defaultWorld.gravity, drag, dt2, startIndex.Value, TerminusAnchored ? TerminusIndex : position.Length);
    }

    private ResolveRopeCollision ResolveCollision()
    {
        return new(position, lastPosition, lastCollisionNormal, nearCollision, hadCollision, raycastDirections, 
            terminusAnchor, terminusAnchorLocalPos, terminusAnchorMode,
            PhysicsWorld.defaultWorld, collisionFilter, collisionSearchRadius, collisionThreshold, tunnelEscapeRadius, collisionBounciness,
            startIndex.Value, TerminusAnchored ? TerminusIndex : position.Length, TerminusAnchored);
    }

    private RopeConstraintIteration ConstraintIteration(int nodeParity)
    {
        return new(position, lastPosition, lastCollisionNormal, nearCollision, hadCollision, raycastDirections,
            terminusAnchor, terminusAnchorLocalPos, forceToApplyToDynamicAnchor, terminusAnchorMode,
            PhysicsWorld.defaultWorld, collisionFilter, collisionSearchRadius, collisionThreshold, tunnelEscapeRadius,
            collisionBounciness, nodeSpacing.Value, nodeMass, terminusMass, dynamicAnchorPullForce,
            startIndex.Value, position.Length, nodeParity);
    }

    private RopeReparametrization RopeReparametrizationJob(int newStartIndex)
    {
        return new(position, lastPosition, positionBuffer, lastPositionBuffer, lastCollisionNormal, nearCollision, hadCollision, nodeSpacing,
            startIndex, Length, newStartIndex);
    }

    private CalculateRopeMaxTension CalculateMaxTension()
    {
        return new(position, maxTension, nodeSpacing.Value, startIndex.Value);
    }

    private CalculateRopeCarryForceDirection CalculateCarryForceDirection()
    {
        return new(position, nearCollision, carryForceDirection, startIndex.Value);
    }

    private CalculateRopeCarryForceMagnitude CalculateCarryForceMagnitude()
    {
        return new(position, nearCollision, carryForceMagnitude, nodeSpacing.Value, carryForceSlackThreshold, carryForceInterval, startIndex.Value);
    }

    private CheckForRopeCollisionFailure CheckForCollisionFailure()
    {
        return new(position, terminusAnchor, PhysicsWorld.defaultWorld, collisionIsFailing, collisionFilter, collisionThreshold, breakThreshold, startIndex.Value);
    }
}