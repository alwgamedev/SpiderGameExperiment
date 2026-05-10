using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.U2D.Physics;
using UnityEngine.Rendering;

//note to self:
//compiler will suspect a race condition and create an error whenever a parallel job's Execute(i) attempts to write to an array element that is *not at index i*,
//even if the jobs are written and scheduled in a thread-safe way (e.g. for constraints job or for NativeRefs like terminusAnchor).
//you need to use the [NativeDisableParallelForRestriction] attribute to get rid of the error.

[BurstCompile]
public struct ClearArrayJob<T> : IJob where T : unmanaged
{
    public NativeArray<T> array;

    public ClearArrayJob(NativeArray<T> array)
    {
        this.array = array;
    }

    public void Execute()
    {
        array.FillArray(default, 0, array.Length);
    }
}

[BurstCompile]
public struct CalculateRopeConstraints : IJobParallelFor
{
    [ReadOnly] public NativeArray<float2> position;
    [NativeDisableParallelForRestriction] public NativeArray<float2> constraintDelta;
    public readonly float nodeSpacing;
    public readonly float nodeSpacing2;
    public readonly float nodeMass;
    public readonly float sourceMass;
    public readonly float terminusMass;
    public readonly float constraintStiffness;
    public readonly int sourceIndex;
    public readonly int offset;

    /// <summary> 
    /// Pass terminusMass = infinity if static anchor, or terminusMass = anchor body mass for dynamic anchor.
    /// </summary>
    public CalculateRopeConstraints(NativeArray<float2> position, NativeArray<float2> constraintDelta,
        float nodeSpacing, float nodeMass, float sourceMass, float terminusMass, float constraintStiffness,
        int sourceIndex, int batch)
    {
        this.position = position;
        this.constraintDelta = constraintDelta;
        this.nodeSpacing = nodeSpacing;
        nodeSpacing2 = nodeSpacing * nodeSpacing;
        this.nodeMass = nodeMass;
        this.sourceMass = sourceMass;
        this.terminusMass = terminusMass;
        this.constraintStiffness = constraintStiffness;
        this.sourceIndex = sourceIndex;
        offset = sourceIndex + 1 + batch;
    }

    public void Execute(int i)
    {
        i = 2 * i + offset;

        if (i == sourceIndex + 1)
        {
            RopeJobUtils.CalculateFirstConstraint(i, position, constraintDelta, nodeSpacing, nodeSpacing2, nodeMass, sourceMass, constraintStiffness);
        }
        else if (i == position.Length - 1)
        {
            RopeJobUtils.CalculateLastConstraint(i, position, constraintDelta, nodeSpacing, nodeSpacing2, nodeMass, terminusMass, constraintStiffness);
        }
        else
        {
            RopeJobUtils.CalculateConstraint(i, position, constraintDelta, nodeSpacing, nodeSpacing2, constraintStiffness);
        }
    }
}

[BurstCompile]
public unsafe struct ApplyRopeConstraints : IJobParallelFor
{
    [ReadOnly] public NativeArray<float2> constraintDelta;
    [NativeDisableParallelForRestriction] public NativeArray<float2> position;
    [NativeDisableParallelForRestriction] public NativeArray<float2> lastPosition;
    [NativeDisableParallelForRestriction] public NativeArray<RopeCollisionDebugData> collisionData;
    [ReadOnly] public NativeParallelHashMap<uint, PhysicsCoreHelper.ShapeProxy> shapeCapture;
    [NativeDisableParallelForRestriction] public NativeReference<PhysicsShape> terminusAnchor;
    [NativeDisableParallelForRestriction] public NativeReference<float2> terminusAnchorLocalPos;
    [NativeDisableParallelForRestriction] public NativeReference<FastRope.TerminusAnchorMode> terminusAnchorMode;
    [ReadOnly] public NativeReference<PhysicsCoreHelper.ShapeProxy> anchorGeometry;
    [ReadOnly] public PhysicsWorld world;
    public readonly PhysicsQuery.QueryFilter collisionFilter;
    public readonly float nodeRadius;
    public readonly float collisionBounciness;
    public readonly float anchorCollisionBounciness;
    public readonly float dynamicAnchorPullForce;
    public readonly int offset;

    public ApplyRopeConstraints(NativeArray<float2> constraintDelta, NativeArray<float2> position,
        NativeArray<float2> lastPosition, NativeArray<RopeCollisionDebugData> collisionData, NativeParallelHashMap<uint, PhysicsCoreHelper.ShapeProxy> shapeCapture,
        NativeReference<PhysicsShape> terminusAnchor, NativeReference<float2> terminusAnchorLocalPos, NativeReference<FastRope.TerminusAnchorMode> terminusAnchorMode,
        NativeReference<PhysicsCoreHelper.ShapeProxy> anchorGeometry, PhysicsWorld world, PhysicsQuery.QueryFilter collisionFilter,
        float nodeRadius, float collisionBounciness, float anchorCollisionBounciness, float dynamicAnchorPullForce, int offset)
    {
        this.constraintDelta = constraintDelta;
        this.position = position;
        this.lastPosition = lastPosition;
        this.collisionData = collisionData;
        this.shapeCapture = shapeCapture;
        this.terminusAnchor = terminusAnchor;
        this.terminusAnchorLocalPos = terminusAnchorLocalPos;
        this.terminusAnchorMode = terminusAnchorMode;
        this.anchorGeometry = anchorGeometry;
        this.world = world;
        this.collisionFilter = collisionFilter;
        this.nodeRadius = nodeRadius;
        this.collisionBounciness = collisionBounciness;
        this.anchorCollisionBounciness = anchorCollisionBounciness;
        this.dynamicAnchorPullForce = dynamicAnchorPullForce;
        this.offset = offset;
    }

    public void Execute(int i)
    {
        i += offset;

        if (i == position.Length - 1)
        {
            switch (terminusAnchorMode.Value)
            {
                case FastRope.TerminusAnchorMode.notAnchored:
                    RopeJobUtils.MoveAndAnchorTerminus(constraintDelta[i], position, lastPosition, collisionData, shapeCapture, terminusAnchor, terminusAnchorLocalPos, terminusAnchorMode,
                        world, collisionFilter, nodeRadius, collisionBounciness, false);
                    break;
                case FastRope.TerminusAnchorMode.dynamicAnchor:
                    var anchorFilter = collisionFilter;
                    if (terminusAnchor.Value.isValid)
                    {
                        anchorFilter.hitCategories &= ~terminusAnchor.Value.contactFilter.categories;
                    }
                    RopeJobUtils.MoveDynamicAnchor(anchorGeometry.GetUnsafeReadOnlyPtr(), terminusAnchor.Value, terminusAnchorLocalPos.Value, constraintDelta[i], position, lastPosition, collisionData,
                        shapeCapture, world, anchorFilter, anchorCollisionBounciness, false);
                    break;
            }
        }
        else
        {
            RopeJobUtils.MoveNode(i, constraintDelta[i], position, lastPosition, collisionData, shapeCapture, world, collisionFilter, nodeRadius, collisionBounciness, false, out _);
        }
    }
}

[BurstCompile]
public unsafe struct IntegrateRope : IJobParallelFor
{
    [NativeDisableParallelForRestriction] public NativeArray<float2> position;
    [NativeDisableParallelForRestriction] public NativeArray<float2> lastPosition;
    [NativeDisableParallelForRestriction] public NativeReference<PhysicsShape> terminusAnchor;
    [NativeDisableParallelForRestriction] public NativeReference<float2> terminusAnchorLocalPos;
    [NativeDisableParallelForRestriction] public NativeArray<RopeCollisionDebugData> collisionData;
    [ReadOnly] public NativeParallelHashMap<uint, PhysicsCoreHelper.ShapeProxy> shapeCapture;
    [NativeDisableParallelForRestriction] public NativeReference<FastRope.TerminusAnchorMode> terminusAnchorMode;
    [ReadOnly] public NativeReference<PhysicsCoreHelper.ShapeProxy> anchorGeometry;
    [ReadOnly] public PhysicsWorld world;
    public readonly PhysicsQuery.QueryFilter collisionFilter;
    public readonly float2 gravity;
    public readonly float drag;
    public readonly float nodeRadius;
    public readonly float collisionBounciness;
    public readonly float anchorCollisionBounciness;
    public readonly float dt2;
    public readonly float timeScale;
    public readonly int offset;

    public IntegrateRope(NativeArray<float2> position, NativeArray<float2> lastPosition,
        NativeArray<RopeCollisionDebugData> collisionData, NativeParallelHashMap<uint, PhysicsCoreHelper.ShapeProxy> shapeCapture,
        NativeReference<PhysicsShape> terminusAnchor, NativeReference<float2> terminusAnchorLocalPos,
        NativeReference<FastRope.TerminusAnchorMode> terminusAnchorMode, NativeReference<PhysicsCoreHelper.ShapeProxy> anchorGeometry,
        PhysicsWorld world, PhysicsQuery.QueryFilter collisionFilter, float2 gravity, float drag, float nodeRadius, float collisionBounciness, float anchorCollisionBounciness,
        float dt2, float timeScale, int offset)
    {
        this.position = position;
        this.lastPosition = lastPosition;
        this.collisionData = collisionData;
        this.shapeCapture = shapeCapture;
        this.terminusAnchor = terminusAnchor;
        this.terminusAnchorLocalPos = terminusAnchorLocalPos;
        this.terminusAnchorMode = terminusAnchorMode;
        this.anchorGeometry = anchorGeometry;
        this.world = world;
        this.collisionFilter = collisionFilter;
        this.gravity = gravity;
        this.drag = drag;
        this.nodeRadius = nodeRadius;
        this.collisionBounciness = collisionBounciness;
        this.anchorCollisionBounciness = anchorCollisionBounciness;
        this.dt2 = dt2;
        this.timeScale = timeScale;
        this.offset = offset;
    }

    public void Execute(int i)
    {
        i += offset;
        var dp = position[i] - lastPosition[i];
        dp = (timeScale - drag * math.length(dp)) * dp + dt2 * gravity;

        if (i == position.Length - 1)
        {
            switch (terminusAnchorMode.Value)
            {
                case FastRope.TerminusAnchorMode.notAnchored:
                    RopeJobUtils.MoveAndAnchorTerminus(dp, position, lastPosition, collisionData, shapeCapture,
                        terminusAnchor, terminusAnchorLocalPos, terminusAnchorMode,
                        world, collisionFilter, nodeRadius, collisionBounciness, true);
                    break;
                case FastRope.TerminusAnchorMode.dynamicAnchor:
                    var anchorFilter = collisionFilter;
                    if (terminusAnchor.Value.isValid)
                    {
                        anchorFilter.hitCategories &= ~terminusAnchor.Value.contactFilter.categories;
                    }
                    RopeJobUtils.MoveDynamicAnchor(anchorGeometry.GetUnsafeReadOnlyPtr(), terminusAnchor.Value, terminusAnchorLocalPos.Value, dp, position, lastPosition, collisionData,
                        shapeCapture, world, anchorFilter, anchorCollisionBounciness, true);
                    break;
            }
        }
        else
        {
            RopeJobUtils.MoveNode(i, dp, position, lastPosition, collisionData, shapeCapture,
                world, collisionFilter, nodeRadius, collisionBounciness, true, out _);
        }
    }
}

[BurstCompile]
public struct CorrectRopeSourcePosition : IJob
{
    public NativeArray<float2> position;
    public NativeReference<float2> carryForce;
    public readonly int sourceIndex;
    public readonly float2 sourcePosition;

    public CorrectRopeSourcePosition(NativeArray<float2> position, NativeReference<float2> carryForce, int sourceIndex, float2 sourcePosition)
    {
        this.position = position;
        this.carryForce = carryForce;
        this.sourceIndex = sourceIndex;
        this.sourcePosition = sourcePosition;
    }

    public void Execute()
    {
        carryForce.Value = position[sourceIndex] - sourcePosition;
        position[sourceIndex] = sourcePosition;
    }
}

[BurstCompile]
public struct RopeReparametrization : IJob
{
    public NativeArray<float2> position;
    public NativeArray<float2> lastPosition;
    public NativeArray<float2> positionBuffer;
    public NativeArray<float2> lastPositionBuffer;
    public NativeReference<float> nodeSpacing;//not parallel job; we don't need the [NativeDisableParallelForRestriction]
    public NativeReference<int> sourceIndex;
    public readonly float length;//recompute before job
    public readonly int newSourceIndex;

    public RopeReparametrization(NativeArray<float2> position, NativeArray<float2> lastPosition, NativeArray<float2> positionBuffer, NativeArray<float2> lastPositionBuffer,
        NativeReference<float> nodeSpacing, NativeReference<int> sourceIndex,
        float length, int newSourceIndex)
    {
        this.position = position;
        this.lastPosition = lastPosition;
        this.positionBuffer = positionBuffer;
        this.lastPositionBuffer = lastPositionBuffer;
        this.nodeSpacing = nodeSpacing;
        this.sourceIndex = sourceIndex;
        this.length = length;
        this.newSourceIndex = newSourceIndex;
    }

    public void Execute()
    {
        int terminusIndex = position.Length - 1;
        float newNodeSpacing = length / (terminusIndex - newSourceIndex);
        int i = sourceIndex.Value;//start index of current segment we're copying from
        int j = newSourceIndex + 1;//index in rescaleBuffer that we're copying to
        float dt = newNodeSpacing / nodeSpacing.Value;//when we move one segment forward in new path, this is how many segments we cover in old path
        float t = dt;//time along current segment we're copying from (0 = nodes[i], 1 = nodes[i + 1])
        while (t > 1)
        {
            i++;
            t -= 1;
        }

        while (j < terminusIndex)
        {
            positionBuffer[j] = math.lerp(position[i], position[i + 1], t);
            lastPositionBuffer[j] = math.lerp(lastPosition[i], lastPosition[i + 1], t);

            t += dt;
            j++;
            while (t > 1)
            {
                i++;
                t -= 1;
            }
        }

        if (newSourceIndex > sourceIndex.Value)
        {
            for (int k = sourceIndex.Value + 1; k < newSourceIndex + 1; k++)
            {
                position[k] = position[sourceIndex.Value];
                RopeJobUtils.Anchor(k, position, lastPosition);
            }
        }

        sourceIndex.Value = newSourceIndex;
        nodeSpacing.Value = newNodeSpacing;
        int copyStart = newSourceIndex + 1;
        int copyCount = terminusIndex - copyStart;//don't need to copy at the last index; no change.

        var positionBufferSlice = new NativeSlice<float2>(positionBuffer, copyStart, copyCount);
        var positionSlice = new NativeSlice<float2>(position, copyStart, copyCount);
        positionSlice.CopyFrom(positionBufferSlice);

        var lastPositionBufferSlice = new NativeSlice<float2>(lastPositionBuffer, copyStart, copyCount);
        var lastPositionSlice = new NativeSlice<float2>(lastPosition, copyStart, copyCount);
        lastPositionSlice.CopyFrom(lastPositionBufferSlice);
    }
}

[BurstCompile]
public struct CalculateRopeMaxTension : IJob
{
    [ReadOnly] public NativeArray<float2> position;
    public NativeReference<float2> bbMin;//this job will also return bounding box containing all rope nodes
    public NativeReference<float2> bbMax;
    public NativeReference<float> maxTension;
    public readonly float nodeSpacing;
    public readonly int sourceIndex;

    public CalculateRopeMaxTension(NativeArray<float2> position, NativeReference<float2> bbMin, NativeReference<float2> bbMax,
        NativeReference<float> maxTension, float nodeSpacing, int sourceIndex)
    {
        this.position = position;
        this.bbMin = bbMin;
        this.bbMax = bbMax;
        this.maxTension = maxTension;
        this.nodeSpacing = nodeSpacing;
        this.sourceIndex = sourceIndex;
    }

    public void Execute()
    {
        float max = 0;
        bbMin.Value = position[sourceIndex];
        bbMax.Value = position[sourceIndex];
        for (int i = sourceIndex + 1; i < position.Length; i++)
        {
            max = math.max(max, (math.length(position[i] - position[i - 1]) - nodeSpacing) / nodeSpacing);
            bbMin.Value = math.min(position[i], bbMin.Value);
            bbMax.Value = math.max(position[i], bbMax.Value);
        }

        maxTension.Value = max;
    }
}