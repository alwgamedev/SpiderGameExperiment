using System;
using UnityEngine;

public class PBFluid : MonoBehaviour
{
    const int THREADS_PER_GROUP = 256;
    const int MAX_NUM_OBSTACLES = 64;

    //kernel indices in compute shader
    const int recalculateAntiClusterCoefficient = 0;
    const int computePredictedPositions = 1;
    const int countParticles = 2;
    const int setCellStarts = 3;
    const int sortParticlesByCell = 4;
    const int calculateDensity = 5;
    const int calculateLambda = 6;
    const int calculatePositionDelta = 7;
    const int addPositionDelta = 8;
    const int storeSolvedVelocity = 9;
    const int calculateVorticityConfinementForce = 10;
    const int applyVorticityConfinmentForce = 11;
    const int integrateParticles = 12;
    const int handleWallCollisions = 13;

    [SerializeField] ComputeShader computeShader;

    [Header("Configuration")]
    [SerializeField] int releaseWidth;
    [SerializeField] int width;
    [SerializeField] int height;
    [SerializeField] float cellSize;
    [SerializeField] int numParticles;

    [Header("Simulation Settings")]
    [SerializeField] int updateFrequency;
    [SerializeField] int stepsPerUpdate;
    [SerializeField] int pressureSolveIterations;
    [SerializeField] int kernelDeg;
    [SerializeField] int densityKernelDeg;
    [SerializeField] float restDensity;
    [SerializeField] float vorticityConfinement;
    [SerializeField] float viscosity;
    [SerializeField] float antiClusterK;
    [SerializeField] float antiClusterDQ;
    [SerializeField] int antiClusterN;
    [SerializeField] float epsilon;//stabilizer
    [SerializeField] float collisionBounciness;
    [SerializeField] LayerMask obstacleMask;
    [SerializeField] float obstacleRepulsion;
    [SerializeField] float velocityBasedObstacleRepulsionMultiplier;
    [SerializeField] float velocityBasedObstacleScaleMultiplier;
    [SerializeField] float obstacleUpscaleMax;
    [SerializeField] float drag;

    [Header("Rendering")]
    [SerializeField] Mesh particleMesh;
    [SerializeField] Shader particleShader;
    [SerializeField] Color particleColorMin;
    [SerializeField] Color particleColorMax;
    [SerializeField] float particleRadiusMin;
    [SerializeField] float particleRadiusMax;
    [SerializeField] float densityNormalizer;

    ComputeBuffer velocity;
    ComputeBuffer position;
    ComputeBuffer predictedPosition;
    ComputeBuffer deltaPosition;
    ComputeBuffer density;
    ComputeBuffer lambda;

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

    int updateCounter;
    bool updateSimSettings;
    bool updateMaterialProperties;

    int kernelDegProperty;
    int densityKernelDegProperty;
    int dtProperty;
    int dtInverseProperty;
    int cellSizeProperty;
    int worldWidthProperty;
    int worldHeightProperty;
    int smoothingRadiusProperty;
    int smoothingRadiusSqrdProperty;
    int gravityProperty;
    int restDensityProperty;
    int vorticityConfinementProperty;
    int viscosityProperty;
    int antiClusterKProperty;
    int antiClusterDQProperty;
    int antiClusterNProperty;
    int epsilonProperty;
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
        kernelDegProperty = Shader.PropertyToID("kernelDeg");
        densityKernelDegProperty = Shader.PropertyToID("densityKernelDeg");
        dtProperty = Shader.PropertyToID("dt");
        dtInverseProperty = Shader.PropertyToID("dtInverse");
        cellSizeProperty = Shader.PropertyToID("cellSize");
        worldWidthProperty = Shader.PropertyToID("worldWidth");
        worldHeightProperty = Shader.PropertyToID("worldHeight");
        smoothingRadiusProperty = Shader.PropertyToID("smoothingRadius");
        smoothingRadiusSqrdProperty = Shader.PropertyToID("smoothingRadiusSqrd");
        gravityProperty = Shader.PropertyToID("gravity");
        restDensityProperty = Shader.PropertyToID("restDensity");
        vorticityConfinementProperty = Shader.PropertyToID("vorticityConfinement");
        viscosityProperty = Shader.PropertyToID("viscosity");
        antiClusterKProperty = Shader.PropertyToID("antiClusterK");
        antiClusterDQProperty = Shader.PropertyToID("antiClusterDQ");
        antiClusterNProperty = Shader.PropertyToID("antiClusterN");
        epsilonProperty = Shader.PropertyToID("epsilon");
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
        velocity = new ComputeBuffer(numParticles, 8);
        position = new ComputeBuffer(numParticles, 8);
        predictedPosition = new ComputeBuffer(numParticles, 8);
        deltaPosition = new ComputeBuffer(numParticles, 8);
        density = new ComputeBuffer(numParticles, 4);
        lambda = new ComputeBuffer(numParticles, 4);

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
        computeShader.SetBuffer(density, "density", calculateDensity, calculateLambda, calculateVorticityConfinementForce);
        computeShader.SetBuffer(lambda, "lambda", calculateLambda, calculatePositionDelta);
        computeShader.SetBuffer(velocity, "velocity", computePredictedPositions, storeSolvedVelocity, calculateVorticityConfinementForce, applyVorticityConfinmentForce, 
            integrateParticles, handleWallCollisions);
        computeShader.SetBuffer(position, "position", computePredictedPositions, storeSolvedVelocity, integrateParticles, handleWallCollisions);
        computeShader.SetBuffer(predictedPosition, "predictedPosition", computePredictedPositions, countParticles, 
            calculateDensity, calculateLambda, addPositionDelta, calculatePositionDelta, storeSolvedVelocity, calculateVorticityConfinementForce, integrateParticles);
        computeShader.SetBuffer(deltaPosition, "deltaPosition", calculatePositionDelta, addPositionDelta, calculateVorticityConfinementForce, applyVorticityConfinmentForce);
        computeShader.SetBuffer(cellContainingParticle, "cellContainingParticle", countParticles, sortParticlesByCell, calculateDensity, 
            calculateLambda, calculatePositionDelta, calculateVorticityConfinementForce);
        computeShader.SetBuffer(particlesByCell, "particlesByCell", sortParticlesByCell, calculateDensity, calculateLambda, calculatePositionDelta, calculateVorticityConfinementForce);
        computeShader.SetBuffer(cellStart, "cellStart", setCellStarts, sortParticlesByCell, calculateDensity, calculateLambda, calculatePositionDelta, calculateVorticityConfinementForce);
        computeShader.SetBuffer(cellParticleCount, "cellParticleCount", countParticles, setCellStarts, sortParticlesByCell);
        computeShader.SetBuffer(obstacleData, "obstacleData", integrateParticles);

        numParticleThreadGroups = (int)Mathf.Ceil((float)numParticles / THREADS_PER_GROUP);

        //set up material
        material = new Material(particleShader);

        commandBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);
        commandData = new GraphicsBuffer.IndirectDrawIndexedArgs[1];

        //bind buffers to material
        material.SetBuffer("particleDensity", density);
        material.SetBuffer("particleVelocity", velocity);
        material.SetBuffer("particlePosition", position);

        if (particleMesh == null)
        {
            CreateBoxMesh();
        }

        InitializeParticlePhysics();
        cellParticleCount.SetData(new uint[numCells]);//to set all to 0

        updateSimSettings = true;
        updateMaterialProperties = true;
    }

    private void OnEnable()
    {
        Initialize();
    }

    private void OnDisable()
    {
        density.Release();
        lambda.Release();
        velocity.Release();
        position.Release();
        predictedPosition.Release();
        deltaPosition.Release();

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

        velocity.SetData(initialPositions);
        predictedPosition.SetData(initialPositions);

        var spacing = particleRadiusMax;
        var x0 = cellSize + 0.5f * spacing;
        var x = x0;
        var y = cellSize * (height - 1) - 0.5f * spacing;
        var xmax = cellSize * Mathf.Min(releaseWidth, width - 1) - 0.5f * spacing;
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

        position.SetData(initialPositions);
    }


    //SIM

    private void FixedUpdate()
    {
        if (updateSimSettings)
        {
            var dt = updateFrequency * Time.deltaTime / stepsPerUpdate;
            UpdateSimSettings(dt);
        }

        if (--updateCounter < 0)
        {
            updateCounter += updateFrequency;
            SetObstacleData();

            for (int i = 0; i < stepsPerUpdate; i++)
            {
                RunSimulationStep();
            }
        }
    }

    private void RunSimulationStep()
    {
        computeShader.Dispatch(computePredictedPositions, numParticleThreadGroups, 1, 1);

        for (int i = 0; i < pressureSolveIterations; i++)
        {
            computeShader.Dispatch(countParticles, numParticleThreadGroups, 1, 1);
            computeShader.Dispatch(setCellStarts, 1, 1, 1);
            computeShader.Dispatch(sortParticlesByCell, numParticleThreadGroups, 1, 1);
            computeShader.Dispatch(calculateDensity, numParticleThreadGroups, 1, 1);

            computeShader.Dispatch(calculateLambda, numParticleThreadGroups, 1, 1);
            computeShader.Dispatch(calculatePositionDelta, numParticleThreadGroups, 1, 1);
            computeShader.Dispatch(addPositionDelta, numParticleThreadGroups, 1, 1);
        }

        computeShader.Dispatch(storeSolvedVelocity, numParticleThreadGroups, 1, 1);
        computeShader.Dispatch(calculateVorticityConfinementForce, numParticleThreadGroups, 1, 1);
        computeShader.Dispatch(applyVorticityConfinmentForce, numParticleThreadGroups, 1, 1);
        computeShader.Dispatch(integrateParticles, numParticleThreadGroups, 1, 1);
        computeShader.Dispatch(handleWallCollisions, numParticleThreadGroups, 1, 1);
    }

    //maybe put settings into a single struct so we only have to do one set
    private void UpdateSimSettings(float dt)
    {
        computeShader.SetInt(kernelDegProperty, kernelDeg);
        computeShader.SetInt(densityKernelDegProperty, densityKernelDeg);
        computeShader.SetFloat(dtProperty, dt);
        computeShader.SetFloat(dtInverseProperty, 1 / dt);
        computeShader.SetFloat(cellSizeProperty, cellSize);
        computeShader.SetFloat(worldWidthProperty, width * cellSize);
        computeShader.SetFloat(worldHeightProperty, height * cellSize);
        computeShader.SetFloat(smoothingRadiusProperty, 0.5f * cellSize);
        computeShader.SetFloat(smoothingRadiusSqrdProperty, 0.25f * cellSize * cellSize);
        computeShader.SetVector(gravityProperty, Physics2D.gravity);
        computeShader.SetFloat(antiClusterKProperty, antiClusterK);
        computeShader.SetFloat(antiClusterDQProperty, antiClusterDQ);
        computeShader.SetInt(antiClusterNProperty, antiClusterN);
        computeShader.SetFloat(epsilonProperty, epsilon);
        computeShader.SetFloat(restDensityProperty, restDensity);
        computeShader.SetFloat(vorticityConfinementProperty, vorticityConfinement);
        computeShader.SetFloat(viscosityProperty, viscosity);
        computeShader.SetFloat(collisionBouncinessProperty, collisionBounciness);
        computeShader.SetFloat(obstacleRepulsionProperty, obstacleRepulsion);

        computeShader.Dispatch(recalculateAntiClusterCoefficient, 1, 1, 1);

        updateSimSettings = false;
    }

    //this is for dynamic obstacles moving through the fluid
    //we'll handle static colliders like ground differently
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
            if (o && o.attachedRigidbody)
            {
                var s2 = o.attachedRigidbody.linearVelocity.magnitude;
                if (s2 != 0)
                {
                    //we'll pretend like the obstacle is a circle with radius the maximum of its bounding box extents.
                    //the repulsive force and the object size get scaled with the obstacle's speed, so fluid can flow by motionless obstacles (useful for 2D)
                    //and will be agitated more by faster moving obstacles.
                    var tScale = Mathf.Min(velocityBasedObstacleScaleMultiplier * s2, obstacleUpscaleMax);
                    var tRepulsion = Mathf.Min(velocityBasedObstacleRepulsionMultiplier * s2, 1);
                    var r = Mathf.Max(o.bounds.extents.x, o.bounds.extents.y);
                    obstacleDataToTransfer[numObstacles++] = new Vector4(o.bounds.center.x - transform.position.x, o.bounds.center.y - transform.position.y, tScale * r, tRepulsion);
                }
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
        material.SetFloat(restDensityProperty, restDensity);
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
