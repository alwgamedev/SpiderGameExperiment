using System;
using UnityEngine;

public class FLIPFluidManager : MonoBehaviour
{
    [SerializeField] int width;
    [SerializeField] int height;
    [SerializeField] float cellSize;
    [SerializeField] int numParticles;
    [SerializeField] float particleRadius;
    [SerializeField] float collisionBounciness;
    [SerializeField] LayerMask collisionMask;
    [SerializeField] float spawnRate;
    [SerializeField] float spawnSpread;
    [SerializeField] int pushApartIterations;
    [SerializeField] int gaussSeidelIterations;
    [SerializeField] float overRelaxation;
    [SerializeField] float flipWeight;
    [SerializeField] float fluidDensity;
    [SerializeField] float obstacleVelocityNormalizer;
    [SerializeField] int updateFrequency;

    FLIPFluidSimulator simulator;
    Material material;

    int updateCounter = 0;

    Vector4[] vertexData;

    //Vector3 lastMousePosition;

    int VertexIndex(int i, int j)
    {
        return i * (width + 1) + j;
    }

    private void OnDrawGizmos()
    {
        simulator?.DrawParticleGizmos(transform.position);
        simulator?.DrawVelocityFieldGizmos(transform.position);
    }

    private void Awake()
    {
        var meshRender = GetComponent<MeshRenderer>();
        material = new Material(meshRender.material);
        meshRender.material = material;
    }

    private void Start()
    {
        CreateMesh();
        InitializeSimulator();
    }

    private void Update()
    {
        if (simulator != null && Input.GetKey(KeyCode.Mouse0))
        {
            var p = Camera.main.ScreenToWorldPoint(Input.mousePosition) - transform.position;
            if (p.x > 0 && p.x < simulator.worldWidth && p.y > 0 && p.y < simulator.worldHeight)
            {
                var n = (int)Mathf.Ceil(spawnRate * Time.deltaTime);
                simulator.SpawnParticles(n, spawnSpread, p);
            }
        }
    }

    private void LateUpdate()
    {
        if (simulator != null)
        {
            simulator.FillObstacleDensities();

            int n = (simulator.width + 1) * (simulator.height + 1) - 1;
            int q0 = n / 4;
            int r0 = n % 4;
            for (int q = 0; q < q0; q++)
            {
                int k = q << 2;
                vertexData[q].x = simulator.DensityAtVertex(k++);
                vertexData[q].y = simulator.DensityAtVertex(k++);
                vertexData[q].z = simulator.DensityAtVertex(k++);
                vertexData[q].w = simulator.DensityAtVertex(k);
            }

            int k0 = q0 << 2;
            vertexData[q0].x = simulator.DensityAtVertex(k0++);
            if (r0 > 0)
            {
                vertexData[q0].y = simulator.DensityAtVertex(k0++);
            }
            if (r0 > 1)
            {
                vertexData[q0].z = simulator.DensityAtVertex(k0++);
            }
            if (r0 > 2)
            {
                vertexData[q0].w = simulator.DensityAtVertex(k0);

            }

            material.SetVectorArray("_Density", vertexData);
        }
    }

    private void FixedUpdate()
    {
        if (++updateCounter > updateFrequency)
        {
            simulator?.Update(updateFrequency * Time.deltaTime, transform.position,
                pushApartIterations, collisionBounciness,
                gaussSeidelIterations, overRelaxation, flipWeight,
                fluidDensity, obstacleVelocityNormalizer);
            updateCounter -= updateFrequency;
        }
    }

    private void InitializeSimulator()
    {
        simulator = new(width, height, cellSize, numParticles, Physics2D.gravity.y,
            particleRadius, collisionMask);
    }

    public void CreateMesh()
    {
        Mesh mesh = new();

        var vertices = new Vector3[(width + 1) * (height + 1)];
        var uv = new Vector2[vertices.Length];
        var triangles = new int[6 * width * height];

        var du = 1 / (float)width;
        var dv = 1 / (float)height;

        for (int i = 0; i < height + 1; i++)
        {
            for (int j = 0; j < width + 1; j++)
            {
                vertices[VertexIndex(i, j)] = new(j * cellSize, i * cellSize);
                uv[VertexIndex(i, j)] = new(j * du, i * dv);
            }
        }

        int k = -1;
        for (int i = 0; i < height; i++)
        {
            for (int j = 0; j < width; j++)
            {
                triangles[++k] = VertexIndex(i, j);
                triangles[++k] = VertexIndex(i + 1, j + 1);
                triangles[++k] = VertexIndex(i, j + 1);
                triangles[++k] = VertexIndex(i, j);
                triangles[++k] = VertexIndex(i + 1, j);
                triangles[++k] = VertexIndex(i + 1, j + 1);

            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uv;
        mesh.RecalculateNormals();
        GetComponent<MeshFilter>().mesh = mesh;

        Array.Resize(ref vertexData, (width + 1) * (height + 1));
    }
}