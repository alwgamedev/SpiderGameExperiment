using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public struct SlugRenderer
{
    public SimpleLineRenderer bodyRenderer;
    public SimpleLineRenderer eyeRenderer;

    NativeArray<float4> bodyControlPoint;
    NativeArray<float4> eyeControlPoint;

    public readonly void OnValidate()
    {
        bodyRenderer?.OnValidate();
        eyeRenderer?.OnValidate();
    }

    public void Initialize()
    {
        bodyRenderer.Initialize();
        bodyControlPoint = new(3, Allocator.Persistent);
        var offset = new Vector2(MathTools.RandomFloat(-10000, 10000), MathTools.RandomFloat(-10000, 10000));
        bodyRenderer.material.SetVector("_RandomOffset", offset);

        eyeRenderer.Initialize();
        eyeControlPoint = new(2, Allocator.Persistent);
    }

    public readonly void OnDestroy()
    {
        bodyRenderer.OnDestroy();
        if (bodyControlPoint.IsCreated)
        {
            bodyControlPoint.Dispose();
        }

        eyeRenderer.OnDestroy();
        if (eyeControlPoint.IsCreated)
        {
            eyeControlPoint.Dispose();
        }
    }

    public void SetBodyPose(in SlugPose pose)
    {
        var ctrlPt = bodyControlPoint.Reinterpret<SlugPose>(16);
        ctrlPt[0] = pose;
        bodyRenderer.InterpolatePositions(bodyControlPoint);
    }

    public void SetEyePose(Vector2 p0, Vector2 v0, Vector2 p1, Vector2 v1)
    {
        eyeControlPoint[0] = new(p0, v0);
        eyeControlPoint[1] = new(p1, v1);
        eyeRenderer.InterpolatePositions(eyeControlPoint);
    }

    public readonly void SetOrientation(float o)
    {
        bodyRenderer.SetOrientation(o);
        eyeRenderer.SetOrientation(o);
    }

    public readonly void SetVisible(bool val)
    {
        bodyRenderer.SetVisible(val);
        eyeRenderer.SetVisible(val);
    }
}