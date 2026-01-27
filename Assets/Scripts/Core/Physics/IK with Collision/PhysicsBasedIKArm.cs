using System;
using UnityEngine;

public class PhysicsBasedIKArm : MonoBehaviour
{
    [SerializeField] Transform[] chain;//transforms not nested
    [SerializeField] float[] armHalfWidth;
    [SerializeField] LayerMask collisionMask;
    [SerializeField] float collisionResponse;
    //[SerializeField] float collisionDamping;
    [SerializeField] float horizontalRaycastSpacing;
    [SerializeField] float tunnelInterval;
    [SerializeField] float tunnelMax;
    [SerializeField] float effectorSpringConstant;
    [SerializeField] float maxTargetPursuitSpeed;
    [SerializeField] float jointDamping;
    [SerializeField] float targetTolerance;
    [SerializeField] float targetingErrorMin;//keeps arm from slowing too much as it gets close to target
    [SerializeField] float configurationSpringConstant;
    [SerializeField] float maxConfigurationPursuitSpeed;
    [SerializeField] float configurationTolerance;

    float[] length;
    float[] angularVelocity;
    float totalLength;

    Vector2 targetPosition;
    Transform targetTransform;
    Collider2D targetCollider;
    Vector2[] targetConfiguration;//in local frame (but relative to anchor position)
    Vector2[] foldedConfiguration;
    Vector2[] foldedConfiguration1;
    Mode mode;

    Vector2 lastTargetPositionUsed;
    bool hasInvokedTargetReached;

    enum Mode
    {
        off, idle, trackPosition, trackTransform, trackCollider, trackConfiguration
    }

    public Transform Anchor => chain[0];
    public Transform Effector => chain[^1];
    public float[] Length => length;
    public float TotalLength => totalLength;
    public bool IsOff => mode == Mode.off;

    public event Action TargetReached;

    public void Initialize()
    {
        totalLength = 0f;
        length = new float[chain.Length - 1];
        for (int i = 0; i < length.Length; i++)
        {
            var d = Vector2.Distance(chain[i].position, chain[i + 1].position);
            length[i] = d;
            totalLength += d;
        }

        angularVelocity = new float[chain.Length - 1];

        targetConfiguration = new Vector2[chain.Length - 1];
        foldedConfiguration = new Vector2[chain.Length - 1];
        foldedConfiguration1 = new Vector2[chain.Length - 1];

        var sign = 1;
        for (int i = 0; i < foldedConfiguration.Length; i++)
        {
            foldedConfiguration[i] = new Vector2(sign, 0);
            sign = -sign;
        }

        sign = 1;
        foldedConfiguration1[0] = Vector2.right;
        for (int i = 1; i < foldedConfiguration1.Length; i++)
        {
            foldedConfiguration1[i] = new Vector2(sign, 0);
            sign = -sign;
        }
    }

    public void BeginTargetingPosition(Vector2 position)
    {
        targetPosition = position;
        mode = Mode.trackPosition;
        hasInvokedTargetReached = false;
        Debug.Log($"begin targeting position {position}");
    }

    public void BeginTargetingTransform(Transform target)
    {
        targetTransform = target;
        mode = Mode.trackTransform;
        hasInvokedTargetReached = false;
        Debug.Log($"begin targeting transform {target.name}");
    }

    public void BeginTargetingCollider(Collider2D collider)
    {
        targetCollider = collider;
        mode = Mode.trackCollider;
        hasInvokedTargetReached = false;
        Debug.Log($"begin targeting collider {collider.name}");
    }

    public void BeginTargetingConfiguration(Vector2[] configuration)
    {
        //Array.Copy(configuration, targetConfiguration, configuration.Length);
        targetConfiguration = configuration;
        mode = Mode.trackConfiguration;
        hasInvokedTargetReached = false;
    }

    public void FoldUp()
    {
        BeginTargetingConfiguration(foldedConfiguration);
    }

    public void FoldUp1()
    {
        BeginTargetingConfiguration(foldedConfiguration1);
    }

    public void SnapToConfiguration(Vector2[] configuration)
    {
        for (int i = 0; i < configuration.Length; i++)
        {
            var u = MathTools.QuaternionFrom2DUnitVector(configuration[i]);
            chain[i].rotation = u;
            chain[i + 1].position = chain[i].position + length[i] * (Vector3)configuration[i];
            //var p = Anchor.position + configuration[i].x * transform.right + configuration[i].y * transform.up;
            //chain[i].position = p;
            //Vector2 u = (chain[i].position - chain[i - 1].position) / length[i - 1];
            //chain[i - 1].rotation = MathTools.QuaternionFrom2DUnitVector(u);
        }
    }

    public void SnapToFolded()
    {
        SnapToConfiguration(foldedConfiguration);
    }

    public void SnapToFolded1()
    {
        SnapToConfiguration(foldedConfiguration1);
    }

    public void GoIdle()
    {
        mode = Mode.idle;
        hasInvokedTargetReached = false;//reset this on any mode change
        Debug.Log($"ik arm going idle");
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
            case Mode.trackConfiguration:
                TrackConfigurationBehavior();
                break;
                //Mode.off => do nothing
        }
    }

    private void RunSimulationStep()
    {
        PhysicsBasedIK.IntegrateJoints(chain, length, angularVelocity, jointDamping, Time.deltaTime);
        PhysicsBasedIK.ApplyCollisionForces(chain, length, armHalfWidth, angularVelocity, collisionMask, collisionResponse * Time.deltaTime,
            horizontalRaycastSpacing, tunnelInterval, tunnelMax/*, collisionDamping*/);
    }

    private void TrackPositionBehavior()
    {
        RunSimulationStep();
        if (targetPosition != lastTargetPositionUsed)
        {
            hasInvokedTargetReached = false;
            lastTargetPositionUsed = targetPosition;
        }
        PullTowardsTarget(targetPosition, Time.deltaTime);
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
            PullTowardsTarget(targetTransform.position, Time.deltaTime);
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
            PullTowardsTarget(target, Time.deltaTime);
        }
    }

    private void TrackConfigurationBehavior()
    {
        RunSimulationStep();
        PullTowardsConfiguration(targetConfiguration, Time.deltaTime);
    }

    private void PullTowardsTarget(Vector2 targetPosition, float dt)
    {
        var error = targetPosition - (Vector2)chain[^1].position;
        var err2 = error.sqrMagnitude;
        if (err2 < targetTolerance * targetTolerance)
        {
            if (!hasInvokedTargetReached)
            {
                Debug.Log("arm reached target");
                hasInvokedTargetReached = true;
                TargetReached?.Invoke();
                //VERY IMPORTANT to set hasInvokedTargetReached = true BEFORE invoking the event!
                //the event may have a callback that begins a new targeting action, and we don't want to set hasInvokedTargetReached = true immediately after that
            }
            return;
        }

        var err = Mathf.Sqrt(err2);
        var clampedErr = Mathf.Max(err, targetingErrorMin);//this keeps it from slowing down too much as it gets close to the target
        var a = effectorSpringConstant * dt * Mathf.Min(clampedErr, maxTargetPursuitSpeed) / err * error;
        PhysicsBasedIK.ApplyForceToJoint(chain, length, angularVelocity, a, chain.Length - 1);
    }

    private void PullTowardsConfiguration(Vector2[] targetConfiguration, float dt)
    {
        bool anyChanged = false;
        for (int i = 0; i < targetConfiguration.Length; i++)
        {
            Vector2 u = (chain[i + 1].position - chain[i].position) / length[i];
            var angle = MathTools.PseudoAngle(u, targetConfiguration[i]);
            if (Mathf.Abs(angle) < configurationTolerance)
            {
                continue;
            }

            anyChanged = true;
            var a = configurationSpringConstant * angle * dt;
            angularVelocity[i] += Mathf.Sign(a) * Mathf.Min(Mathf.Abs(a), maxConfigurationPursuitSpeed);
            //break here would be cool? i.e. we just fold one joint at a time
            //or we can also do them backwards
        }

        if (!anyChanged && !hasInvokedTargetReached)
        {
            Debug.Log("arm reached target configuration");
            hasInvokedTargetReached = true;
            TargetReached?.Invoke();
        }
    }
}