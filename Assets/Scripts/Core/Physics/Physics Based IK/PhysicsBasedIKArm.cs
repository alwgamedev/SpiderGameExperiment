using System;
using UnityEngine;

public class PhysicsBasedIKArm : MonoBehaviour
{
    [SerializeField] Transform orientingTransform;//if arm is not childed to anything, just use self transform here
    [SerializeField] Transform[] chain;//transforms assumed to be nested
    [SerializeField] float[] armHalfWidth;
    [SerializeField] LayerMask collisionMask;
    [SerializeField] float collisionResponse;
    [SerializeField] float horizontalRaycastSpacing;
    [SerializeField] float tunnelInterval;
    [SerializeField] float tunnelMax;
    [SerializeField] float effectorSpringConstant;
    [SerializeField] float maxTargetPursuitSpeed;
    [SerializeField] float jointDamping;
    [SerializeField] float targetTolerance;
    [SerializeField] float targetingErrorMin;//keeps arm from slowing too much as it gets close to target
    [SerializeField] float poseSpringConstant;
    [SerializeField] float maxPosePursuitSpeed;
    [SerializeField] float poseTolerance;
    public float gravityScale;

    float[] length;
    float[] inverseLength;
    float[] angularVelocity;

    Vector2 targetPosition;
    Transform targetTransform;
    Collider2D targetCollider;
    Vector2[] targetPose;//unit vectors for arm directions in local frame
    float[] poseWeight;
    Mode mode;

    Vector2 lastTargetPositionUsed;
    bool hasInvokedTargetReached;

    enum Mode
    {
        off, idle, trackPosition, trackTransform, trackCollider, trackPose
    }

    public Transform Anchor => chain[0];
    public Transform Effector => chain[^1];
    public bool IsOff => mode == Mode.off;

    public event Action TargetReached;

    public void Initialize()
    {
        length = new float[chain.Length - 1];
        inverseLength = new float[chain.Length - 1];
        for (int i = 0; i < length.Length; i++)
        {
            var d = Vector2.Distance(chain[i].position, chain[i + 1].position);
            length[i] = d;
            inverseLength[i] = 1 / d;
        }

        angularVelocity = new float[chain.Length - 1];
    }

    public void BeginTargetingPosition(Vector2 position, float[] poseWeight = null, Vector2[] pose = null)
    {
        targetPosition = position;
        this.poseWeight = poseWeight;
        targetPose = pose;
        mode = Mode.trackPosition;
        hasInvokedTargetReached = false;
    }

    public void BeginTargetingTransform(Transform target, float[] poseWeight = null, Vector2[] pose = null)
    {
        targetTransform = target; 
        this.poseWeight = poseWeight;
        targetPose = pose;
        mode = Mode.trackTransform;
        hasInvokedTargetReached = false;
    }

    public void BeginTargetingCollider(Collider2D collider, float[] poseWeight = null, Vector2[] pose = null)
    {
        targetCollider = collider;
        this.poseWeight = poseWeight;
        targetPose = pose;
        mode = Mode.trackCollider;
        hasInvokedTargetReached = false;
    }

    public void BeginTargetingPose(Vector2[] pose)
    {
        targetPose = pose;
        mode = Mode.trackPose;
        hasInvokedTargetReached = false;
    }

    public void SnapToPose(Vector2[] pose)
    {
        for (int i = 0; i < pose.Length; i++)
        {
            var v = pose[i].x * orientingTransform.right + pose[i].y * orientingTransform.up;
            var q = MathTools.QuaternionFrom2DUnitVector(v);
            chain[i].rotation = q;
        }
    }

    public void GoIdle()
    {
        mode = Mode.idle;
        hasInvokedTargetReached = false;//reset this on any mode change
    }

    public void TurnOff()
    {
        mode = Mode.off;
        hasInvokedTargetReached = false;
        targetTransform = null;
        targetCollider = null;
        for (int i = 0; i < angularVelocity.Length; i++)
        {
            angularVelocity[i] = 0f;
        }
    }

    public void CapturePose(Vector2[] pose)
    {
        Array.Resize(ref pose, chain.Length - 1);
        var u = Mathf.Sign(orientingTransform.localScale.x) * orientingTransform.right;
        var v = orientingTransform.up;
        for (int i = 0; i < pose.Length; i++)
        {
            var w = (chain[i + 1].position - chain[i].position).normalized;
            pose[i] = new(Vector2.Dot(w, u), Vector2.Dot(w, v));
        }
    }

    private void FixedUpdate()
    {
        switch (mode)
        {
            case Mode.idle:
                RunSimulationStep();
                break;
            case Mode.trackPosition:
                TrackPositionBehavior();
                break;
            case Mode.trackTransform:
                TrackTransformBehavior();
                break;
            case Mode.trackCollider:
                TrackColliderBehavior();
                break;
            case Mode.trackPose:
                TrackPoseBehavior();
                break;
            //Mode.off => do nothing
        }
    }

    private void RunSimulationStep()
    {
        PhysicsBasedIK.IntegrateJoints(chain, angularVelocity, jointDamping, Time.deltaTime);
        if (collisionResponse > 0)
        {
            PhysicsBasedIK.ApplyCollisionForces(chain, length, inverseLength, armHalfWidth, angularVelocity, collisionMask, collisionResponse * Time.deltaTime,
            horizontalRaycastSpacing, tunnelInterval, tunnelMax);
        }
        if (gravityScale != 0)
        {
            PhysicsBasedIK.ApplyGravity(chain, inverseLength, angularVelocity, gravityScale, Time.deltaTime);
        }
    }

    private void TrackPositionBehavior()
    {
        RunSimulationStep();
        if (targetPosition != lastTargetPositionUsed)
        {
            hasInvokedTargetReached = false;
            lastTargetPositionUsed = targetPosition;
        }
        PullTowardsTarget(targetTransform.position, poseWeight, targetPose, Time.deltaTime);
    }

    private void TrackTransformBehavior()
    {
        RunSimulationStep();
        if (targetTransform)
        {
            Vector2 target = targetTransform.position;
            if (target != lastTargetPositionUsed)
            {
                hasInvokedTargetReached = false;
                lastTargetPositionUsed = target;
            }
            PullTowardsTarget(target, poseWeight, targetPose, Time.deltaTime);
        }
        else if (targetTransform != null)
        {
            targetTransform = null;
            if (!hasInvokedTargetReached)
            {
                hasInvokedTargetReached = true;
                TargetReached?.Invoke();
            }
        }
    }

    private void TrackColliderBehavior()
    {
        RunSimulationStep();
        if (targetCollider)
        {
            Vector2 target = targetCollider.ClosestPoint(Effector.position);
            if (target != lastTargetPositionUsed)
            {
                hasInvokedTargetReached = false;
                lastTargetPositionUsed = target;
            }
            PullTowardsTarget(target, poseWeight, targetPose, Time.deltaTime);
        }
        else if (targetCollider != null)
        {
            targetCollider = null;
            if (!hasInvokedTargetReached)
            {
                hasInvokedTargetReached = true;
                TargetReached?.Invoke();
            }
        }
    }

    private void TrackPoseBehavior()
    {
        RunSimulationStep();
        PullTowardsPose(targetPose, Time.deltaTime);
    }

    private void PullTowardsPose(Vector2[] pose, float dt)
    {
        bool reachedPose = true;
        for (int i = 0; i < pose.Length; i++)
        {
            Vector2 u = inverseLength[i] * (chain[i + 1].position - chain[i].position);
            Vector2 v = PoseToWorldDirection(pose, i);
            var angle = MathTools.PseudoAngle(u, v);
            if (Mathf.Abs(angle) < poseTolerance)
            {
                continue;
            }

            reachedPose = false;
            var a = poseSpringConstant * angle * dt;
            angularVelocity[i] += Mathf.Sign(a) * Mathf.Min(Mathf.Abs(a), maxPosePursuitSpeed);
        }

        if (reachedPose && !hasInvokedTargetReached)
        {
            hasInvokedTargetReached = true;
            TargetReached?.Invoke();
        }
    }

    private void PullTowardsTarget(Vector2 targetPosition, float[] poseWeight, Vector2[] pose, float dt)
    {
        if (poseWeight != null && pose != null)
        {
            for (int i = 0; i < pose.Length; i++)
            {
                if (poseWeight[i] == 0)
                {
                    continue;
                }
                Vector2 u = inverseLength[i] * (chain[i + 1].position - chain[i].position);
                Vector2 v = PoseToWorldDirection(pose, i);
                var angle = MathTools.PseudoAngle(u, v);
                if (Mathf.Abs(angle) < poseTolerance)
                {
                    continue;
                }

                var a = poseWeight[i] * poseSpringConstant * angle * dt;
                angularVelocity[i] += Mathf.Sign(a) * Mathf.Min(Mathf.Abs(a), maxPosePursuitSpeed);
            }
        }

        var error = targetPosition - (Vector2)chain[^1].position;
        var err = error.sqrMagnitude;
        if (err < targetTolerance * targetTolerance)
        {
            if (!hasInvokedTargetReached)
            {
                hasInvokedTargetReached = true;
                TargetReached?.Invoke();
                //VERY IMPORTANT to set hasInvokedTargetReached = true BEFORE invoking the event!
                //the event may have a callback that begins a new targeting action, and we don't want to set hasInvokedTargetReached = true immediately after that
            }
            return;
        }

        err = Mathf.Sqrt(err);
        var clampedErr = Mathf.Max(err, targetingErrorMin);//this keeps it from slowing down too much as it gets close to the target
        var accel = effectorSpringConstant * dt * Mathf.Min(clampedErr, maxTargetPursuitSpeed) / err * error;
        PhysicsBasedIK.ApplyForceToJoint(chain, inverseLength, angularVelocity, accel, chain.Length - 1, poseWeight);
    }

    private Vector2 PoseToWorldDirection(Vector2[] pose, int i)
    {
        return pose[i].x * Mathf.Sign(orientingTransform.localScale.x) * orientingTransform.right + pose[i].y * orientingTransform.up;
    }

    private Vector2 WorldToPoseDirection(Vector2 w)
    {
        return new(Mathf.Sign(orientingTransform.localScale.x) * Vector2.Dot(w, orientingTransform.right), Vector2.Dot(w, orientingTransform.up));
    }
}