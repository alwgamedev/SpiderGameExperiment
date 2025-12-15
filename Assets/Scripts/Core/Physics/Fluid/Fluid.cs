using Unity.Cinemachine;
using UnityEngine;

public class Fluid : MonoBehaviour
{
    const int MAX_NUM_VERTICES = 4091;

    [SerializeField] int width;
    [SerializeField] int height;
    [SerializeField] float cellSize;
    [SerializeField] int numParticles;
    [SerializeField] float particleRadius;
    [SerializeField] float particleDrag;
    [SerializeField] float collisionBounciness;
    [SerializeField] LayerMask collisionMask;
    [SerializeField] float spawnRate;
    [SerializeField] float spawnSpread;
    [SerializeField] int pushApartIterations;
    [SerializeField] int gaussSeidelIterations;
    [SerializeField] float overRelaxation;
    [SerializeField] float simulationWeight;
    [SerializeField] int updateFrequency;

    FluidSimulator simulator;
    Material material;

    int updateCounter = 0;

    Vector4[] vertexData;

    Vector3 lastMousePosition;

    int VertexIndex(int i, int j)
    {
        return i * (width + 1) + j;
    }

    private void OnValidate()
    {
        int vertices = (width + 1) * (height + 1);
        if (vertices > MAX_NUM_VERTICES)
        {
            height = -1 + MAX_NUM_VERTICES / (width + 1);
        }
    }

    //private void OnDrawGizmos()
    //{
    //    simulator?.DrawParticleGizmos(transform.position.x, transform.position.y);
    //    simulator?.DrawVelocityFieldGizmos(transform.position.x, transform.position.y);
    //}

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
        vertexData = new Vector4[(width + 1) * (height + 1)];
    }

    private void Update()
    {
        var p = Camera.main.ScreenToWorldPoint(Input.mousePosition) - transform.position;
        if (!p.IsNaN())
        {
            if (simulator != null && Input.GetKey(KeyCode.Mouse0))
            {
                if (p.x > 0 && p.x < simulator.worldWidth && p.y > 0 && p.y < simulator.worldHeight)
                {
                    var n = (int)Mathf.Ceil(spawnRate * Time.deltaTime);
                    simulator.SpawnParticles(n, spawnSpread, p.x, p.y, (p.x - lastMousePosition.x) / Time.deltaTime, (p.y - lastMousePosition.y) / Time.deltaTime);
                }
            }
            lastMousePosition = p;
        }
    }

    private void FixedUpdate()
    {
        if (++updateCounter > updateFrequency)
        {
            simulator?.Update(updateFrequency * Time.deltaTime, transform.position.x, transform.position.y, particleDrag, pushApartIterations,
            gaussSeidelIterations, overRelaxation, simulationWeight);
            updateCounter -= updateFrequency;
        }
    }

    private void LateUpdate()
    {
        if (simulator != null)
        {
            for (int i = 0; i < height + 1;  i++)
            {
                for (int j = 0; j < width + 1; j++)
                {
                    vertexData[VertexIndex(i, j)] = new(simulator.DensityAtVertex(i, j), 0f, 0f, 0f);
                }
            }

            material.SetVectorArray("_Density", vertexData);
        }
    }

    public void InitializeSimulator()
    {
        simulator = new(width, height, cellSize, numParticles, Physics2D.gravity.y, 
            particleRadius, collisionBounciness, collisionMask);
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
    }
}