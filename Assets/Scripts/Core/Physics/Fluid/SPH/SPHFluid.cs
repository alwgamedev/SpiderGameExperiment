using System;
using UnityEngine;

public class SPHFluid : MonoBehaviour
{
    const int THREADS_PER_GROUP = 256;
    const int MAX_NUM_OBSTACLES = 64;

    //kernel indices in compute shader
    const int computePredictedPositions = 0;
    const int countParticles = 1;
    const int setCellStarts = 2;
    const int sortParticlesByCell = 3;
    const int calculateDensity = 4;
    const int accumulateForces = 5;
    const int integrateParticles = 6;
    const int handleWallCollisions = 7;

    [SerializeField] ComputeShader computeShader;

    [Header("Configuration")]
    [SerializeField] int width;
    [SerializeField] int height;
    [SerializeField] float cellSize;
    [SerializeField] int numParticles;

    [Header("Simulation Settings")]
    [SerializeField] int stepsPerFrame;
    [SerializeField] float stiffnessCoefficient;
    [SerializeField] float nearStiffnessCoefficient;
    [SerializeField] float restDensity;
    [SerializeField] float viscosity;
    [SerializeField] float collisionBounciness;
    [SerializeField] LayerMask obstacleMask;
    [SerializeField] float obstacleRepulsion;
    [SerializeField] float drag;

    [Header("Rendering")]
    [SerializeField] Mesh particleMesh;
    [SerializeField] Shader particleShader;
    [SerializeField] Color particleColorMin;
    [SerializeField] Color particleColorMax;
    [SerializeField] float particleRadiusMin;
    [SerializeField] float particleRadiusMax;
    [SerializeField] float densityNormalizer;

    ComputeBuffer particleDensity;
    ComputeBuffer nearDensity;
    ComputeBuffer particleAcceleration;
    ComputeBuffer particleVelocity;
    ComputeBuffer particlePosition;
    ComputeBuffer predictedPosition;

    ComputeBuffer cellContainingParticle;
    ComputeBuffer particlesByCell;
    ComputeBuffer cellStart;
    ComputeBuffer cellParticleCount;

    ComputeBuffer obstacleData;
    Vector4[] obstacleDataToTransfer;
    Collider2D[] obstacleColliders;
    ContactFilter2D obstacleFilter;

    Material material;
    GraphicsBuffer commandBuffer;
    GraphicsBuffer.IndirectDrawIndexedArgs[] commandData;

    bool updateSimSettings;
    bool updateMaterialProperties;

    int dtProperty;
    int cellSizeProperty;
    int worldWidthProperty;
    int worldHeightProperty;
    int smoothingRadiusProperty;
    int smoothingRadiusSqrdProperty;
    int gravityProperty;
    int stiffnessCoefficientProperty;
    int nearStiffnessCoefficientProperty;
    int restDensityProperty;
    int viscosityProperty;
    int collisionBouncinessProperty;
    int obstacleRepulsionProperty;
    int numObstaclesProperty;

    int pivotPositionProperty;
    int particleColorMinProperty;
    int particleColorMaxProperty;
    int particleRadiusMinProperty;
    int particleRadiusMaxProperty;
    int densityNormalizerProperty;

    int numParticleThreadGroups;

    private void OnValidate()
    {
        updateSimSettings = true;
        updateMaterialProperties = true;
    }

    private void Awake()
    {
        dtProperty = Shader.PropertyToID("dt");
        cellSizeProperty = Shader.PropertyToID("cellSize");
        worldWidthProperty = Shader.PropertyToID("worldWidth");
        worldHeightProperty = Shader.PropertyToID("worldHeight");
        smoothingRadiusProperty = Shader.PropertyToID("smoothingRadius");
        smoothingRadiusSqrdProperty = Shader.PropertyToID("smoothingRadiusSqrd");
        gravityProperty = Shader.PropertyToID("gravity");
        stiffnessCoefficientProperty = Shader.PropertyToID("stiffnessCoefficient");
        nearStiffnessCoefficientProperty = Shader.PropertyToID("nearStiffnessCoefficient");
        restDensityProperty = Shader.PropertyToID("restDensity");
        viscosityProperty = Shader.PropertyToID("viscosity");
        collisionBouncinessProperty = Shader.PropertyToID("collisionBounciness");
        obstacleRepulsionProperty = Shader.PropertyToID("obstacleRepulsion");
        numObstaclesProperty = Shader.PropertyToID("numObstacles");

        pivotPositionProperty = Shader.PropertyToID("pivotPosition");
        particleColorMinProperty = Shader.PropertyToID("particleColorMin");
        particleColorMaxProperty = Shader.PropertyToID("particleColorMax");
        particleRadiusMinProperty = Shader.PropertyToID("particleRadiusMin");
        particleRadiusMaxProperty = Shader.PropertyToID("particleRadiusMax");
        densityNormalizerProperty = Shader.PropertyToID("densityNormalizer");
    }


    //INITIALIZATION

    private void Initialize()
    {
        var numCells = width * height;

        //create buffers
        particleDensity = new ComputeBuffer(numParticles, 4);
        nearDensity = new ComputeBuffer(numParticles, 4);
        particleAcceleration = new ComputeBuffer(numParticles, 8);
        particleVelocity = new ComputeBuffer(numParticles, 8);
        particlePosition = new ComputeBuffer(numParticles, 8);
        predictedPosition = new ComputeBuffer(numParticles, 8);

        cellContainingParticle = new ComputeBuffer(numParticles, 4);
        particlesByCell = new ComputeBuffer(numParticles, 4);
        cellStart = new ComputeBuffer(numCells + 1, 4);
        cellParticleCount = new ComputeBuffer(numCells, 4);

        obstacleData = new ComputeBuffer(MAX_NUM_OBSTACLES, 16, ComputeBufferType.Default, ComputeBufferMode.SubUpdates);
        obstacleDataToTransfer = new Vector4[MAX_NUM_OBSTACLES];
        obstacleColliders = new Collider2D[MAX_NUM_OBSTACLES];

        obstacleFilter = ContactFilter2D.noFilter;
        obstacleFilter.useTriggers = false;
        obstacleFilter.SetLayerMask(obstacleMask);

        //configure compute shader
        computeShader.SetInt("width", width);
        computeShader.SetInt("height", height);
        computeShader.SetInt("numCells", numCells);
        computeShader.SetInt("numParticles", numParticles);

        //bind buffers to compute shader
        computeShader.SetBuffer(particleDensity, "particleDensity", calculateDensity, accumulateForces);
        computeShader.SetBuffer(nearDensity, "nearDensity", calculateDensity, accumulateForces);
        computeShader.SetBuffer(particleAcceleration, "particleAcceleration", integrateParticles, accumulateForces);
        computeShader.SetBuffer(particleVelocity, "particleVelocity", computePredictedPositions, integrateParticles, handleWallCollisions, accumulateForces);
        computeShader.SetBuffer(particlePosition, "particlePosition", computePredictedPositions, integrateParticles, handleWallCollisions);
        computeShader.SetBuffer(predictedPosition, "predictedPosition", computePredictedPositions, countParticles, calculateDensity, accumulateForces);
        computeShader.SetBuffer(cellContainingParticle, "cellContainingParticle", countParticles, sortParticlesByCell, calculateDensity, accumulateForces);
        computeShader.SetBuffer(particlesByCell, "particlesByCell", sortParticlesByCell, calculateDensity, accumulateForces);
        computeShader.SetBuffer(cellStart, "cellStart", setCellStarts, sortParticlesByCell, calculateDensity, accumulateForces);
        computeShader.SetBuffer(cellParticleCount, "cellParticleCount", countParticles, setCellStarts, sortParticlesByCell);
        computeShader.SetBuffer(obstacleData, "obstacleData", computePredictedPositions);

        numParticleThreadGroups = (int)Mathf.Ceil((float)numParticles / THREADS_PER_GROUP);

        //set up material
        material = new Material(particleShader);

        commandBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);
        commandData = new GraphicsBuffer.IndirectDrawIndexedArgs[1];

        //bind buffers to material
        material.SetBuffer("particleDensity", particleDensity);
        material.SetBuffer("particleVelocity", particleVelocity);
        material.SetBuffer("particlePosition", particlePosition);

        if (particleMesh == null)
        {
            CreateBoxMesh();
        }

        InitializeParticlePhysics();
        cellParticleCount.SetData(new uint[numCells]);

        updateSimSettings = true;
        updateMaterialProperties = true;
    }

    private void OnEnable()
    {
        Initialize();
    }

    private void OnDisable()
    {
        particleDensity.Release();
        nearDensity.Release();
        particleAcceleration.Release();
        particleVelocity.Release();
        particlePosition.Release();
        predictedPosition.Release();

        cellContainingParticle.Release();
        particlesByCell.Release();
        cellStart.Release();
        cellParticleCount.Release();

        obstacleData.Release();

        commandBuffer.Release();
    }

    private void InitializeParticlePhysics()
    {
        var initialPositions = new Vector2[numParticles];

        particleAcceleration.SetData(initialPositions);
        particleVelocity.SetData(initialPositions);
        predictedPosition.SetData(initialPositions);

        var spacing = 0.1f * cellSize;
        var x0 = cellSize + 0.5f * spacing;
        var x = x0;
        var y = cellSize * (height - 1) - 0.5f * spacing;
        var xmax = cellSize * (width - 1) - 0.5f * spacing;
        var ymin = cellSize + 0.5f * spacing;

        for (int k = 0; k < numParticles; k++)
        {
            x += spacing;
            if (x > xmax)
            {
                x = x0;
                if (y > ymin)
                {
                    y -= spacing;
                }
            }

            initialPositions[k] = new(x, y);
        }

        particlePosition.SetData(initialPositions);
    }


    //SIM

    private void FixedUpdate()
    {
        if (updateSimSettings)
        {
            var dt = Time.deltaTime / stepsPerFrame;
            UpdateSimSettings(dt);
        }

        SetObstacleData();

        for (int i = 0; i < stepsPerFrame; i++)
        {
            RunSimulationStep();
        }
    }

    private void RunSimulationStep()
    {
        computeShader.Dispatch(computePredictedPositions, numParticleThreadGroups, 1, 1);
        computeShader.Dispatch(countParticles, numParticleThreadGroups, 1, 1);
        computeShader.Dispatch(setCellStarts, 1, 1, 1);
        computeShader.Dispatch(sortParticlesByCell, numParticleThreadGroups, 1, 1);
        computeShader.Dispatch(calculateDensity, numParticleThreadGroups, 1, 1);
        computeShader.Dispatch(accumulateForces, numParticleThreadGroups, 1, 1);
        computeShader.Dispatch(integrateParticles, numParticleThreadGroups, 1, 1);
        computeShader.Dispatch(handleWallCollisions, numParticleThreadGroups, 1, 1);
    }

    //maybe put settings into a single struct so we only have to do one set
    private void UpdateSimSettings(float dt)
    {
        computeShader.SetFloat(dtProperty, dt);
        computeShader.SetFloat(cellSizeProperty, cellSize);
        computeShader.SetFloat(worldWidthProperty, width * cellSize);
        computeShader.SetFloat(worldHeightProperty, height * cellSize);
        computeShader.SetFloat(smoothingRadiusProperty, 0.5f * cellSize);
        computeShader.SetFloat(smoothingRadiusSqrdProperty, 0.25f * cellSize * cellSize);
        computeShader.SetVector(gravityProperty, Physics2D.gravity);
        computeShader.SetFloat(stiffnessCoefficientProperty, stiffnessCoefficient);
        computeShader.SetFloat(nearStiffnessCoefficientProperty, nearStiffnessCoefficient);
        computeShader.SetFloat(restDensityProperty, restDensity);
        computeShader.SetFloat(viscosityProperty, viscosity);
        computeShader.SetFloat(collisionBouncinessProperty, collisionBounciness);
        computeShader.SetFloat(obstacleRepulsionProperty, obstacleRepulsion);

        updateSimSettings = false;
    }

    private void SetObstacleData()
    {
        var worldWidth = width * cellSize;
        var worldHeight = height * cellSize;
        var boxCenter = new Vector2(transform.position.x + 0.5f * worldWidth, transform.position.y + 0.5f * worldHeight);
        var boxSize = new Vector2(worldWidth, worldHeight);
        Array.Clear(obstacleColliders, 0, obstacleColliders.Length);
        Physics2D.OverlapBox(boxCenter, boxSize, 0, obstacleFilter, obstacleColliders);

        //put non-null obstacles at the beginning of the compute buffer
        //and we'll use numObstacles to mark the end so we don't have to clear out the rest of the buffer
        var numObstacles = 0;
        for (int i = 0; i < MAX_NUM_OBSTACLES; i++)
        {
            var o = obstacleColliders[i];
            if (o)
            {
                var r = Mathf.Max(o.bounds.extents.x, o.bounds.extents.y) + particleRadiusMax;
                obstacleDataToTransfer[numObstacles++] = new Vector4(o.bounds.center.x - transform.position.x, o.bounds.center.y - transform.position.y, r, r);
                    //o.bounds.extents.x + particleRadiusMax, o.bounds.extents.y + particleRadiusMax);
            }
        }

        computeShader.SetInt(numObstaclesProperty, numObstacles);
        obstacleData.SetData(obstacleDataToTransfer);
    }


    //RENDERING

    private void LateUpdate()
    {
        if (updateMaterialProperties)
        {
            UpdateMaterialProperties();
        }

        material.SetVector(pivotPositionProperty, transform.position);

        var renderParams = new RenderParams(material)
        {
            worldBounds = new(Vector3.zero, new(10000, 10000, 10000))//better options?
        };
        commandData[0].indexCountPerInstance = particleMesh.GetIndexCount(0);
        commandData[0].instanceCount = (uint)numParticles;
        commandBuffer.SetData(commandData);
        Graphics.RenderMeshIndirect(in renderParams, particleMesh, commandBuffer);
        //there's also RenderPrimitives and RenderMeshPrimitives?
        //you should test if there's a significant difference in performance for us
    }

    private void UpdateMaterialProperties()
    {
        material.SetColor(particleColorMinProperty, particleColorMin);
        material.SetColor(particleColorMaxProperty, particleColorMax);
        material.SetFloat(particleRadiusMinProperty, particleRadiusMin);
        material.SetFloat(particleRadiusMaxProperty, particleRadiusMax);
        material.SetFloat(restDensityProperty, restDensity);//isn't used atm
        material.SetFloat(densityNormalizerProperty, densityNormalizer);

        updateMaterialProperties = false;
    }

    private void CreateBoxMesh()//we can make it look like a circle in shader
    {
        particleMesh = new();
        var vertices = new Vector3[]
        {
            new(-1f, -1f),
            new(-1f, 1f),
            new(1f, 1f),
            new(1f, -1f)
        };
        var uv = new Vector2[]
        {
            new(0,0), new(0,1), new(1,1), new(1,0)
        };
        var triangles = new int[]
        {
            0, 1, 2, 2, 3, 0
        };

        particleMesh.vertices = vertices;
        particleMesh.uv = uv;
        particleMesh.triangles = triangles;
        particleMesh.RecalculateNormals();
    }
}
