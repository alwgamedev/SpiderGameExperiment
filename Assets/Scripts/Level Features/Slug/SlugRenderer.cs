using System;
using Unity.Collections;
using Unity.Mathematics;
using Unity.U2D.Physics;
using UnityEngine;

[Serializable]
public struct SlugRenderer
{
    [SerializeField] SimpleLineRenderer lr;

    NativeArray<float4> controlPoint;

    // public float Orientation
    // {
    //     get => transform.localScale.x;
    //     set
    //     {
    //         var s = transform.localScale;
    //         s.x = value;
    //         transform.localScale = s;
    //         lr.SetOrientation(Mathf.Sign(value));
    //     }
    // }

    public readonly void OnValidate()
    {
        lr.OnValidate();
    }

    public void Initialize()
    {
        lr.Initialize();
        controlPoint = new(3, Allocator.Persistent);
    }

    public readonly void OnDestroy()
    {
        lr.OnDestroy();
        if (controlPoint.IsCreated)
        {
            controlPoint.Dispose();
        }
    }

    public readonly void SetPose(SlugPose pose, Matrix4x4 transform, PhysicsRotate aim)
    {
        UpdateControlPoints(pose.Transform(transform).Aim(aim));
        lr.InterpolatePositions(controlPoint);
    }

    public readonly void SetOrientation(float o)
    {
        lr.SetOrientation(o);
    }

    public readonly void SetVisible(bool val)
    {
        lr.SetVisible(val);
    }

    private readonly void UpdateControlPoints(SlugPose pose)
    {
        var ctrlPt = controlPoint.Reinterpret<SlugPose>(16);
        ctrlPt[0] = pose;
    }
}