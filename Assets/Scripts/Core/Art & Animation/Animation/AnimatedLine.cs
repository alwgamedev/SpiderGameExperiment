using UnityEngine;
using Unity.Collections;

public class AnimatedLine : MonoBehaviour
{
    [SerializeField] int segmentsPerControlPoint;
    [SerializeField] int numNodes;
    [SerializeField] float halfWidth;
    [SerializeField] MeshRenderer meshRenderer;
    [SerializeField] MeshFilter meshFilter;

    Mesh mesh;
    Material material;
    GraphicsBuffer nodePosition;

    readonly int numNodesProperty = Shader.PropertyToID("_NumNodes");
    readonly int nodePositionProperty = Shader.PropertyToID("_NodePosition");
    readonly int halfWidthProperty = Shader.PropertyToID("_HalfWidth");
    //^this is a global width scaler; you can have variable width per segment by adjusting the length of the segment normal
    //(.zw of the nodePosition)

    private void OnValidate()
    {
        if (material)
        {
            material.SetFloat(halfWidthProperty, halfWidth);
        }        
    }

    private void Start()
    {
        material = new(meshRenderer.sharedMaterial);
        meshRenderer.sharedMaterial = material;

        material.SetFloat(halfWidthProperty, halfWidth);

        nodePosition = new(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.LockBufferForWrite, numNodes, 16);
        material.SetBuffer(nodePositionProperty, nodePosition);
    }

    void OnDestroy()
    {
        Destroy(mesh);
        Destroy(material);
        nodePosition?.Release();
    }

    private void CreateMesh()
    {
        //vertices are ordered like this
        //               1 ---------------- 3 ---------------- 5
        //               |                  |                  |
        //               |                  |                  |
        //               |                  |                  |  ...
        //               |                  |                  | 
        //               |                  |                  |
        //               0 ---------------- 2 ---------------- 4

        if (mesh)
        {
            Destroy(mesh);
        }

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

        mesh = new();
        mesh.SetVertices(vertices);
        mesh.SetIndices(triangles, MeshTopology.Triangles, 0);
        mesh.SetUVs(0, uv);
        mesh.RecalculateNormals();
        meshFilter.sharedMesh = mesh;

        material.SetInt(numNodesProperty, numNodes);
    }
}