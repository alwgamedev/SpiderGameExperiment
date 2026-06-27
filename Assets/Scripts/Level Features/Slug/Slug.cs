using System;
using Unity.Collections;
using Unity.Mathematics;
using Unity.U2D.Physics;
using UnityEngine;

public class Slug : MonoBehaviour
{
    [Serializable]
    public struct SlugPose
    {
        public Vector2 p0;
        public Vector2 v0;
        public Vector2 p1;
        public Vector2 v1;
        public Vector2 p2;
        public Vector2 v2;

        public readonly SlugPose Transform(Matrix4x4 transform)
        {
            return new()
            {
                p0 = transform.MultiplyPoint3x4(p0),
                p1 = transform.MultiplyPoint3x4(p1),
                p2 = transform.MultiplyPoint3x4(p2),
                v0 = transform.MultiplyVector(v0),
                v1 = transform.MultiplyVector(v1),
                v2 = transform.MultiplyVector(v2)
            };
        }

        public readonly SlugPose Aim(PhysicsRotate aim)
        {
            return new()
            {
                p0 = p0,
                p1 = p1,
                p2 = p1 + aim.RotateVector(p2 - p1),
                v0 = v0,
                v1 = aim.RotateVector(v1),
                v2 = aim.RotateVector(v2)
            };
        }
    }

    public SlugPose pose;
    public PhysicsRotate aim;
    [SerializeField] SimpleLineRenderer lr;

    NativeArray<float4> controlPoint;

    void OnValidate()
    {
        lr.OnValidate();

        if (aim.direction == Vector2.zero)
        {
            aim = PhysicsRotate.identity;
        }
    }

    void Start()
    {
        lr.Start();
        controlPoint = new(3, Allocator.Persistent);
    }

    void LateUpdate()
    {
        //it will cull when transform is far away, so may as well keep gameobject transform accurate
        //and use it to set slug transform (we'll probably only be setting the transform position on spawn)
        UpdateControlPoints(pose.Transform(transform.localToWorldMatrix).Aim(aim));
        lr.InterpolatePositions(controlPoint);
    }

    void OnDestroy()
    {
        lr.OnDestroy();
        if (controlPoint.IsCreated)
        {
            controlPoint.Dispose();
        }
    }

    void UpdateControlPoints(SlugPose pose)
    {
        controlPoint[0] = new(pose.p0, pose.v0);
        controlPoint[1] = new(pose.p1, pose.v1);
        controlPoint[2] = new(pose.p2, pose.v2);
    }
}