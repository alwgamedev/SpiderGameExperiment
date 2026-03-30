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
    public enum TerminusAnchorMode
    {
        notAnchored, staticAnchor, dynamicAnchor
    }

    //ROPE DATA

    public PhysicsBody owner;
    public RopeSettings settings;

    JobHandle jobHandle;
    float lastJobStartTime;
    float lastDt;

    float requestedNodeSpacing;
    NativeReference<float> nodeSpacing;

    AlwaysAccessibleNativeReference<int> sourceIndex;//effective beginning of rope (we set some nodes at beginning of rope to sleep to maintain reasonable node spacing)
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

    public FastRope(PhysicsBody owner, RopeSettings settings, float2 position, float length, int numNodes)
    {
        this.owner = owner;
        this.settings = settings;

        InitializeNAs(position, numNodes, settings.minNodeSpacing, settings.maxNodeSpacing, length);
        RecomputeLength();

        //lastJobStartTime = Time.time;
        //lastDt = Time.fixedDeltaTime;
        lastDt = 0f;
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
            nodeSpacing.Value = math.clamp(length / TerminusIndex, settings.minNodeSpacing, settings.maxNodeSpacing);
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

        lastDt = 0;//Time.fixedDeltaTime;

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
        nodeSpacing = new(math.clamp(length / TerminusIndex, minNodeSpacing, maxNodeSpacing), Allocator.Persistent);
        sourceIndex = new(TerminusIndex - math.clamp((int)(length / nodeSpacing.Value), 1, TerminusIndex), Allocator.Persistent);

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

    public void Update(float2 sourcePosition)
    {
        //if (!jobHandle.IsCompleted)
        //{
        //    return;
        //}

        //jobHandle.Complete();
        //UnlockWrappers();


        //PROCESS LAST JOB'S RESULTS
        //float dt;
        //float timeScale;
        //if (lastDt == 0)
        //{
        //    dt = Time.fixedDeltaTime;
        //    timeScale = 1;
        //    lastJobStartTime = Time.time;
        //    lastDt = Time.fixedDeltaTime;
        //}
        //else
        //{
        //    dt = Time.time - lastJobStartTime;
        //    timeScale = dt / lastDt;
        //    lastJobStartTime = Time.time;
        //    lastDt = dt;
        //}

        if (TerminusAnchored)
        {
            //in case someday we have an anchor that could get destroyed or change rigidbody type...
            terminusAnchorMode.Value = terminusAnchor.Value.isValid ? terminusAnchor.Value.body.type == PhysicsBody.BodyType.Dynamic ?
                TerminusAnchorMode.dynamicAnchor : TerminusAnchorMode.staticAnchor : TerminusAnchorMode.notAnchored;
        }
        if (TerminusAnchored)
        {
            var p = terminusAnchor.Value.transform.TransformPoint(terminusAnchorLocalPos.Value);
            position[TerminusIndex] = p;
            if (terminusAnchorMode.Value == TerminusAnchorMode.staticAnchor)
            {
                lastPosition[TerminusIndex] = p;
            }
        }

        var dt = Time.fixedDeltaTime;
        var timeScale = 1;
        var terminusMass = terminusAnchorMode.Value == TerminusAnchorMode.staticAnchor ? math.INFINITY : settings.terminusMass;

        SetSourcePosition(position, sourcePosition);

        var integrateJob = IntegrateRope(dt * dt, timeScale);
        var constraintIter = new SimpleConstraint(position, lastPosition, positionBuffer, terminusAnchor, terminusAnchorLocalPos,
            terminusAnchorMode.native, owner.world, settings.CollisionFilter, settings.constraintStiffness, settings.NodeRadius, nodeSpacing.Value, settings.nodeMass,
            owner.mass, terminusMass, settings.collisionBounciness, sourceIndex.Value);

        integrateJob.Run(TerminusIndex - sourceIndex.Value);
        for (int j = 0; j < settings.constraintIterations; j++)
        {
            constraintIter.Run();
        }

        carryForce.Value = position[sourceIndex.Value] - sourcePosition;
        position[sourceIndex.Value] = sourcePosition;

        constraintIter = new SimpleConstraint(position, lastPosition, positionBuffer, terminusAnchor, terminusAnchorLocalPos,
            terminusAnchorMode.native, owner.world, settings.CollisionFilter, settings.constraintStiffness, settings.NodeRadius, nodeSpacing.Value, settings.nodeMass,
            math.INFINITY, terminusMass, settings.collisionBounciness, sourceIndex.Value);

        for (int j = 0; j < settings.itersPulledByOwner; j++)
        {
            constraintIter.Run();
        }

        CalculateMaxTension().Run();
        HandleLengthRequest();
        positionBuffer.CopyFrom(position);

        GrappleExtent = position[TerminusIndex] - position[sourceIndex.Value];


        //PREPARE NEXT JOB
        //SetSourcePosition(position, sourcePosition);

        //if (TerminusAnchored)
        //{
        //    //in case someday we have an anchor that could get destroyed or change rigidbody type...
        //    terminusAnchorMode.Value = terminusAnchor.Value.isValid ? terminusAnchor.Value.body.type == PhysicsBody.BodyType.Dynamic ?
        //        TerminusAnchorMode.dynamicAnchor : TerminusAnchorMode.staticAnchor : TerminusAnchorMode.notAnchored;
        //}
        //if (terminusAnchorMode.Value == TerminusAnchorMode.staticAnchor)
        //{
        //    var p = terminusAnchor.Value.transform.TransformPoint(terminusAnchorLocalPos.Value);
        //    position[TerminusIndex] = p;
        //    lastPosition[TerminusIndex] = p;
        //}

        //LockWrappers();

        //switch (terminusAnchorMode.Value)
        //{
        //    case TerminusAnchorMode.notAnchored:
        //        Integrate();//Schedule(TerminusIndex - sourceIndex.Value, 32, jobHandle);
        //        Constraints();
        //        break;
        //    case TerminusAnchorMode.staticAnchor:
        //        //jobHandle = IntegrateRope(dt * dt, timeScale).Schedule(TerminusIndex - sourceIndex.Value, 32, jobHandle);
        //        Integrate();
        //        Constraints();
        //        //jobHandle = CompleteConstraintsWithStaticAnchor().Schedule(jobHandle);//idk just so final positions for rendering are as current as possible
        //        break;
        //    case TerminusAnchorMode.dynamicAnchor:
        //        jobHandle = IntegrateRope(dt * dt, timeScale).Schedule(TerminusIndex - sourceIndex.Value, 32, jobHandle);
        //        jobHandle = PrepareForConstraintsWithDynamicAnchor(dt).Schedule(jobHandle);
        //        Constraints();
        //        jobHandle = CompleteConstraintsWithDynamicAnchor().Schedule(jobHandle);
        //        break;
        //}

        //void Integrate()
        //{
        //    IntegrateRope(dt * dt, timeScale).Run(TerminusIndex - sourceIndex.Value);
        //}

        //void Constraints()
        //{
        //    for (int i = 0; i < settings.constraintIterationsPullingOwner; i++)
        //    {
        //        SingleThreadedConstraints(true).Run();
        //    }
        //    carryForce.Value = position[sourceIndex.Value] - sourcePosition;//do both (see below)
        //    position[sourceIndex.Value] = sourcePosition;
        //    for (int i = 0; i < settings.constraintIterationsPulledByOwner; i++)
        //    {
        //        SingleThreadedConstraints(false).Run();
        //    }

        //    var d = position[sourceIndex.Value + 1] - position[sourceIndex.Value];//do both (see above)
        //    var l = math.length(d);
        //    if (l > nodeSpacing.Value)
        //    {
        //        carryForce.Value += (l - nodeSpacing.Value) / l * d;
        //    }
        //    //jobHandle = CorrectSourcePosition(sourcePosition).Schedule(jobHandle);
        //    //for (int i = 0; i < settings.constraintIterationsPulledByOwner; i++)
        //    //{
        //    //    jobHandle = SingleThreadedConstraints(false).Schedule(jobHandle);
        //    //}
        //    //var numActive = TerminusIndex - sourceIndex.Value;
        //    //var g = settings.constraintGroupSize;
        //    //var q = numActive / g;
        //    //var r = numActive % g;

        //    //int ArrLength(int b) => math.select(q, q + 1, b < r);

        //    //for (int i = 0; i < settings.constraintIterationsPullingOwner; i++)
        //    //{
        //    //    for (int b = 0; b < g; b++)
        //    //    {
        //    //        jobHandle = GroupedConstraintIteration(g, b, true).Schedule(ArrLength(b), 16, jobHandle);
        //    //    }
        //    //}

        //    //jobHandle = CorrectSourcePosition(sourcePosition).Schedule(jobHandle);

        //    //for (int i = 0; i < settings.constraintIterationsPulledByOwner; i++)
        //    //{
        //    //    for (int b = 0; b < g; b++)
        //    //    {
        //    //        jobHandle = GroupedConstraintIteration(g, b, false).Schedule(ArrLength(b), 16, jobHandle);
        //    //    }
        //    //}
        //}
    }

    private void SetSourcePosition(NativeArray<float2> position, float2 sourcePosition)
    {
        position.FillArray(sourcePosition, 0, sourceIndex.Value + 1);
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
            float goalSpacing = math.select(settings.minNodeSpacing, settings.maxNodeSpacing, nodeSpacing.Value > settings.maxNodeSpacing);
            //nodeSpacing.Value < settings.minNodeSpacing ? settings.minNodeSpacing : settings.maxNodeSpacing;
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

    private RopeConstraintIteration ConstraintIteration(int batch, bool pullOwner)
    {
        return new(position, lastPosition,
            terminusAnchor, terminusAnchorLocalPos, terminusAnchorMode.native,
            PhysicsWorld.defaultWorld, settings.CollisionFilter,
            settings.collisionBounciness, nodeSpacing.Value, settings.NodeRadius,
            settings.nodeMass, pullOwner ? owner.mass : math.INFINITY,
            settings.terminusMass, settings.dynamicAnchorPullForce,
            sourceIndex.Value, batch);
    }

    private RopeGroupedConstraintIteration GroupedConstraintIteration(int groupSize, int batch, bool pullOwner)
    {
        return new(position, lastPosition,
            terminusAnchor, terminusAnchorLocalPos, terminusAnchorMode.native,
            PhysicsWorld.defaultWorld, settings.CollisionFilter,
            settings.collisionBounciness, nodeSpacing.Value, settings.NodeRadius,
            settings.nodeMass, pullOwner ? owner.mass : math.INFINITY,
            settings.terminusMass, settings.dynamicAnchorPullForce,
            sourceIndex.Value, groupSize, batch);
    }

    private CorrectRopeSourcePosition CorrectSourcePosition(float2 sourcePosition)
    {
        return new(position, carryForce.native, sourceIndex.Value, sourcePosition);
    }

    private CompleteRopeConstraintsWithStaticAnchor CompleteConstraintsWithStaticAnchor()
    {
        return new(position, lastPosition, terminusAnchor, terminusAnchorLocalPos);
    }

    private PrepareForRopeConstraintsWithDynamicAnchor PrepareForConstraintsWithDynamicAnchor(float dt)
    {
        return new(position, lastPosition, terminusAnchor, terminusAnchorLocalPos,
            dynamicAnchorTerminusPositionStorage, dt);
    }

    private CompleteRopeConstraintsWithDynamicAnchor CompleteConstraintsWithDynamicAnchor()
    {
        return new(position, lastPosition, dynamicAnchorTerminusPositionStorage);//use lastPositionBuffer here and we'll use positionBuffer for render positions
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