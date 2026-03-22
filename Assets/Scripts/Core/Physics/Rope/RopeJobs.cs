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
    public float2 gravity;
    public readonly float drag;
    public readonly float dt2;
    public readonly float timeScale;
    public readonly int offset;

    public IntegrateRope(NativeArray<float2> position, NativeArray<float2> lastPosition, float2 gravity, 
        float drag, float dt2, float timeScale, int startIndex)
    {
        this.position = position;
        this.lastPosition = lastPosition;
        this.gravity = gravity;
        this.drag = drag;
        this.dt2 = dt2;
        this.timeScale = timeScale;
        offset = startIndex + 1;
    }

    public void Execute(int i)
    {
        i += offset;
        var p = position[i];
        var d = p - lastPosition[i];
        position[i] += (timeScale - drag * math.length(d)) * d + dt2 * gravity;
        lastPosition[i] = p;
    }
}

[BurstCompile]
public struct ResolveRopeCollision : IJobParallelFor
{
    [NativeDisableParallelForRestriction] public NativeArray<float2> position;
    [NativeDisableParallelForRestriction] public NativeArray<float2> lastPosition;
    [NativeDisableParallelForRestriction] public NativeArray<float2> lastCollisionNormal;
    [NativeDisableParallelForRestriction] public NativeArray<bool> nearCollision;
    [NativeDisableParallelForRestriction] public NativeArray<bool> hadCollision;
    [NativeDisableParallelForRestriction][ReadOnly] public NativeArray<float2> raycastDirections;
    [NativeDisableParallelForRestriction] public NativeReference<PhysicsShape> terminusAnchor;
    [NativeDisableParallelForRestriction] public NativeReference<float2> terminusAnchorLocalPos;
    [NativeDisableParallelForRestriction] public NativeReference<FastRope.TerminusAnchorMode> terminusAnchorMode;
    [ReadOnly] public PhysicsWorld world;
    public readonly PhysicsQuery.QueryFilter filter;
    public readonly float collisionSearchRadius;
    public readonly float collisionThreshold;
    public readonly float tunnelEscapeRadius;
    public readonly float collisionBounciness;
    public readonly int offset;

    public ResolveRopeCollision(NativeArray<float2> position, NativeArray<float2> lastPosition, NativeArray<float2> lastCollisionNormal, 
        NativeArray<bool> nearCollision, NativeArray<bool> hadCollision, NativeArray<float2> raycastDirections, NativeReference<PhysicsShape> terminusAnchor, 
        NativeReference<float2> terminusAnchorLocalPos, NativeReference<FastRope.TerminusAnchorMode> terminusAnchorMode, PhysicsWorld world, PhysicsQuery.QueryFilter filter, 
        float collisionSearchRadius, float collisionThreshold, float tunnelEscapeRadius, float collisionBounciness, int startIndex)
    {
        this.position = position;
        this.lastPosition = lastPosition;
        this.lastCollisionNormal = lastCollisionNormal;
        this.nearCollision = nearCollision;
        this.hadCollision = hadCollision;
        this.raycastDirections = raycastDirections;
        this.terminusAnchor = terminusAnchor;
        this.terminusAnchorLocalPos = terminusAnchorLocalPos;
        this.terminusAnchorMode = terminusAnchorMode;
        this.world = world;
        this.filter = filter;
        this.collisionSearchRadius = collisionSearchRadius;
        this.collisionThreshold = collisionThreshold;
        this.tunnelEscapeRadius = tunnelEscapeRadius;
        this.collisionBounciness = collisionBounciness;
        offset = startIndex + 1;
    }

    //readonly for method means cpu doesn't have to create defensive copies of the struct data
    public readonly void Execute(int i)
    {
        i += offset;
        if (i == position.Length - 1)
        {
            RopeJobUtils.ResolveTerminusCollision(i, position, lastPosition, lastCollisionNormal, nearCollision, hadCollision, raycastDirections,
                terminusAnchor, terminusAnchorLocalPos, terminusAnchorMode,
                world, filter, collisionSearchRadius, collisionThreshold, tunnelEscapeRadius, collisionBounciness);
        }
        else
        {
            RopeJobUtils.ResolveCollision(i, position, lastPosition, lastCollisionNormal, nearCollision, hadCollision, raycastDirections,
                world, filter, collisionSearchRadius, collisionThreshold, tunnelEscapeRadius, collisionBounciness);
        }
    }
}

[BurstCompile]
public struct RopeConstraintIteration : IJobParallelFor
{
    [NativeDisableParallelForRestriction] public NativeArray<float2> position;
    [NativeDisableParallelForRestriction] public NativeArray<float2> lastPosition;
    [NativeDisableParallelForRestriction] public NativeArray<float2> lastCollisionNormal;
    [NativeDisableParallelForRestriction] public NativeArray<bool> nearCollision;
    [NativeDisableParallelForRestriction] public NativeArray<bool> hadCollision;
    [NativeDisableParallelForRestriction][ReadOnly] public NativeArray<float2> raycastDirections;
    [NativeDisableParallelForRestriction] public NativeReference<PhysicsShape> terminusAnchor;
    [NativeDisableParallelForRestriction] public NativeReference<float2> terminusAnchorLocalPos;
    [NativeDisableParallelForRestriction] public NativeReference<FastRope.TerminusAnchorMode> terminusAnchorMode;
    [ReadOnly] public PhysicsWorld world;
    public readonly PhysicsQuery.QueryFilter filter;
    public readonly float collisionSearchRadius;
    public readonly float collisionThreshold;
    public readonly float tunnelEscapeRadius;
    public readonly float collisionBounciness;
    public readonly float nodeSpacing;
    public readonly float nodeMass;
    public readonly float ownerMass;
    public readonly float terminusMass;
    public readonly float dynamicAnchorPullForce;
    public readonly int startIndex;
    public readonly int offset;

    public RopeConstraintIteration(NativeArray<float2> position, NativeArray<float2> lastPosition, NativeArray<float2> lastCollisionNormal, 
        NativeArray<bool> nearCollision, NativeArray<bool> hadCollision, NativeArray<float2> raycastDirections, NativeReference<PhysicsShape> terminusAnchor, 
        NativeReference<float2> terminusAnchorLocalPos, NativeReference<FastRope.TerminusAnchorMode> terminusAnchorMode, 
        PhysicsWorld world, PhysicsQuery.QueryFilter filter, float collisionSearchRadius, float collisionThreshold, float tunnelEscapeRadius, 
        float collisionBounciness, float nodeSpacing, float nodeMass, float ownerMass, float terminusMass, float dynamicAnchorPullForce, int startIndex, int batch)
    {
        this.position = position;
        this.lastPosition = lastPosition;
        this.lastCollisionNormal = lastCollisionNormal;
        this.nearCollision = nearCollision;
        this.hadCollision = hadCollision;
        this.raycastDirections = raycastDirections;
        this.terminusAnchor = terminusAnchor;
        this.terminusAnchorLocalPos = terminusAnchorLocalPos;
        this.terminusAnchorMode = terminusAnchorMode;
        this.world = world;
        this.filter = filter;
        this.collisionSearchRadius = collisionSearchRadius;
        this.collisionThreshold = collisionThreshold;
        this.tunnelEscapeRadius = tunnelEscapeRadius;
        this.collisionBounciness = collisionBounciness;
        this.nodeSpacing = nodeSpacing;
        this.nodeMass = nodeMass;
        this.ownerMass = ownerMass;
        this.terminusMass = terminusMass;
        this.dynamicAnchorPullForce = dynamicAnchorPullForce;
        this.startIndex = startIndex;
        offset = startIndex + 1 + batch;
    }

    public readonly void Execute(int i)
    {
        i = 2 * i + offset;
        RopeJobUtils.CoverAllSpacingConstraint(i, position, lastPosition, lastCollisionNormal, nearCollision, hadCollision, raycastDirections,
                terminusAnchor, terminusAnchorLocalPos, terminusAnchorMode,
                world, filter, collisionSearchRadius, collisionThreshold, tunnelEscapeRadius,
                collisionBounciness, nodeSpacing, nodeMass, ownerMass, terminusMass, dynamicAnchorPullForce, startIndex);
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
    public NativeArray<bool> nearCollision;
    public NativeArray<bool> hadCollision;
    public NativeReference<float> nodeSpacing;//not parallel job; we don't need the [NativeDisableParallelForRestriction]
    public NativeReference<int> startIndex;
    public readonly float length;//recompute before job
    public readonly int newStartIndex;

    public RopeReparametrization(NativeArray<float2> position, NativeArray<float2> lastPosition, NativeArray<float2> positionBuffer, NativeArray<float2> lastPositionBuffer, 
        NativeArray<float2> lastCollisionNormal, NativeArray<bool> nearCollision, NativeArray<bool> hadCollision, 
        NativeReference<float> nodeSpacing, NativeReference<int> startIndex, float length, int newStartIndex)
    {
        this.position = position;
        this.lastPosition = lastPosition;
        this.positionBuffer = positionBuffer;
        this.lastPositionBuffer = lastPositionBuffer;
        this.lastCollisionNormal = lastCollisionNormal;
        this.nearCollision = nearCollision;
        this.hadCollision = hadCollision;
        this.nodeSpacing = nodeSpacing;
        this.startIndex = startIndex;
        this.length = length;
        this.newStartIndex = newStartIndex;
    }

    public void Execute()
    {
        int terminusIndex = position.Length - 1;
        float newNodeSpacing = length / (terminusIndex - newStartIndex);
        int i = startIndex.Value;//start index of current segment we're copying from
        int j = newStartIndex + 1;//index in rescaleBuffer that we're copying to
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

        if (newStartIndex > startIndex.Value)
        {
            for (int k = startIndex.Value + 1; k < newStartIndex + 1; k++)
            {
                position[k] = position[startIndex.Value];
                RopeJobUtils.Anchor(k, position, lastPosition, nearCollision, hadCollision, lastCollisionNormal);
            }
        }

        startIndex.Value = newStartIndex;
        nodeSpacing.Value = newNodeSpacing;
        int copyStart = newStartIndex + 1;
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
    public readonly int start;

    public CalculateRopeMaxTension(NativeArray<float2> position, NativeReference<float> maxTension, float nodeSpacing, int start)
    {
        this.position = position;
        this.maxTension = maxTension;
        this.nodeSpacing = nodeSpacing;
        this.start = start;
    }

    public void Execute()
    {
        float max = -math.INFINITY;
        for (int i = start + 1;  i < position.Length; i++)
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
public struct CalculateRopeCarryForceDirection : IJob
{
    [ReadOnly] public NativeArray<float2> position;
    [ReadOnly] public NativeArray<bool> nearCollision;
    public NativeReference<float2> carryForceDirection;
    public int start;

    public CalculateRopeCarryForceDirection(NativeArray<float2> position, NativeArray<bool> nearCollision, 
        NativeReference<float2> carryForceDirection, int start)
    {
        this.position = position;
        this.nearCollision = nearCollision;
        this.carryForceDirection = carryForceDirection;
        this.start = start;
    }

    public void Execute()
    {
        int firstCollisionIndex = start + 1;
        while (firstCollisionIndex < position.Length - 1 && !nearCollision[firstCollisionIndex])
        {
            firstCollisionIndex++;
        }

        carryForceDirection.Value = math.normalize(position[firstCollisionIndex] - position[0]);
    }
}

[BurstCompile]
public struct CalculateRopeCarryForceMagnitude : IJob
{
    [ReadOnly] public NativeArray<float2> position;
    [ReadOnly] public NativeArray<bool> nearCollision;
    public NativeReference<float> carryForceMagnitude;
    public float nodeSpacing;
    public float slackThreshold;
    public float calculationInterval;
    public int start;

    public CalculateRopeCarryForceMagnitude(NativeArray<float2> position, NativeArray<bool> nearCollision, 
        NativeReference<float> carryForceMagnitude, float nodeSpacing, float slackThreshold, float calculationInterval,
        int start)
    {
        this.position = position;
        this.nearCollision = nearCollision;
        this.carryForceMagnitude = carryForceMagnitude;
        this.nodeSpacing = nodeSpacing;
        this.slackThreshold = slackThreshold;
        this.calculationInterval = calculationInterval;
        this.start = start;
    }

    public void Execute()
    {
        int nodesPerSeg = (int)math.ceil(calculationInterval / nodeSpacing);
        int terminusIndex = position.Length - 1;
        float total = 0;
        int i = start;
        int j = start;
        var d = nodesPerSeg * nodeSpacing;
        var length = 0f;
        while (i < terminusIndex)
        {
            j += nodesPerSeg;
            if (j > terminusIndex)
            {
                j = terminusIndex;
                d = (j - i) * nodeSpacing;
            }

            //can't get reliable tension around corners -- works better if you just ignore them
            if (nearCollision[i] && nearCollision[j])
            {
                i = j;
                continue;
            }

            var err = math.length(position[j] - position[i]) - d;

            if (err > -slackThreshold * d)
            {
                total += err;
                length += d;
            }
            else
            {
                carryForceMagnitude.Value = 0;
                return;
            }

            i = j;
        }

        carryForceMagnitude.Value = total > 0 ? total / length: 0;
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
    public int start;

    public CheckForRopeCollisionFailure(NativeArray<float2> position, NativeReference<PhysicsShape> terminusAnchor, 
        PhysicsWorld world, NativeReference<bool> collisionIsFailing, PhysicsQuery.QueryFilter filter, 
        float collisionThreshold, float breakThreshold, int start)
    {
        this.position = position;
        this.terminusAnchor = terminusAnchor;
        this.world = world;
        this.collisionIsFailing = collisionIsFailing;
        this.filter = filter;
        this.collisionThreshold = collisionThreshold;
        this.breakThreshold = breakThreshold;
        this.start = start;
    }

    public void Execute()
    {
        float distance = 0;
        bool chaining = false;
        bool dynamicAnchor = terminusAnchor.Value.isValid && terminusAnchor.Value.body.type == PhysicsBody.BodyType.Dynamic;

        collisionIsFailing.Value = false;

        for (int i = start + 1; i < position.Length - 1; i++)
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