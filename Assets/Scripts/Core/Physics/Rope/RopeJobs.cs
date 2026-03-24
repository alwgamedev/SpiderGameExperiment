using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.U2D.Physics;

//note to self:
//compiler will suspect a race condition and create an error whenever a parallel job's Execute(i) attempts to write to an array element that is *not at index i*,
//even if the jobs are written and scheduled in a thread-safe way (e.g. for constraints job or for NativeRefs like terminusAnchor).
//you need to use the [NativeDisableParallelForRestriction] attribute to get rid of the error.

[BurstCompile]
public struct IntegrateRope : IJobParallelFor
{
    [NativeDisableParallelForRestriction] public NativeArray<float2> position;
    [NativeDisableParallelForRestriction] public NativeArray<float2> lastPosition;
    [NativeDisableParallelForRestriction] public NativeArray<float2> lastCollisionNormal;
    [NativeDisableParallelForRestriction] public NativeArray<bool> collisionStatus;
    [NativeDisableParallelForRestriction] public NativeReference<PhysicsShape> terminusAnchor;
    [NativeDisableParallelForRestriction] public NativeReference<float2> terminusAnchorLocalPos;
    [NativeDisableParallelForRestriction] public NativeReference<FastRope.TerminusAnchorMode> terminusAnchorMode;
    [ReadOnly] public PhysicsWorld world;
    public readonly PhysicsQuery.QueryFilter collisionFilter;
    public readonly float2 gravity;
    public readonly float drag;
    public readonly float nodeRadius;
    public readonly float collisionBounciness;
    public readonly float dt2;
    public readonly float timeScale;
    public readonly int offset;

    public IntegrateRope(NativeArray<float2> position, NativeArray<float2> lastPosition, NativeArray<float2> lastCollisionNormal, NativeArray<bool> collisionStatus, 
        NativeReference<PhysicsShape> terminusAnchor, NativeReference<float2> terminusAnchorLocalPos, NativeReference<FastRope.TerminusAnchorMode> terminusAnchorMode, 
        PhysicsWorld world, PhysicsQuery.QueryFilter collisionFilter, float2 gravity, float drag, float nodeRadius, float collisionBounciness, float dt2, float timeScale, 
        int offset)
    {
        this.position = position;
        this.lastPosition = lastPosition;
        this.lastCollisionNormal = lastCollisionNormal;
        this.collisionStatus = collisionStatus;
        this.terminusAnchor = terminusAnchor;
        this.terminusAnchorLocalPos = terminusAnchorLocalPos;
        this.terminusAnchorMode = terminusAnchorMode;
        this.world = world;
        this.collisionFilter = collisionFilter;
        this.gravity = gravity;
        this.drag = drag;
        this.nodeRadius = nodeRadius;
        this.collisionBounciness = collisionBounciness;
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
            RopeJobUtils.MoveAndAnchorTerminus(dp, position, lastPosition, lastCollisionNormal, collisionStatus,
                terminusAnchor, terminusAnchorLocalPos, terminusAnchorMode,
                world, collisionFilter, nodeRadius, collisionBounciness, true);
        }
        else
        {
            RopeJobUtils.MoveNode(i, dp, position, lastPosition, lastCollisionNormal, collisionStatus,
                world, collisionFilter, nodeRadius, collisionBounciness, true, out _);
        }
    }
}

[BurstCompile]
public struct RopeConstraintIteration : IJobParallelFor
{
    [NativeDisableParallelForRestriction] public NativeArray<float2> position;
    [NativeDisableParallelForRestriction] public NativeArray<float2> lastPosition;
    [NativeDisableParallelForRestriction] public NativeArray<float2> lastCollisionNormal;
    [NativeDisableParallelForRestriction] public NativeArray<bool> collisionStatus;
    [NativeDisableParallelForRestriction] public NativeReference<PhysicsShape> terminusAnchor;
    [NativeDisableParallelForRestriction] public NativeReference<float2> terminusAnchorLocalPos;
    [NativeDisableParallelForRestriction] public NativeReference<FastRope.TerminusAnchorMode> terminusAnchorMode;
    [ReadOnly] public PhysicsWorld world;
    public readonly PhysicsQuery.QueryFilter collisionFilter;
    public readonly float collisionBounciness;
    public readonly float nodeSpacing;
    public readonly float nodeRadius;
    public readonly float nodeMass;
    public readonly float ownerMass;
    public readonly float terminusMass;
    public readonly float dynamicAnchorPullForce;
    public readonly int sourceIndex;
    public readonly int offset;

    public RopeConstraintIteration(NativeArray<float2> position, NativeArray<float2> lastPosition, NativeArray<float2> lastCollisionNormal, 
        NativeArray<bool> collisionStatus, NativeReference<PhysicsShape> terminusAnchor, NativeReference<float2> terminusAnchorLocalPos, 
        NativeReference<FastRope.TerminusAnchorMode> terminusAnchorMode, PhysicsWorld world, PhysicsQuery.QueryFilter collisionFilter,
        float collisionBounciness, float nodeSpacing, float nodeRadius, float nodeMass, float ownerMass, float terminusMass, float dynamicAnchorPullForce, 
        int sourceIndex, int batch)
    {
        this.position = position;
        this.lastPosition = lastPosition;
        this.lastCollisionNormal = lastCollisionNormal;
        this.collisionStatus = collisionStatus;
        this.terminusAnchor = terminusAnchor;
        this.terminusAnchorLocalPos = terminusAnchorLocalPos;
        this.terminusAnchorMode = terminusAnchorMode;
        this.world = world;
        this.collisionFilter = collisionFilter;
        this.collisionBounciness = collisionBounciness;
        this.nodeSpacing = nodeSpacing;
        this.nodeRadius = nodeRadius;
        this.nodeMass = nodeMass;
        this.ownerMass = ownerMass;
        this.terminusMass = terminusMass;
        this.dynamicAnchorPullForce = dynamicAnchorPullForce;
        this.sourceIndex = sourceIndex;
        offset = sourceIndex + 1 + batch;
    }

    public readonly void Execute(int i)
    {
        i = 2 * i + offset;
        RopeJobUtils.CoverAllSpacingConstraint(i, position, lastPosition, lastCollisionNormal, collisionStatus,
                terminusAnchor, terminusAnchorLocalPos, terminusAnchorMode,
                world, collisionFilter,  collisionBounciness, nodeSpacing, nodeRadius, nodeMass, ownerMass, terminusMass, dynamicAnchorPullForce, 
                sourceIndex);
    }
}

[BurstCompile]
public struct RopeGroupedConstraintIteration : IJobParallelFor
{
    [NativeDisableParallelForRestriction] public NativeArray<float2> position;
    [NativeDisableParallelForRestriction] public NativeArray<float2> lastPosition;
    [NativeDisableParallelForRestriction] public NativeArray<float2> lastCollisionNormal;
    [NativeDisableParallelForRestriction] public NativeArray<bool> collisionStatus;
    [NativeDisableParallelForRestriction] public NativeReference<PhysicsShape> terminusAnchor;
    [NativeDisableParallelForRestriction] public NativeReference<float2> terminusAnchorLocalPos;
    [NativeDisableParallelForRestriction] public NativeReference<FastRope.TerminusAnchorMode> terminusAnchorMode;
    [ReadOnly] public PhysicsWorld world;
    public readonly PhysicsQuery.QueryFilter filter;
    public readonly float collisionBounciness;
    public readonly float nodeSpacing;
    public readonly float nodeRadius;
    public readonly float nodeMass;
    public readonly float ownerMass;
    public readonly float terminusMass;
    public readonly float dynamicAnchorPullForce;
    public readonly int sourceIndex;
    public readonly int groupSize;
    public readonly int offset;

    public RopeGroupedConstraintIteration(NativeArray<float2> position, NativeArray<float2> lastPosition, NativeArray<float2> lastCollisionNormal, NativeArray<bool> collisionStatus, 
        NativeReference<PhysicsShape> terminusAnchor, NativeReference<float2> terminusAnchorLocalPos, NativeReference<FastRope.TerminusAnchorMode> terminusAnchorMode, 
        PhysicsWorld world, PhysicsQuery.QueryFilter filter, float collisionBounciness, float nodeSpacing, 
        float nodeRadius, float nodeMass, float ownerMass, float terminusMass, float dynamicAnchorPullForce, 
        int sourceIndex, int groupSize, int batch)
    {
        this.position = position;
        this.lastPosition = lastPosition;
        this.lastCollisionNormal = lastCollisionNormal;
        this.collisionStatus = collisionStatus;
        this.terminusAnchor = terminusAnchor;
        this.terminusAnchorLocalPos = terminusAnchorLocalPos;
        this.terminusAnchorMode = terminusAnchorMode;
        this.world = world;
        this.filter = filter;
        this.collisionBounciness = collisionBounciness;
        this.nodeSpacing = nodeSpacing;
        this.nodeRadius = nodeRadius;
        this.nodeMass = nodeMass;
        this.ownerMass = ownerMass;
        this.terminusMass = terminusMass;
        this.dynamicAnchorPullForce = dynamicAnchorPullForce;
        this.sourceIndex = sourceIndex;
        this.groupSize = groupSize;
        offset = sourceIndex + 1 + batch;
    }

    public readonly void Execute(int i)
    {
        i = groupSize * i + offset;
        int min = math.max(i - groupSize + 1, sourceIndex); 
        while (i > min)
        {
            RopeJobUtils.CoverAllSpacingConstraint(i, position, lastPosition, lastCollisionNormal, collisionStatus, 
                terminusAnchor, terminusAnchorLocalPos, terminusAnchorMode, world, filter,
                collisionBounciness, nodeSpacing, nodeRadius, nodeMass, ownerMass, terminusMass, dynamicAnchorPullForce,
                sourceIndex);
            i--;
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
public struct CompleteRopeConstraintsWithStaticAnchor : IJob
{
    public NativeArray<float2> position;
    public NativeArray<float2> lastPosition;
    [ReadOnly] public NativeReference<PhysicsShape> terminusAnchor;
    [ReadOnly] public NativeReference<float2> terminusAnchorLocalPos;

    public CompleteRopeConstraintsWithStaticAnchor(NativeArray<float2> position, NativeArray<float2> lastPosition,
        NativeReference<PhysicsShape> terminusAnchor, NativeReference<float2> terminusAnchorLocalPos)
    {
        this.position = position;
        this.lastPosition = lastPosition;
        this.terminusAnchor = terminusAnchor;
        this.terminusAnchorLocalPos = terminusAnchorLocalPos;
    }

    public void Execute()
    {
        if (terminusAnchor.Value.isValid)
        {
            float2 p = terminusAnchor.Value.transform.TransformPoint(terminusAnchorLocalPos.Value);
            position[^1] = p;
            lastPosition[^1] = p;
        }
    }
}

[BurstCompile]
public struct PrepareForRopeConstraintsWithDynamicAnchor : IJob
{
    public NativeArray<float2> position;
    public NativeArray<float2> positionBuffer;
    public NativeArray<float2> lastPosition;
    [ReadOnly] public NativeReference<PhysicsShape> terminusAnchor;
    [ReadOnly] public NativeReference<float2> terminusAnchorLocalPos;
    public readonly float dt;

    public PrepareForRopeConstraintsWithDynamicAnchor(NativeArray<float2> position, NativeArray<float2> positionBuffer, NativeArray<float2> lastPosition, 
        NativeReference<PhysicsShape> terminusAnchor, NativeReference<float2> terminusAnchorLocalPos, float dt)
    {
        this.position = position;
        this.positionBuffer = positionBuffer;
        this.lastPosition = lastPosition;
        this.terminusAnchor = terminusAnchor;
        this.terminusAnchorLocalPos = terminusAnchorLocalPos;
        this.dt = dt;
    }

    public void Execute()
    {
        if (terminusAnchor.Value.isValid)
        {
            float2 p = terminusAnchor.Value.transform.TransformPoint(terminusAnchorLocalPos.Value);
            position[^1] = p;
            positionBuffer[0] = p;
            lastPosition[^1] = p - dt * (float2)terminusAnchor.Value.body.linearVelocity;
        }
    }
}

[BurstCompile]
public struct CompleteRopeConstraintsWithDynamicAnchor : IJob
{
    public NativeArray<float2> position;
    [ReadOnly] public NativeArray<float2> positionBuffer;
    public NativeArray<float2> lastPosition;

    public CompleteRopeConstraintsWithDynamicAnchor(NativeArray<float2> position, NativeArray<float2> positionBuffer, NativeArray<float2> lastPosition)
    {
        this.position = position;
        this.positionBuffer = positionBuffer;
        this.lastPosition = lastPosition;
    }

    public void Execute()
    {
        var p = positionBuffer[0];
        var v = position[^1] - lastPosition[^1];
        position[^1] = p;
        lastPosition[^1] += p - v;
    }
}

[BurstCompile]
public struct RopeReparametrization : IJob
{
    public NativeArray<float2> position;
    public NativeArray<float2> lastPosition;
    public NativeArray<float2> positionBuffer;
    public NativeArray<float2> lastPositionBuffer;
    public NativeArray<float2> lastCollisionNormal;
    public NativeArray<bool> collisionStatus;
    public NativeReference<float> nodeSpacing;//not parallel job; we don't need the [NativeDisableParallelForRestriction]
    public NativeReference<int> sourceIndex;
    public readonly float length;//recompute before job
    public readonly int newSourceIndex;

    public RopeReparametrization(NativeArray<float2> position, NativeArray<float2> lastPosition, NativeArray<float2> positionBuffer, NativeArray<float2> lastPositionBuffer, 
        NativeArray<float2> lastCollisionNormal, NativeArray<bool> collisionStatus, NativeReference<float> nodeSpacing, NativeReference<int> sourceIndex, 
        float length, int newSourceIndex)
    {
        this.position = position;
        this.lastPosition = lastPosition;
        this.positionBuffer = positionBuffer;
        this.lastPositionBuffer = lastPositionBuffer;
        this.lastCollisionNormal = lastCollisionNormal;
        this.collisionStatus = collisionStatus;
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
                RopeJobUtils.Anchor(k, position, lastPosition, collisionStatus, lastCollisionNormal);
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
    public NativeReference<float> maxTension;
    public readonly float nodeSpacing;
    public readonly int sourceIndex;

    public CalculateRopeMaxTension(NativeArray<float2> position, NativeReference<float> maxTension, float nodeSpacing, int sourceIndex)
    {
        this.position = position;
        this.maxTension = maxTension;
        this.nodeSpacing = nodeSpacing;
        this.sourceIndex = sourceIndex;
    }

    public void Execute()
    {
        float max = -math.INFINITY;
        for (int i = sourceIndex + 1;  i < position.Length; i++)
        {
            var t = math.length(position[i] - position[i - 1]) - nodeSpacing;
            if (t > max)
            {
                max = t;
            }
        }

        maxTension.Value = max;
    }
}

[BurstCompile]
public struct CheckForRopeCollisionFailure : IJob
{
    [ReadOnly] public NativeArray<float2> position;
    public NativeReference<PhysicsShape> terminusAnchor;
    [ReadOnly] public PhysicsWorld world;
    public NativeReference<bool> collisionIsFailing;
    public PhysicsQuery.QueryFilter filter;
    public float collisionThreshold;
    public float breakThreshold;
    public int sourceIndex;

    public CheckForRopeCollisionFailure(NativeArray<float2> position, NativeReference<PhysicsShape> terminusAnchor, 
        PhysicsWorld world, NativeReference<bool> collisionIsFailing, PhysicsQuery.QueryFilter filter, 
        float collisionThreshold, float breakThreshold, int sourceIndex)
    {
        this.position = position;
        this.terminusAnchor = terminusAnchor;
        this.world = world;
        this.collisionIsFailing = collisionIsFailing;
        this.filter = filter;
        this.collisionThreshold = collisionThreshold;
        this.breakThreshold = breakThreshold;
        this.sourceIndex = sourceIndex;
    }

    public void Execute()
    {
        float distance = 0;
        bool chaining = false;
        bool dynamicAnchor = terminusAnchor.Value.isValid && terminusAnchor.Value.body.type == PhysicsBody.BodyType.Dynamic;

        collisionIsFailing.Value = false;

        for (int i = sourceIndex + 1; i < position.Length - 1; i++)
        {
            if (chaining)
            {
                distance += math.distance(position[i - 1], position[i]);
                if (distance > breakThreshold)
                {
                    collisionIsFailing.Value = true;
                    return;
                }
            }

            //check if node is fully tunneled inside one collider
            var c = world.OverlapPoint(position[i], filter);
            if (c.Length > 0)
            {
                if (!c[0].shape.isValid || (dynamicAnchor && c[0].shape.body == terminusAnchor.Value.body))
                {
                    //we don't really want rope to break when we're hauling something,
                    //so we'll exclude dynamic anchor from failure checks and allow rope to pass through dynamic anchor when it needs to
                    chaining = false;
                    distance = 0;
                    continue;
                }

                var body0 = c[0].shape.body;

                c = world.OverlapPoint(position[i] + new float2(0, collisionThreshold), filter);
                if (c.Length == 0 || !c[0].shape.isValid || c[0].shape.body != body0)
                {
                    chaining = false;
                    distance = 0;
                    continue;
                }

                c = world.OverlapPoint(position[i] + new float2(0, -collisionThreshold), filter);
                if (c.Length == 0 || !c[0].shape.isValid || c[0].shape.body != body0)
                {
                    chaining = false;
                    distance = 0;
                    continue;
                }

                c = world.OverlapPoint(position[i] + new float2(collisionThreshold, 0), filter);
                if (c.Length == 0 || !c[0].shape.isValid || c[0].shape.body != body0)
                {
                    chaining = false;
                    distance = 0;
                    continue;
                }

                c = world.OverlapPoint(position[i] + new float2(-collisionThreshold, 0), filter);
                if (c.Length == 0 || !c[0].shape.isValid || c[0].shape.body != body0)
                {
                    chaining = false;
                    distance = 0;
                    continue;
                }

                if (!chaining)
                {
                    chaining = true;
                    distance = math.distance(position[i - 1], position[i]);
                    if (distance > breakThreshold)
                    {
                        collisionIsFailing.Value = true;
                        break;
                    }
                }
            }
        }
    }
}