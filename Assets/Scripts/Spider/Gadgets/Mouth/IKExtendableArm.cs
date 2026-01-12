using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class IKExtendableArm : MonoBehaviour
{
    [SerializeField] Transform[] chain;
    [SerializeField] Transform grabber;
    [SerializeField] float[] collisionHalfWidth;
    [SerializeField] LayerMask collisionMask;
    [SerializeField] int collisionIntervals;
    [SerializeField] int ikIterations;
    [SerializeField] float ikTolerance;
    [SerializeField] float smoothingRate;

    Vector2[] position;
    Vector2[] newPosition;
    float[] gradient;
    int[] lambdaWeight;
    float[] length;
    float maxLength;

    Vector2 targetPosition;
    bool targetPositionInitialized;

    private void OnDrawGizmos()
    {
        if (Application.isPlaying)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(targetPosition, 1f);
        }
    }

    private void Start()
    {
        maxLength = 0;
        length = new float[chain.Length - 1];
        for (int i = 0; i < length.Length; i++)
        {
            length[i] = Vector2.Distance(chain[i].position, chain[i + 1].position);
            maxLength += length[i];
        }

        position = new Vector2[chain.Length];
        newPosition = new Vector2[chain.Length];
        gradient = new float[chain.Length - 1];
        lambdaWeight = new int[chain.Length - 1];
    }

    private void Update()
    {
        if (Mouse.current.leftButton.isPressed)
        {
            var p = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            targetPosition = p;
            if (!targetPositionInitialized)
            {
                targetPositionInitialized = true;
            }
            //if (!targetPositionInitialized)
            //{
            //    targetPosition = p;
            //    targetPositionInitialized = true;
            //}
            //else
            //{
            //    targetPosition = new(
            //    MathTools.LerpAtConstantRate(targetPosition.x, p.x, targetLerpRate, Time.deltaTime),
            //    MathTools.LerpAtConstantRate(targetPosition.y, p.y, targetLerpRate, Time.deltaTime)
            //    );
            //}
        }
    }

    private void FixedUpdate()
    {
        if (!targetPositionInitialized)
        {
            return;
        }

        for (int j = 0; j < chain.Length; j++)
        {
            position[j] = chain[j].position;
        }
        for (int i = 0; i < ikIterations; i++)
        {
            //if (!GoodIK.RunIteration(position, length, gradient, targetPosition, ikTolerance, maxLength, smoothingRate))
            //{
            //    break;
            //}
            if (!GoodIK.RunIterationWithCollisionAvoidance(position, newPosition, length, gradient, lambdaWeight, targetPosition,
                ikTolerance, maxLength, smoothingRate, collisionMask, collisionIntervals))
            {
                break;
            }
        }

        for (int j = 0; j < chain.Length; j++)
        {
            chain[j].position = position[j];
            if (j < chain.Length - 1)
            {
                chain[j].right = position[j + 1] - position[j];
            }
        }

        grabber.right = targetPosition - (Vector2)grabber.position;
        //with the grabber arm being so much shorter, the IK tends to rotate it weirdly
        //so best to omit from the chain and justs point it towards target
    }
}