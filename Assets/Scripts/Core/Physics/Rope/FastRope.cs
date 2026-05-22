using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.U2D.Physics;
using UnityEngine;
using UnityEngine.Rendering;

[System.Serializable]
public struct RopeSettings
{
    public float collisionBounciness;
    public float dynamicCollisionForce;

    public float width;//full width of the rope (not half of it)
    public float minNodeSpacing;
    public float maxNodeSpacing;

    public float drag;//all nodes have same drag, and all nodes have same mass except terminus
    public float nodeMass;
    public float terminusMass;
    public float constraintStiffness;
    public int constraintIterations;
    public int itersPulledByOwner;
    public float dynamicAnchorSpringForce;
    public float dynamicAnchorSpringForceCap;

    public readonly float NodeRadius => 0.5f * width;
}

public unsafe class FastRope
{
    public enum TerminusAnchorMode
    {
        notAnchored, staticAnchor, dynamicAnchor
    }

    //ROPE DATA

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
    NativeArray<float2> lastPositionBuffer;//also used to store constraint deltas
    NativeArray<float4> constraintDeltaF4;


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
        if (constraintDeltaF4.IsCreated)
        {
            constraintDeltaF4.Dispose();
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
        constraintDeltaF4 = new(numNodes, Allocator.Persistent);

        maxTension = new(Allocator.Persistent);
        carryForce = new(Allocator.Persistent);
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
        for (int i = sourceIndex.Value; i < positionBuffer.Length; i++)
        {
            Gizmos.color = Color.blue;
            Vector2 p = positionBuffer[i];
            Gizmos.DrawSphere(p, settings.NodeRadius);
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

    public void CompleteJobs()
    {
        jobHandle.Complete();
    }

    public void Update(float2 sourcePosition, float dt, PhysicsQuery.QueryFilter collisionFilter, NativeArray<PhysicsCoreHelper.ShapeProxyForJobs> shapeCapture)
    {
        jobHandle.Complete();
        UnlockWrappers();
        HandleLengthRequest();

        if (TerminusAnchored)
        {
            //check if anchor was destroyed or changed body type
            terminusAnchorMode.Value = terminusAnchor.Value.isValid ? terminusAnchor.Value.body.type == PhysicsBody.BodyType.Dynamic ?
                TerminusAnchorMode.dynamicAnchor : TerminusAnchorMode.staticAnchor : TerminusAnchorMode.notAnchored;
        }

        float terminusMass;
        switch (terminusAnchorMode.Value)
        {
            case TerminusAnchorMode.staticAnchor:
                {
                    var p = terminusAnchor.Value.transform.TransformPoint(terminusAnchorLocalPos.Value);
                    position[TerminusIndex] = p;
                    lastPosition[TerminusIndex] = p;
                    terminusMass = Mathf.Infinity;
                    break;
                }
            case TerminusAnchorMode.dynamicAnchor:
                {
                    var anchorBody = terminusAnchor.Value.body;
                    float2 p = anchorBody.transform.TransformPoint(terminusAnchorLocalPos.Value);
                    var f = DynamicAnchorForce(p);
                    anchorBody.ApplyLinearImpulse(f, p);
                    var ptVelocity = anchorBody.GetWorldPointVelocity(p);
                    position[TerminusIndex] = p + dt * (float2)(ptVelocity - dt * anchorBody.world.gravity);//predict next terminus position -- this actually makes a big difference
                    lastPosition[TerminusIndex] = p;//in case we lose anchor/gets destroyed, we keep accurate velocity
                    terminusMass = Mathf.Infinity;//anchorBody.mass;
                    break;
                }
            default://not anchored
                terminusMass = settings.terminusMass;
                break;
        }

        SetSourcePosition(position, sourcePosition);//btw maybe we should set source node velocity (and integrate) now that we are on a one frame delay
        GrappleExtent = position[TerminusIndex] - position[sourceIndex.Value];
        positionBuffer.CopyFrom(position);//copy positions for rendering (and we'll use lastPositionBuffer for constraint deltas)

        CalculateMaxTension().Run();

        LockWrappers();

        var integrateJob = IntegrateRope(dt * dt, 1, collisionFilter, shapeCapture);
        var clearConstraintDelta = new ClearArrayJob<float4>(constraintDeltaF4);
        var calculateConstraints = CalculateConstraintsF4(ownerMass, terminusMass);
        var applyConstraints = ApplyConstraintF4(collisionFilter, shapeCapture);

        var numActive = TerminusIndex - sourceIndex.Value;

        jobHandle = integrateJob.Schedule(TerminusAnchored ? numActive - 1 : numActive, 16, jobHandle);

        //constraints pulling owner
        for (int i = 0; i < settings.constraintIterations; i++)
        {
            jobHandle = clearConstraintDelta.Schedule(jobHandle);
            jobHandle = calculateConstraints.Schedule(numActive, 16, jobHandle);
            jobHandle = applyConstraints.Schedule(numActive + 1, 16, jobHandle);
        }

        //calculate carry force and put source node back to starting position
        jobHandle = CorrectSourcePosition(sourcePosition).Schedule(jobHandle);

        //constraints pulled by owner
        calculateConstraints = CalculateConstraintsF4(Mathf.Infinity, terminusMass);

        for (int i = 0; i < settings.itersPulledByOwner; i++)
        {
            jobHandle = clearConstraintDelta.Schedule(jobHandle);
            jobHandle = calculateConstraints.Schedule(numActive, 16, jobHandle);
            jobHandle = applyConstraints.Schedule(numActive + 1, 16, jobHandle);
        }
    }

    private float2 DynamicAnchorForce(Vector2 terminusPosition)
    {
        var d = (Vector2)position[^2] - terminusPosition;
        var l = d.SqrMagnitude();
        var nodeSpacing = this.nodeSpacing.Value;

        if (l > nodeSpacing * nodeSpacing)
        {
            l = Mathf.Sqrt(l);
            var max = nodeSpacing * settings.dynamicAnchorSpringForceCap;
            return settings.dynamicAnchorSpringForce * Mathf.Min(l - nodeSpacing, max) / l * d;
        }
        else
        {
            return default;
        }
    }

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

    private IntegrateRope IntegrateRope(float dt2, float timeScale, PhysicsQuery.QueryFilter collisionFilter, NativeArray<PhysicsCoreHelper.ShapeProxyForJobs> shapeCapture)
    {
        return new(position, lastPosition, shapeCapture,
            terminusAnchor, terminusAnchorLocalPos, terminusAnchorMode.native,
            ownerWorld, collisionFilter, ownerWorld.gravity, settings.drag, settings.NodeRadius, settings.nodeMass, settings.collisionBounciness, settings.dynamicCollisionForce,
            dt2, timeScale, sourceIndex.Value + 1);
    }

    private CalculateRopeConstraintsF4 CalculateConstraintsF4(float sourceMass, float terminusMass)
    {
        return new(position, constraintDeltaF4, nodeSpacing.Value, settings.nodeMass, sourceMass, terminusMass, settings.constraintStiffness, sourceIndex.Value);
    }

    /// <summary> Batch = 0 or 1 (because adjacent constraints may write to the same index in constraintDelta). </summary>
    private CalculateRopeConstraints CalculateConstraints(float sourceMass, float terminusMass, int batch)
    {
        return new(position, lastPositionBuffer, nodeSpacing.Value, settings.nodeMass, sourceMass, terminusMass, settings.constraintStiffness, sourceIndex.Value, batch);
    }

    private ApplyRopeConstraintsF4 ApplyConstraintF4(PhysicsQuery.QueryFilter collisionFilter, NativeArray<PhysicsCoreHelper.ShapeProxyForJobs> shapeCapture)
    {
        return new(constraintDeltaF4, position, lastPosition, shapeCapture,
            terminusAnchor, terminusAnchorLocalPos, terminusAnchorMode.native, ownerWorld, collisionFilter,
            settings.NodeRadius, settings.nodeMass, settings.collisionBounciness, settings.dynamicCollisionForce, sourceIndex.Value);
    }

    private ApplyRopeConstraints ApplyConstraints(PhysicsQuery.QueryFilter collisionFilter, NativeArray<PhysicsCoreHelper.ShapeProxyForJobs> shapeCapture)
    {
        return new(lastPositionBuffer, position, lastPosition, shapeCapture,
            terminusAnchor, terminusAnchorLocalPos, terminusAnchorMode.native, ownerWorld, collisionFilter,
            settings.NodeRadius, settings.nodeMass, settings.collisionBounciness, settings.dynamicCollisionForce, sourceIndex.Value);
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
        return new(position, /*bbMin, bbMax,*/ maxTension.native, nodeSpacing.Value, sourceIndex.Value);
    }
}