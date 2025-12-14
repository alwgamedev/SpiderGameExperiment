using System;
using UnityEngine;

public class RopeRenderer : MonoBehaviour
{
    [Min(2)][SerializeField] int endCapTriangles;
    [SerializeField] float taperLength;//measured in number of rope segments, just to keep things simple
    [SerializeField] float taperBaseScale;

    Mesh mesh; 
    MeshRenderer meshRenderer;
    MeshFilter meshFilter;
    Material material;

    Vector4[] nodePositions;

    const string positionsProperty = "_NodePositions";

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        meshFilter = GetComponent<MeshFilter>();
    }

    public void Start()
    {
        material = new Material(meshRenderer.material);
        meshRenderer.material = material;
        meshRenderer.enabled = false;
    }

    public void OnRopeSpawned(Rope rope)
    {
        if (nodePositions == null || nodePositions.Length != rope.position.Length)
        {
            nodePositions = new Vector4[rope.position.Length];
            CreateMesh(nodePositions.Length);
        }
        meshRenderer.enabled = true;
        material.SetFloat("_HalfWidth", 0.5f * rope.width);
        UpdateRenderPositions(rope);
    }

    public void OnRopeDestroyed()
    {
        meshRenderer.enabled = false;
    }

    public void UpdateRenderPositions(Rope rope)
    {
        float taperMult = taperBaseScale;
        var taperRate = (1 - taperBaseScale) / (float)taperLength;
        for (int i = 0; i < nodePositions.Length; i++)
        {
            if (taperMult < 1 && i > rope.AnchorPointer)
            {
                taperMult += Mathf.Min(taperRate * Vector2.Distance(nodePositions[i - 1], nodePositions[i]), 1);
            }
            nodePositions[i] = new(rope.position[i].x, rope.position[i].y, taperMult, 0);
        }
        material.SetVectorArray(positionsProperty, nodePositions);
    }

    private void CreateMesh(int numNodes)//nodeSpacing used to determine uv with endcaps
    {
        //with 3 nodes and 2 endcap triangles, the vertices would be ordered like this
        //               1 ---------------- 3 ---------------- 5
        //              /|                  |                  |\
        //             7 |                  |                  | 9
        //             | |                  |                  | |
        //             6 |                  |                  | 8
        //              \|                  |                  |/
        //               0 ---------------- 2 ---------------- 4

        mesh = new();

        var vertices = new Vector3[2 * numNodes + endCapTriangles];
        var triangles = new int[6 * (numNodes - 1) + 3 * endCapTriangles];
        var uv = new Vector2[vertices.Length];

        float du = 1 / (numNodes - 1);

        int k = -1;
        for (int i = 0; i < numNodes - 1; i++)
        {
            var u = i * du;
            var j = i << 1;
            uv[j] = new Vector2(u, 0);
            uv[j + 1] = new Vector2(u, 1);
            triangles[++k] = j;
            triangles[++k] = j + 1;
            triangles[++k] = j + 3;
            triangles[++k] = j;
            triangles[++k] = j + 3;
            triangles[++k] = j + 2;
        }

        uv[2 * numNodes - 2] = new Vector2(1, 0);
        uv[2 * numNodes - 1] = new Vector2(1, 1);

        var dv = 1 / (float)(endCapTriangles + 1);

        //endcap at beginning of rope
        int bottom = 2 * (numNodes - 1);
        int top = bottom + 1;
        int l = top + 1;
        for (int i = l; i < l + endCapTriangles; i++)
        {
            uv[i] = new Vector2(1, (i - l + 1) * dv);
            triangles[++k] = i;
            triangles[++k] = bottom;
            triangles[++k] = top;
            bottom = i;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uv;
        mesh.RecalculateNormals();

        meshFilter.mesh = mesh;

        material.SetInt("_NumNodes", numNodes);
        material.SetInt("_EndcapTriangles", endCapTriangles);
    }
}