using UnityEngine;
using Unity.Collections;
using System;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;

[Serializable]
public class SimpleLineRenderer
{
    public GraphicsBuffer nodePosition;
    [SerializeField] int numNodes;
    [SerializeField] float halfWidth;
    [SerializeField] MeshRenderer meshRenderer;
    [SerializeField] MeshFilter meshFilter;

    Mesh mesh;
    Material material;
    float orientation;

    readonly int numNodesProperty = Shader.PropertyToID("_NumNodes");
    readonly int nodePositionProperty = Shader.PropertyToID("_NodePosition");
    readonly int halfWidthProperty = Shader.PropertyToID("_HalfWidth");
    readonly int orientationProperty = Shader.PropertyToID("_Orientation");
    //^this is a global width scaler; you can have variable width per segment by scaling the segment normal (nodePosition.zw)

    public int NumNodes => numNodes;
    public float Orientation => orientation;

    public void OnValidate()
    {
        if (material)
        {
            material.SetFloat(halfWidthProperty, halfWidth);
        }        
    }

    public void Initialize()
    {
        if (material)
        {
            UnityEngine.Object.Destroy(material);
        }
        material = new(meshRenderer.sharedMaterial);
        meshRenderer.sharedMaterial = material;
        material.SetInt(numNodesProperty, numNodes);
        material.SetFloat(halfWidthProperty, halfWidth);

        if (mesh)
        {
            UnityEngine.Object.Destroy(material);
        }
        mesh = CreateMesh(numNodes);
        meshFilter.sharedMesh = mesh;

        nodePosition?.Release();
        nodePosition = new(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.LockBufferForWrite, numNodes, 16);
        material.SetBuffer(nodePositionProperty, nodePosition);
    }

    public void SetVisible(bool val)
    {
        meshRenderer.enabled = val;
    }

    public void SetOrientation(float orientation)
    {
        this.orientation = orientation;
        material.SetFloat(orientationProperty, orientation);
    }

    public void OnDestroy()
    {
        UnityEngine.Object.Destroy(mesh);
        UnityEngine.Object.Destroy(material);
        nodePosition?.Release();
    }

    public static Mesh CreateMesh(int numNodes)
    {
        //vertices are ordered like this
        //      1 ---------------- 3 ---------------- 5
        //      |                  |                  |
        //      |                  |                  |
        //      |                  |                  |  ...
        //      |                  |                  | 
        //      |                  |                  |
        //      0 ---------------- 2 ---------------- 4

        var vertices = new NativeArray<Vector3>(2 * numNodes, Allocator.Temp);
        var uv = new NativeArray<Vector2>(vertices.Length, Allocator.Temp);
        var triangles = new NativeArray<int>(6 * (numNodes - 1), Allocator.Temp);

        var du = 1f / (numNodes - 1);
        int k = -1;
        for (int i = 0; i < numNodes - 1; i++)
        {
            var u = i * du;
            var j = 2 * i;
            uv[j] = new(u, 0);
            uv[j + 1] = new(u, 1);
            triangles[++k] = j;
            triangles[++k] = j + 1;
            triangles[++k] = j + 3;
            triangles[++k] = j;
            triangles[++k] = j + 3;
            triangles[++k] = j + 2;
        }

        uv[2 * numNodes - 2] = new Vector2(1, 0);
        uv[2 * numNodes - 1] = new Vector2(1, 1);

        Mesh mesh = new();
        mesh.SetVertices(vertices);
        mesh.SetIndices(triangles, MeshTopology.Triangles, 0);
        mesh.SetUVs(0, uv);
        mesh.RecalculateNormals();
        return mesh;
    }

    public void InterpolatePositions(NativeArray<float4> controlPoint)
    {
        var nodePos = nodePosition.LockBufferForWrite<float4>(0, nodePosition.count);

        var controlPointF4 = controlPoint.Reinterpret<float4>();
        new InterpolatePositionsJob(controlPointF4, nodePos).Run();

        nodePosition.UnlockBufferAfterWrite<float4>(nodePosition.count);
    }

    [BurstCompile]
    struct InterpolatePositionsJob : IJob
    {
        [ReadOnly] NativeArray<float4> controlPoint;
        NativeArray<float4> nodePosition;

        public InterpolatePositionsJob(NativeArray<float4> controlPoint, NativeArray<float4> nodePosition)
        {
            this.controlPoint = controlPoint;
            this.nodePosition = nodePosition;
        }

        public void Execute()
        {
            int segsPerCtrl = (int)math.ceil((nodePosition.Length - 1f) / (controlPoint.Length - 1));
            float segsPerCtrlInv = 1f / segsPerCtrl;

            for (int i = 0; i < nodePosition.Length; i++)
            {
                var quot = i / segsPerCtrl;
                var remainder = i % segsPerCtrl;
                var c0 = controlPoint[quot];
                var c1 = controlPoint[math.min(quot + 1, controlPoint.Length - 1)];
                var t = remainder * segsPerCtrlInv;
                float2 p = MathTools.CubicInterpolation(c0.xy, c0.zw, c1.xy, c1.zw, t);
                float2 v = MathTools.CubicTangent(c0.xy, c0.zw, c1.xy, c1.zw, t);
                var u = math.normalize(v);

                nodePosition[i] = new float4(p, u.CCWPerp());
            }
        }
    }
}