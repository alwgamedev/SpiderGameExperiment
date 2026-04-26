using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.U2D.Physics;
using UnityEngine;
using UnityEngine.Rendering;

[System.Serializable]
public struct RopeSettings
{
    public PhysicsMask collisionMask;//64 bit mask so put at top
    public float collisionBounciness;

    public float width;//full width of the rope (not half of it)
    public float minNodeSpacing;
    public float maxNodeSpacing;

    public float drag;//all nodes have same drag, and all nodes have same mass except terminus
    public float nodeMass;
    public float terminusMass;
    public float constraintStiffness;
    public int constraintIterations;
    public int itersPulledByOwner;
    public float dynamicAnchorPullForce;

    public readonly float NodeRadius => 0.5f * width;
    public readonly PhysicsQuery.QueryFilter CollisionFilter => new(PhysicsMask.All, collisionMask, PhysicsWorld.IgnoreFilter.IgnoreTriggerShapes);
}

public class FastRope
{
    //const float MAX_SPEED = 50f;
    //const float MAX_SPEED_SQRD = MAX_SPEED * MAX_SPEED;

    public enum TerminusAnchorMode
    {
        notAnchored, staticAnchor, dynamicAnchor
    }

    //ROPE DATA

    //public PhysicsBody owner;
    public RopeSettings settings;
    public PhysicsWorld ownerWorld;
    public float ownerMass;

    JobHandle jobHandle;

    float requestedNodeSpacing;
    NativeReference<float> nodeSpacing;

    AlwaysAccessibleNativeReference<int> sourceIndex;//effective beginning of rope (we set some nodes at beginning to sleep to maintain node spacing)
    NativeReference<PhysicsShape> terminusAnchor;
    NativeReference<float2> terminusAnchorLocalPos;
    AlwaysAccessibleNativeReference<TerminusAnchorMode> terminusAnchorMode;
    AlwaysAccessibleNativeReference<float2> carryForce;
    AlwaysAccessibleNativeReference<float> maxTension;


    //NODE DATA

    NativeArray<float2> position;
    NativeArray<float2> lastPosition;
    NativeArray<float2> positionBuffer;//for reparametrizing & rendering
    NativeArray<float2> lastPositionBuffer;
    NativeReference<float2> dynamicAnchorTerminusPositionStorage;

    NativeArray<float2> raycastDirections;


    //PROPERTIES

    public bool Enabled { get; private set; }
    public int NumNodes { get; private set; }
    public int TerminusIndex { get; private set; }
    public float Length { get; private set; }//recompute whenever length gets set via SetLength

    public float MaxTension => maxTension.Value;
    public float2 CarryForce => carryForce.Value;
    public bool TerminusAnchored => terminusAnchorMode.Value != TerminusAnchorMode.notAnchored;
    public float2 GrappleExtent { get; private set; }//recompute after each simulation step


    //LIFE CYCLE

    public FastRope(RopeSettings settings, PhysicsWorld ownerWorld, float ownerMass, float2 position, float length, int numNodes)
    {
        this.settings = settings;
        this.ownerWorld = ownerWorld;
        this.ownerMass = ownerMass;

        InitializeNAs(position, numNodes, settings.minNodeSpacing, settings.maxNodeSpacing, length);
        RecomputeLength();

        requestedNodeSpacing = nodeSpacing.Value;

        Enabled = true;
    }

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
            nodeSpacing.Value = Mathf.Clamp(length / TerminusIndex, settings.minNodeSpacing, settings.maxNodeSpacing);
            this.position.FillArray(position, 0, numNodes);
            lastPosition.CopyFrom(this.position);
        }

        UpdateLength();
        requestedNodeSpacing = nodeSpacing.Value;

        positionBuffer.CopyFrom(this.position);
        lastPositionBuffer.CopyFrom(this.position);

        maxTension.Value = 0;
        carryForce.Value = 0;
        GrappleExtent = 0;

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
        if (dynamicAnchorTerminusPositionStorage.IsCreated)
        {
            dynamicAnchorTerminusPositionStorage.Dispose();
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
        nodeSpacing = new(Mathf.Clamp(length / TerminusIndex, minNodeSpacing, maxNodeSpacing), Allocator.Persistent);
        sourceIndex = new(TerminusIndex - Mathf.Clamp((int)(length / nodeSpacing.Value), 1, TerminusIndex), Allocator.Persistent);

        terminusAnchor = new(Allocator.Persistent);
        terminusAnchorMode = new(TerminusAnchorMode.notAnchored, Allocator.Persistent);
        terminusAnchorLocalPos = new(Allocator.Persistent);

        this.position = new(numNodes, Allocator.Persistent);
        this.position.FillArray(position, 0, numNodes);
        lastPosition = new(numNodes, Allocator.Persistent);
        lastPosition.CopyFrom(this.position);

        positionBuffer = new(numNodes, Allocator.Persistent);
        positionBuffer.CopyFrom(this.position);
        lastPositionBuffer = new(numNodes, Allocator.Persistent);
        lastPositionBuffer.CopyFrom(this.position);
        dynamicAnchorTerminusPositionStorage = new(Allocator.Persistent);

        maxTension = new(Allocator.Persistent);
        carryForce = new(Allocator.Persistent);

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
        carryForce.Locked = true;
        maxTension.Locked = true;
    }

    private void UnlockWrappers()
    {
        sourceIndex.Locked = false;
        terminusAnchorMode.Locked = false;
        carryForce.Locked = false;
        maxTension.Locked = false;
    }

    private void DisposeWrappers()
    {
        sourceIndex.Dispose();
        terminusAnchorMode.Dispose();
        carryForce.Dispose();
        maxTension.Dispose();
    }


    //ROPE FUNCTIONS

    public void DrawGizmos()
    {
        Gizmos.color = Color.red;
        for (int i = sourceIndex.Value; i < positionBuffer.Length; i++)
        {
            Gizmos.DrawSphere((Vector2)positionBuffer[i], settings.NodeRadius);
        }
    }

    public void SetRenderPositions(Vector4[] renderData, float2 sourcePosition, float taperBaseScale, float taperLength)
    {
        float taperMult = taperBaseScale;
        var taperRate = (1 - taperBaseScale) / taperLength;

        var dSourcePos = sourcePosition - positionBuffer[sourceIndex.Value];

        for (int i = 0; i < positionBuffer.Length; i++)
        {
            float2 p;
            if (!(i > sourceIndex.Value))
            {
                p = sourcePosition;
            }
            else
            {
                p = positionBuffer[i];

                if (taperMult < 1)
                {
                    p += (1 - taperMult) * dSourcePos;
                    taperMult += taperRate * Vector2.Distance(renderData[i - 1], p);
                    taperMult = Mathf.Min(taperMult, 1);
                }
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

    public void Update(float2 sourcePosition, float dt)
    {
        jobHandle.Complete();
        UnlockWrappers();
        HandleLengthRequest();

        float terminusMass;
        if (TerminusAnchored)
        {
            //in case someday we have an anchor that could get destroyed or change rigidbody type...
            terminusAnchorMode.Value = terminusAnchor.Value.isValid ? terminusAnchor.Value.body.type == PhysicsBody.BodyType.Dynamic ?
                TerminusAnchorMode.dynamicAnchor : TerminusAnchorMode.staticAnchor : TerminusAnchorMode.notAnchored;
        }

        switch (terminusAnchorMode.Value)
        {
            case TerminusAnchorMode.staticAnchor:
                lastPosition[TerminusIndex] = SetTerminusToAnchorPosition();
                terminusMass = Mathf.Infinity;
                break;
            case TerminusAnchorMode.dynamicAnchor:
                lastPosition[TerminusIndex] = SetTerminusToAnchorPosition() - dt * (float2)terminusAnchor.Value.body.linearVelocity;
                terminusMass = terminusAnchor.Value.body.mass;
                break;
            default://not anchored
                terminusMass = settings.terminusMass;
                break;
        }

        float2 SetTerminusToAnchorPosition()
        {
            var p = terminusAnchor.Value.transform.TransformPoint(terminusAnchorLocalPos.Value);
            position[TerminusIndex] = p;
            return p;
        }

        SetSourcePosition(position, sourcePosition);//btw maybe we should set source node velocity (and integrate) now that we are on a one frame delay
        GrappleExtent = position[TerminusIndex] - position[sourceIndex.Value];
        CalculateMaxTension().Run();
        positionBuffer.CopyFrom(position);//copy positions for rendering (and we'll use lastPositionBuffer for constraint deltas)

        //ClampSpeed(dt);

        var integrateJob = IntegrateRope(dt * dt, 1);

        var clearConstraintDelta = new ClearArrayJob<float2>(lastPositionBuffer);
        var calculateConstraintsEven = CalculateConstraints(ownerMass, terminusMass, 0);
        var calculateConstraintOdd = CalculateConstraints(ownerMass, terminusMass, 1);
        var applyConstraints = ApplyConstraints();

        var numActive = TerminusIndex - sourceIndex.Value;
        var numOdd = numActive / 2;
        var numEven = numActive - numOdd;

        LockWrappers();

        jobHandle = integrateJob.Schedule(numActive, 16, jobHandle);

        //constraints pulling owner
        for (int i = 0; i < settings.constraintIterations; i++)
        {
            jobHandle = clearConstraintDelta.Schedule(jobHandle);
            jobHandle = calculateConstraintsEven.Schedule(numEven, 16, jobHandle);
            jobHandle = calculateConstraintOdd.Schedule(numOdd, 16, jobHandle);
            jobHandle = applyConstraints.Schedule(numActive + 1, 16, jobHandle);
        }

        //calculate carry force and put source node back to starting position
        jobHandle = CorrectSourcePosition(sourcePosition).Schedule(jobHandle);

        //constraints pulled by owner
        calculateConstraintsEven = CalculateConstraints(Mathf.Infinity, terminusMass, 0);
        calculateConstraintOdd = CalculateConstraints(Mathf.Infinity, terminusMass, 1);

        for (int i = 0; i < settings.itersPulledByOwner; i++)
        {
            jobHandle = clearConstraintDelta.Schedule(jobHandle);
            jobHandle = calculateConstraintsEven.Schedule(numEven, 16, jobHandle);
            jobHandle = calculateConstraintOdd.Schedule(numOdd, 16, jobHandle);
            jobHandle = applyConstraints.Schedule(numActive + 1, 16, jobHandle);
        }
    }

    //private void ClampSpeed(float dt)
    //{
    //    var dt2inv = 1 / (dt * dt);
    //    for (int i = sourceIndex.Value + 1; i < position.Length; i++)
    //    {
    //        var w = lastPosition[i] - position[i];
    //        var spd2 = math.lengthsq(w) * dt2inv;
    //        if (spd2 > MAX_SPEED_SQRD)
    //        {
    //            lastPosition[i] = position[i] + dt * MAX_SPEED * math.normalize(w);
    //        }
    //    }
    //}

    private void SetSourcePosition(NativeArray<float2> position, float2 sourcePosition)
    {
        position.FillArray(sourcePosition, 0, sourceIndex.Value + 1);
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
            float goalSpacing = nodeSpacing.Value > settings.maxNodeSpacing ? settings.maxNodeSpacing : settings.minNodeSpacing;
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


    //JOBS

    private IntegrateRope IntegrateRope(float dt2, float timeScale)
    {
        return new(position, lastPosition,
            terminusAnchor, terminusAnchorLocalPos, terminusAnchorMode.native,
            PhysicsWorld.defaultWorld, settings.CollisionFilter, PhysicsWorld.defaultWorld.gravity, settings.drag, settings.NodeRadius, settings.collisionBounciness,
            dt2, timeScale, sourceIndex.Value + 1);
    }

    /// <summary> Batch = 0 or 1 (because adjacent constraints may write to the same index in constraintDelta). </summary>
    private CalculateRopeConstraints CalculateConstraints(float sourceMass, float terminusMass, int batch)
    {
        return new(position, lastPosition, lastPositionBuffer, nodeSpacing.Value, settings.nodeMass, sourceMass, terminusMass, settings.constraintStiffness, sourceIndex.Value, batch);
    }

    private ApplyRopeConstraints ApplyConstraints()
    {
        return new(lastPositionBuffer, position, lastPosition, terminusAnchor, terminusAnchorLocalPos, terminusAnchorMode.native, ownerWorld, settings.CollisionFilter,
            settings.NodeRadius, settings.collisionBounciness, settings.dynamicAnchorPullForce, sourceIndex.Value);
    }

    private CorrectRopeSourcePosition CorrectSourcePosition(float2 sourcePosition)
    {
        return new(position, carryForce.native, sourceIndex.Value, sourcePosition);
    }

    private RopeReparametrization ReparametrizationJob(int newSourceIndex)
    {
        return new(position, lastPosition, positionBuffer, lastPositionBuffer, nodeSpacing,
            sourceIndex.native, Length, newSourceIndex);
    }

    private CalculateRopeMaxTension CalculateMaxTension()
    {
        return new(position, maxTension.native, nodeSpacing.Value, sourceIndex.Value);
    }
}