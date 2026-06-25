using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public class RopeRenderer
{
    [SerializeField] MeshRenderer meshRenderer;
    [SerializeField] MeshFilter meshFilter;
    [SerializeField] bool drawGizmos;
    [Min(2)][SerializeField] int endcapTriangles;
    [SerializeField] float taperLength;//measured in number of rope segments, just to keep things simple
    [SerializeField] float taperBaseScale;

    Mesh ropeMesh;
    Material bodyMaterial;
    Material endcapMaterial;

    GraphicsBuffer nodePositionGB;
    readonly int nodePositionProperty = Shader.PropertyToID("_NodePosition");
    readonly int numNodesProperty = Shader.PropertyToID("_NumNodes");
    readonly int endcapTrianglesProperty = Shader.PropertyToID("_EndcapTriangles");
    readonly int halfWidthProperty = Shader.PropertyToID("_HalfWidth");
    readonly int orientationProperty = Shader.PropertyToID("_Orientation");

    public void Initialize()
    {
        var mats = new List<Material>();
        meshRenderer.GetSharedMaterials(mats);
        bodyMaterial = new Material(mats[0]);
        endcapMaterial = new Material(mats[1]);
        mats[0] = bodyMaterial;
        mats[1] = endcapMaterial;
        meshRenderer.SetSharedMaterials(mats);
        meshRenderer.enabled = false;
    }

    public void OnDestroy()
    {
        UnityEngine.Object.Destroy(bodyMaterial);
        UnityEngine.Object.Destroy(endcapMaterial);
        UnityEngine.Object.Destroy(ropeMesh);
        nodePositionGB?.Release();
    }

    public void OnRopeSpawned(FastRope rope, float2 sourcePosition)
    {
        if (nodePositionGB == null || !nodePositionGB.IsValid() || nodePositionGB.count != rope.NumNodes)
        {
            InitializeBuffer(rope.NumNodes);
            bodyMaterial.SetBuffer(nodePositionProperty, nodePositionGB);
            endcapMaterial.SetBuffer(nodePositionProperty, nodePositionGB);
            InitializeRopeMesh(rope.NumNodes);
        }
        meshRenderer.enabled = true;
        SetRenderWidth(rope.settings.width);
        UpdateRenderPositions(rope, sourcePosition);
    }

    public void SetRenderWidth(float ropeWidth)
    {
        var hw = 0.5f * ropeWidth;
        bodyMaterial.SetFloat(halfWidthProperty, hw);
        endcapMaterial.SetFloat(halfWidthProperty, hw);
    }

    public void SetOrientation(bool facingRight)
    {
        var o = facingRight ? 1 : -1;
        bodyMaterial.SetFloat(orientationProperty, o);
        endcapMaterial.SetFloat(orientationProperty, o);
    }

    public void OnRopeDestroyed()
    {
        meshRenderer.enabled = false;
    }

    public void UpdateRenderPositions(FastRope rope, float2 sourcePosition)
    {
        var nodePosition = nodePositionGB.LockBufferForWrite<float4>(0, nodePositionGB.count);
        rope.SetRenderPositions(nodePosition, sourcePosition, taperBaseScale, taperLength);
        nodePositionGB.UnlockBufferAfterWrite<float4>(nodePositionGB.count);
    }

    private void InitializeBuffer(int numNodes)
    {
        nodePositionGB?.Release();
        nodePositionGB = new(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.LockBufferForWrite, numNodes, 16);
    }

    private void InitializeRopeMesh(int numNodes)
    {
        if (ropeMesh)
        {
            UnityEngine.Object.Destroy(ropeMesh);
        }
        //with 3 nodes and 2 endcap triangles, the vertices would be ordered like this
        //               1 ---------------- 3 ---------------- 5
        //               |                  |                  |\
        //               |                  |                  | 7
        //               |                  |                  | |
        //               |                  |                  | 6
        //               |                  |                  |/
        //               0 ---------------- 2 ---------------- 4

        ropeMesh = new() { subMeshCount = 2 };

        var vertices = new NativeArray<Vector3>(2 * numNodes + endcapTriangles, Allocator.Temp);
        var uv = new NativeArray<Vector2>(vertices.Length, Allocator.Temp);
        var bodyTris = new NativeArray<int>(6 * (numNodes - 1), Allocator.Temp);
        var endcapTris = new NativeArray<int>(3 * endcapTriangles, Allocator.Temp);

        //SUBMESH 0: rope body
        var du = 1f / (numNodes - 1);
        int k = -1;
        for (int i = 0; i < numNodes - 1; i++)
        {
            var u = i * du;
            var j = i << 1;
            uv[j] = new Vector2(u, 0);
            uv[j + 1] = new Vector2(u, 1);
            bodyTris[++k] = j;
            bodyTris[++k] = j + 1;
            bodyTris[++k] = j + 3;
            bodyTris[++k] = j;
            bodyTris[++k] = j + 3;
            bodyTris[++k] = j + 2;
        }

        uv[2 * numNodes - 2] = new Vector2(1, 0);
        uv[2 * numNodes - 1] = new Vector2(1, 1);

        //SUBMESH 1: rope endcap
        var dv = 1f / (endcapTriangles + 1);
        var bottom = 2 * (numNodes - 1);
        var top = bottom + 1;
        int l = top + 1;
        k = -1;
        for (int i = l; i < l + endcapTriangles; i++)
        {
            uv[i] = new Vector2(1, (i - l + 1) * dv);
            endcapTris[++k] = i;
            endcapTris[++k] = bottom;
            endcapTris[++k] = top;
            bottom = i;
        }

        ropeMesh.SetVertices(vertices);
        ropeMesh.SetIndices(bodyTris, MeshTopology.Triangles, 0);
        ropeMesh.SetIndices(endcapTris, MeshTopology.Triangles, 1);
        ropeMesh.SetUVs(0, uv);
        ropeMesh.RecalculateNormals();

        meshFilter.mesh = ropeMesh;

        bodyMaterial.SetInt(numNodesProperty, numNodes);
        bodyMaterial.SetInt(endcapTrianglesProperty, endcapTriangles);
        endcapMaterial.SetInt(numNodesProperty, numNodes);
        endcapMaterial.SetInt(endcapTrianglesProperty, endcapTriangles);
    }
}