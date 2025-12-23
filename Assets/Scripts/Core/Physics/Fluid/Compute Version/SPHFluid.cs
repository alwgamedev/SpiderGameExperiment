using System;
using UnityEngine;

public class SPHFluid : MonoBehaviour
{
    const int threadsPerGroup = 256;

    //compute shader kernel indices
    const int integrateParticles = 0;
    const int handleWallCollisions = 1;
    const int countParticles = 2;
    const int setCellStarts = 3;
    const int sortParticlesByCell = 4;
    const int calculateDensity = 5;
    const int accumulateForces = 6;

    [SerializeField] ComputeShader computeShader;

    [Header("Configuration")]
    [SerializeField] int width;
    [SerializeField] int height;
    [SerializeField] float cellSize;
    [SerializeField] int numParticles;

    [Header("Simulation Settings")]
    //[SerializeField] float particleRadius;
    [SerializeField] float particleMass;
    [SerializeField] float igConstant;
    [SerializeField] float restDensity;
    [SerializeField] float viscosity;
    [SerializeField] float obstacleDrag;
    [SerializeField] float obstacleSpeedNormalizer;
    [SerializeField] LayerMask collisionMask;
    [SerializeField] float collisionBounciness;

    [Header("Rendering")]
    [SerializeField] Mesh particleMesh;
    [SerializeField] Shader particleShader;
    [SerializeField] float particleScale;
    [SerializeField] Color particleColor;

    ComputeBuffer particleDensity;
    ComputeBuffer particleAcceleration;
    ComputeBuffer particleVelocity;
    ComputeBuffer particlePosition;

    ComputeBuffer cellContainingParticle;
    ComputeBuffer particlesByCell;
    ComputeBuffer cellStart;
    ComputeBuffer cellParticleCount;

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
    int igConstantProperty;
    int restDensityProperty;
    int viscosityProperty;
    //int particleRadiusProperty;
    int particleMassProperty;
    int collisionBouncinessProperty;

    int pivotPositionProperty;
    int particleColorProperty;
    int particleScaleProperty;

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
        igConstantProperty = Shader.PropertyToID("igConstant");
        restDensityProperty = Shader.PropertyToID("restDensity");
        viscosityProperty = Shader.PropertyToID("viscosity");
        //particleRadiusProperty = Shader.PropertyToID("particleRadius");
        particleMassProperty = Shader.PropertyToID("particleMass");
        collisionBouncinessProperty = Shader.PropertyToID("collisionBounciness");

        pivotPositionProperty = Shader.PropertyToID("pivotPosition");
        particleColorProperty = Shader.PropertyToID("particleColor");
        particleScaleProperty = Shader.PropertyToID("particleScale");
    }


    //INITIALIZATION

    private void Initialize()
    {
        var numCells = width * height;

        //create buffers
        particleDensity = new ComputeBuffer(numParticles, 4);
        particleAcceleration = new ComputeBuffer(numParticles, 8);
        particleVelocity = new ComputeBuffer(numParticles, 8);
        particlePosition = new ComputeBuffer(numParticles, 8);

        cellContainingParticle = new ComputeBuffer(numParticles, 4);
        particlesByCell = new ComputeBuffer(numParticles, 4);
        cellStart = new ComputeBuffer(numCells + 1, 4);
        cellParticleCount = new ComputeBuffer(numCells, 4);

        //configure compute shader
        computeShader.SetInt("width", width);
        computeShader.SetInt("height", height);
        computeShader.SetInt("numCells", numCells);
        computeShader.SetInt("numParticles", numParticles);

        //bind buffers to compute shader
        computeShader.SetBuffer(particleDensity, "particleDensity", calculateDensity, accumulateForces);
        computeShader.SetBuffer(particleAcceleration, "particleAcceleration", integrateParticles, accumulateForces);
        computeShader.SetBuffer(particleVelocity, "particleVelocity", integrateParticles, handleWallCollisions, accumulateForces);
        computeShader.SetBuffer(particlePosition, "particlePosition", integrateParticles, handleWallCollisions, countParticles, calculateDensity, accumulateForces);
        computeShader.SetBuffer(cellContainingParticle, "cellContainingParticle", countParticles, sortParticlesByCell, calculateDensity, accumulateForces);
        computeShader.SetBuffer(particlesByCell, "particlesByCell", sortParticlesByCell, calculateDensity, accumulateForces);
        computeShader.SetBuffer(cellStart, "cellStart", setCellStarts, sortParticlesByCell, calculateDensity, accumulateForces);
        computeShader.SetBuffer(cellParticleCount, "cellParticleCount", countParticles, setCellStarts, sortParticlesByCell);

        numParticleThreadGroups = (int)Mathf.Ceil((float)numParticles / threadsPerGroup);

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
        particleAcceleration.Release();
        particleVelocity.Release();
        particlePosition.Release();

        cellContainingParticle.Release();
        particlesByCell.Release();
        cellStart.Release();
        cellParticleCount.Release();

        commandBuffer.Release();
    }

    private void InitializeParticlePhysics()
    {
        var initialPositions = new Vector2[numParticles];

        particleAcceleration.SetData(initialPositions);
        particleVelocity.SetData(initialPositions);

        var spacing = 0.2f * cellSize;
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
            UpdateSimSettings();
        }

        computeShader.Dispatch(integrateParticles, numParticleThreadGroups, 1, 1);
        computeShader.Dispatch(handleWallCollisions, numParticleThreadGroups, 1, 1);
        computeShader.Dispatch(countParticles, numParticleThreadGroups, 1, 1);
        computeShader.Dispatch(setCellStarts, 1, 1, 1);
        computeShader.Dispatch(sortParticlesByCell, numParticleThreadGroups, 1, 1);
        computeShader.Dispatch(calculateDensity, numParticleThreadGroups, 1, 1);
        computeShader.Dispatch(accumulateForces, numParticleThreadGroups, 1, 1);
    }

    //maybe put settings into a single struct so we only have to do one set
    private void UpdateSimSettings()
    {
        computeShader.SetFloat(dtProperty, Time.deltaTime);
        computeShader.SetFloat(cellSizeProperty, cellSize);
        computeShader.SetFloat(worldWidthProperty, width * cellSize);
        computeShader.SetFloat(worldHeightProperty, height * cellSize);
        computeShader.SetFloat(smoothingRadiusProperty, 0.5f * cellSize);
        computeShader.SetFloat(smoothingRadiusSqrdProperty, 0.25f * cellSize * cellSize);
        computeShader.SetFloat(gravityProperty, Physics2D.gravity.y);
        computeShader.SetFloat(igConstantProperty, igConstant);
        computeShader.SetFloat(restDensityProperty, restDensity);
        computeShader.SetFloat(viscosityProperty, viscosity);
        computeShader.SetFloat(particleMassProperty, particleMass);
        computeShader.SetFloat(collisionBouncinessProperty, collisionBounciness);

        updateSimSettings = false;
    }


    //RENDERING

    private void LateUpdate()
    {
        if (updateMaterialProperties)
        {
            UpdateMaterialProperties();
        }

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
        material.SetVector(pivotPositionProperty, transform.position);
        material.SetColor(particleColorProperty, particleColor);
        material.SetFloat(particleScaleProperty, particleScale);
        material.SetFloat(restDensityProperty, restDensity);

        updateMaterialProperties = false;
    }

    private void CreateBoxMesh()//we can make it look like a circle in shader
    {
        particleMesh = new();
        var vertices = new Vector3[]
        {
            new(-0.5f, -0.5f),
            new(-0.5f, 0.5f),
            new(0.5f, 0.5f),
            new(0.5f, -0.5f)
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
