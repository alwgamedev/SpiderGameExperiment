using System;
using System.Collections.Generic;
using UnityEngine;

public class IKExtendableArm : MonoBehaviour
{
    [SerializeField] Transform[] chain;
    [SerializeField] float[] armHalfWidth;
    [SerializeField] LayerMask collisionMask;
    [SerializeField] float collisionBuffer;
    [SerializeField] int ikIterations;
    [SerializeField] float ikTolerance;
    [SerializeField] float raycastLength;
    [SerializeField] float horizontalRaycastSpacing;
    [SerializeField] float failedRaycastShift;
    [SerializeField] int failedRaycastNumShifts;
    [SerializeField] int fwIterations;
    [SerializeField] float maxChange;
    [SerializeField] float jointRotationSpeed;

    Vector2[] position;
    Vector2[] ray;
    Vector2[] boundary;
    float[] deltaAngle;
    float[] gradient;
    float[] length;
    float totalLength;

    HashSet<int> verticesVisited = new();

    Vector2 targetPosition;
    Transform targetTransform;
    Mode mode;

    Vector2 lastTargetPositionUsed;
    bool hasInvokedTargetReached;

    enum Mode
    {
        idle, trackPosition, trackTransform
    }

    public Transform[] Chain => chain;
    public float TotalLength => totalLength;

    public event Action TargetReached;

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

    public void GoIdle()
    {
        mode = Mode.idle;
        hasInvokedTargetReached = false;//reset this on any mode change
        Debug.Log($"ik arm going idle");
    }

    //private void OnDrawGizmos()
    //{
    //    if (Application.isPlaying)
    //    {
    //        Gizmos.color = Color.green;
    //        Gizmos.DrawSphere(targetPosition, 1f);
    //    }
    //}

    private void Start()
    {
        totalLength = 0;
        length = new float[chain.Length - 1];
        for (int i = 0; i < length.Length; i++)
        {
            length[i] = Vector2.Distance(chain[i].position, chain[i + 1].position);
            totalLength += length[i];
        }

        position = new Vector2[chain.Length];
        ray = new Vector2[chain.Length - 1];
        boundary = new Vector2[chain.Length - 1];
        deltaAngle = new float[chain.Length - 1];
        gradient = new float[chain.Length - 1];
    }

    //private void Update()
    //{
    //    if (Mouse.current.leftButton.isPressed)
    //    {
    //        var p = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
    //        targetPosition = p;
    //        if (!targetPositionInitialized)
    //        {
    //            targetPositionInitialized = true;
    //        }
    //    }
    //}

    private void FixedUpdate()
    {
        if (mode != Mode.idle)
        {
            Vector2 target;
            if (mode == Mode.trackPosition)
            {
                target = targetPosition;
            }
            else
            {
                if (!targetTransform)
                {
                    Debug.Log("target transform is null");
                    return;
                }
                target = targetTransform.position;
            }
            if (target != lastTargetPositionUsed)
            {
                hasInvokedTargetReached = false;
                lastTargetPositionUsed = target;
            }
            MoveTowardsTarget(target);
        }
    }

    private void MoveTowardsTarget(Vector2 target)
    {
        position[0] = chain[0].position;
        for (int j = 1; j < chain.Length; j++)
        {
            position[j] = chain[j].position;
            ray[j - 1] = position[j] - position[j - 1];
        }

        float maxChange = this.maxChange * Time.deltaTime;
        int iterations = 0;
        while (iterations < ikIterations)
        {
            if (BetterIK.RunIterationWithCollisionAvoidance(position, ray, length, armHalfWidth, 
                target, ikTolerance, totalLength,
                collisionMask, horizontalRaycastSpacing, failedRaycastShift, failedRaycastNumShifts, raycastLength,
                boundary, gradient, deltaAngle, maxChange,
                fwIterations, verticesVisited))
            {
                iterations++;
            }
            else
            {
                break;
            }
        }

        if (iterations > 0)
        {
            for (int i = 1; i < chain.Length; i++)
            {
                var u = (position[i] - position[i - 1]) / length[i - 1];
                chain[i - 1].ApplyCheapRotationBySpeedClamped(u, jointRotationSpeed, Time.deltaTime, out _);
                chain[i].position = chain[i - 1].position + length[i - 1] * chain[i - 1].right;//so we can keep arm transforms unnested
            }
        }

        if (!hasInvokedTargetReached && ((Vector2)chain[^1].position - target).sqrMagnitude < ikTolerance * ikTolerance)
        {
            Debug.Log("target reached");
            TargetReached?.Invoke();
            hasInvokedTargetReached = true;
        }
    }
}