using UnityEngine;

public class WaterMeshManager : MonoBehaviour
{
    [SerializeField] float agitationScale;
    [SerializeField] WaterMeshSimulator simulation;

    Mesh mesh;
    public Vector3[] vertices;
    public Vector2[] uv;
    public int[] triangles;

    private void Start()
    {
        Initialize();
    }

    private void LateUpdate()
    {
        UpdateMeshVertices();
    }

    private void FixedUpdate()
    {
        simulation.UpdateSimulation(Time.deltaTime);
    }

    public float FluidHeight(float worldX)
    {
        return simulation.FluidHeight(worldX - transform.position.x);
    }

    public void HandleDisplacement(Collider2D c, float dt)
    {
        simulation.HandleDisplacement(c, transform.position, agitationScale, dt);
    }

    public void ResizeCollider()
    {
        if (TryGetComponent(out BoxCollider2D b))
        {
            b.size = new(simulation.Width, simulation.Height);
            b.offset = Vector2.zero;
        }
    }


    public void Initialize()
    {
        simulation.Initialize();

        mesh = new Mesh();

        int numQuads = simulation.NumQuads;

        vertices = new Vector3[simulation.vertices.Length];
        /*var*/ uv = new Vector2[vertices.Length];
        /*var*/ triangles = new int[6 * numQuads];

        float halfWidth = 0.5f * simulation.Width;
        float halfHeight = 0.5f * simulation.Height;

        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = simulation.vertices[i].position;
            uv[i] = new Vector2((simulation.vertices[i].position.x + halfWidth) / simulation.Width, (simulation.vertices[i].position.y + halfHeight) / simulation.Height);
        }

        int k = -1;
        for (int i = 0; i < numQuads; i++)
        {
            int j = 4 * i;
            triangles[++k] = simulation.quads[j];
            triangles[++k] = simulation.quads[j + 1];
            triangles[++k] = simulation.quads[j + 2];
            triangles[++k] = simulation.quads[j];
            triangles[++k] = simulation.quads[j + 2];
            triangles[++k] = simulation.quads[j + 3];
        }

        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        GetComponent<MeshFilter>().mesh = mesh; 
    }

    private void UpdateMeshVertices()
    {
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = simulation.vertices[i].position;
        }
        mesh.SetVertices(vertices);
    }
}