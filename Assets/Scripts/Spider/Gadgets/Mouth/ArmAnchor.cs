using System;
using UnityEngine;

public class ArmAnchor : MonoBehaviour
{
    [SerializeField] Transform[] chain;//transforms assumed to be nested
    [SerializeField] float moveSpeed;
    [SerializeField] float targetTolerance;

    Transform targetTransform;

    public event Action TargetReached;

    private void FixedUpdate()
    {
        if (targetTransform)
        {
            MoveTowardsTarget(Time.deltaTime);
        }
    }

    public void SetPosition(Vector2 position)
    {
        chain[0].position = position;
    }

    public void BeginTargetingTransform(Transform target)
    {
        targetTransform = target;
    }

    private void MoveTowardsTarget(float dt)
    {
        var v = targetTransform.position - chain[0].position;
        var d = Vector2.SqrMagnitude(v);
        if (d < targetTolerance * targetTolerance)
        {
            targetTransform = null;
            TargetReached?.Invoke();
            return;
        }

        d = Mathf.Sqrt(d);
        v /= d;
        var s = Mathf.Min(moveSpeed * dt, d);

        chain[0].position += s * v;
    }
}