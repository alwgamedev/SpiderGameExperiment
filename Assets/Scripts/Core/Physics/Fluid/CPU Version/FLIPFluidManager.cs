using UnityEngine;

public class FLIPFluidManager : MonoBehaviour
{
    [Header("Sim Settings")]
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
    [SerializeField] float pushApartTolerance;
    [SerializeField] int gaussSeidelIterations;
    [SerializeField] float overRelaxation;
    [SerializeField] float flipWeight;
    [SerializeField] float fluidDrag;
    [SerializeField] float fluidDensity;
    [SerializeField] float normalizedFluidDensity;
    [SerializeField] float densitySpringConstant;
    [SerializeField] float agitationPower;
    [SerializeField] float obstacleVelocityNormalizer;

    [Header("Rendering")]
    [SerializeField] float particleScale;
    [SerializeField] Mesh particleMesh;
    [SerializeField] Color particleColor;
    [SerializeField] Shader particleShader;

    FLIPFluidSimulator simulator;

    Material _material;
    int particlePositionsProperty;
    int particleColorProperty;
    Vector4[] particlePositions;

    GraphicsBuffer commandBuffer;
    GraphicsBuffer.IndirectDrawIndexedArgs[] commandData;

    //private void OnDrawGizmos()
    //{
    //    simulator?.DrawParticleGizmos(transform.position);
    //    simulator?.DrawVelocityFieldGizmos(transform.position);
    //}

    private void Start()
    {
        _material = new Material(particleShader);

        particlePositionsProperty = Shader.PropertyToID("_ParticlePositions");
        particleColorProperty = Shader.PropertyToID("_Color");

        var n = numParticles % 2 == 0 ? numParticles / 2 : numParticles / 2 + 1;
        particlePositions = new Vector4[n];

        commandBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);
        commandData = new GraphicsBuffer.IndirectDrawIndexedArgs[1];

        if (particleMesh == null)
        {
            CreateCircleMesh(particleScale * particleRadius, 12);
        }

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
        UpdateMaterialProperties();

        var renderParams = new RenderParams(_material);
        renderParams.worldBounds = new(Vector3.zero, new(10000, 10000, 10000));
        commandData[0].indexCountPerInstance = particleMesh.GetIndexCount(0);
        commandData[0].instanceCount = (uint)simulator.numParticles;
        commandBuffer.SetData(commandData);
        Graphics.RenderMeshIndirect(in renderParams, particleMesh, commandBuffer);
    }

    private void FixedUpdate()
    {
        simulator?.Update(Time.deltaTime, transform.position,
                pushApartIterations, pushApartTolerance, collisionBounciness, flipWeight,
                gaussSeidelIterations, overRelaxation,
                fluidDensity, fluidDrag,
                normalizedFluidDensity, densitySpringConstant, agitationPower, obstacleVelocityNormalizer);
    }

    private void InitializeSimulator()
    {
        simulator = new(width, height, cellSize, numParticles, Physics2D.gravity.y,
            particleRadius, collisionMask);
    }

    private void UpdateMaterialProperties()
    {
        //we could move sim to gpu
        //we have to choose between:
        //a) run sim on cpu and send rendering data (positions, density, etc.) to gpu in late update (like below)
        //b) run sim in compute shader and send obstacle data back and forth (need to read back buoyancy and drag forces)
        //Let's get a) working and then we'll try b).

        int i = 0;
        while (i < simulator.numParticles)
        {
            particlePositions[i / 2] = new(
                transform.position.x + simulator.particlePosition[i].x, transform.position.y + simulator.particlePosition[i++].y, 
                transform.position.x + simulator.particlePosition[i].x, transform.position.y + simulator.particlePosition[i++].y
                );
        }
        _material.SetVectorArray(particlePositionsProperty, particlePositions);
        _material.SetColor(particleColorProperty, particleColor);
    }

    private void CreateCircleMesh(float radius, int numVerts)
    {
        particleMesh = new();
        var vertices = new Vector3[numVerts];
        var uv = new Vector2[numVerts];
        var triangles = new int[3 * (numVerts - 2)];

        float t = 2 * Mathf.PI / numVerts;
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = new(radius * Mathf.Cos(i * t), radius * Mathf.Sin(i * t));
        }

        var n2 = numVerts / 2;
        uv[0] = new(0.5f, 0);
        if (numVerts % 2 == 0)
        {
            var dv = 1f / (n2 - 2);
            for (int j = 1; j < n2; j++)
            {
                var v = (j - 1) * dv;
                uv[j] = new(1f, v);
                uv[numVerts - j] = new(0f, v);
            }
            uv[n2] = new(0.5f, 1f);
        }
        else
        {
            var dv = 1f / (n2 - 1);
            for (int j = 1; j < n2 + 1; j++)
            {
                var v = (j - 1) * dv;
                uv[j] = new(1f, v);
                uv[numVerts - j] = new(0f, v);
            }
        }

        int k = -1;
        for (int i = 2; i < numVerts; i++)
        {
            triangles[++k] = i;
            triangles[++k] = i - 1;
            triangles[++k] = 0;
        }

        particleMesh.vertices = vertices;
        particleMesh.uv = uv;
        particleMesh.triangles = triangles;
        particleMesh.RecalculateNormals();
    }

    private void OnDestroy()
    {
        commandBuffer.Release();
    }
}