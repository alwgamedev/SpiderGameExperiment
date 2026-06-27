using UnityEngine;
using Unity.Collections;
using UnityEngine.U2D;

[ExecuteAlways]
public class BezierArcLengthBuffer : MonoBehaviour
{
    [SerializeField] VFXGraphicsBuffer<float> bufferManager;
    [SerializeField] Vector3 bezierPtA;
    [SerializeField] Vector3 bezierPtB;
    [SerializeField] Vector3 bezierPtC;
    [SerializeField] Vector3 bezierPtD;

    NativeArray<float> arcLengthPosition;

    readonly int bezierPtAProperty = Shader.PropertyToID("BezierPtA");
    readonly int bezierPtBProperty = Shader.PropertyToID("BezierPtB");
    readonly int bezierPtCProperty = Shader.PropertyToID("BezierPtC");
    readonly int bezierPtDProperty = Shader.PropertyToID("BezierPtD");

    public void SetBezierPoints(Vector3 ptA, Vector3 ptB, Vector3 ptC, Vector3 ptD)
    {
        bezierPtA = ptA;
        bezierPtB = ptB;
        bezierPtC = ptC;
        bezierPtD = ptD;
        UpdateArcLength();
    }

    void OnValidate()
    {
        bufferManager?.OnValidate();
        UpdateArcLength();
    }

    void OnEnable()
    {
        bufferManager?.InitializeBuffer();
        bufferManager?.AssignBufferToVFX();
        UpdateArcLength();
    }

    void OnDisable()
    {
        bufferManager.ReleaseBuffer();
    }

    void OnDestroy()//
    {
        if (arcLengthPosition.IsCreated)
        {
            arcLengthPosition.Dispose();
        }

        bufferManager.ReleaseBuffer();
    }

    void UpdateArcLength()
    {
        if (!bufferManager?.vfx)
        {
            return;
        }

        var ct = bufferManager.bufferWrapper.BufferCount;
        if (!arcLengthPosition.IsCreated || arcLengthPosition.Length != ct)
        {
            AllocateNativeArray(ct);
        }

        var denom = 1f / (ct - 1);
        var vfxTransform = bufferManager.vfx.transform;
        var p0 = BezierUtility.BezierPoint(bezierPtB, bezierPtA, bezierPtD, bezierPtC, 0);
        p0 = vfxTransform.TransformPoint(p0);

        arcLengthPosition[0] = 0;
        for (int i = 1; i < ct; i++)
        {
            var t = denom * i;
            var p1 = BezierUtility.BezierPoint(bezierPtB, bezierPtA, bezierPtD, bezierPtC, t);
            p1 = vfxTransform.TransformPoint(p1);
            arcLengthPosition[i] = arcLengthPosition[i - 1] + Vector3.Distance(p0, p1);
            p0 = p1;
        }

        bufferManager.vfx.SetVector3(bezierPtAProperty, bezierPtA);
        bufferManager.vfx.SetVector3(bezierPtBProperty, bezierPtB);
        bufferManager.vfx.SetVector3(bezierPtCProperty, bezierPtC);
        bufferManager.vfx.SetVector3(bezierPtDProperty, bezierPtD);
        bufferManager.SetData(arcLengthPosition);

        if (!Application.isPlaying)
        {
            arcLengthPosition.Dispose();
        }
    }

    void AllocateNativeArray(int length)
    {
        if (arcLengthPosition.IsCreated)
        {
            arcLengthPosition.Dispose();
        }

        var allocator = Application.isPlaying ? Allocator.Persistent : Allocator.TempJob;
        arcLengthPosition = new(length, allocator);
    }
}