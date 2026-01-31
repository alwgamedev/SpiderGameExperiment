using System;
using UnityEngine;

public class ArmAnchor : MonoBehaviour
{
    [SerializeField] Transform[] chain;//transforms not nested
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
        //Vector3 dp = new(position.x - chain[0].position.x, position.y - chain[0].position.y, 0);
        //for (int i = 0; i < chain.Length; i++)
        //{
        //    chain[i].position += dp;
        //}
    }

    public void BeginTargetingTransform(Transform target)
    {
        targetTransform = target;
    }

    private void MoveTowardsTarget(float dt)
    {
        //Vector2 p0 = chain[0].position;
        //Vector2 p1 = targetTransform.position;
        var v = targetTransform.position - chain[0].position;//p1 - p0;
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

        //Vector3 dp = s * v;
        //for (int i = 0; i < chain.Length; i++)
        //{
        //    chain[i].position += dp;
        //}
    }
}