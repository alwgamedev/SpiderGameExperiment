using System;
using UnityEngine;

public class PBFluid : MonoBehaviour
{
    const int THREADS_PER_GROUP = 256;
    const int MAX_NUM_OBSTACLES = 64;

    //kernel indices in compute shader
    public const int recalculateAntiClusterCoefficient = 0;
    public const int computePredictedPositions = 1;
    public const int countParticles = 2;
    public const int setCellStarts = 3;
    public const int sortParticlesByCell = 4;
    public const int calculateDensity = 5;
    public const int calculateLambda = 6;
    public const int calculatePositionDelta = 7;
    public const int addPositionDelta = 8;
    public const int storeSolvedVelocity = 9;
    public const int calculateVorticityConfinementForce = 10;
    public const int integrateParticles = 11;
    public const int handleWallCollisions = 12;
    public const int scrollNoise = 13;
    public const int updateDensityTexture = 14;
    public const int spawnFoam = 15;
    public const int updateFoam = 16;
    public const int transferFoamSurvivorsToBuffer = 17;
    public const int transferFoamSurvivorsBack = 18;
    public const int completeFoamUpdate = 19;

    [SerializeField] ComputeShader _computeShader;

    public PBFConfiguration configuration;
    public PBFSimSettings simSettings;
    public PBFFoamParticleSettings foamParticleSettings;
    public PBFDensityTexSettings densityTexSettings;

    [HideInInspector] public ComputeShader computeShader;
    [HideInInspector] public RenderTexture densityTexture;
    [HideInInspector] public RenderTexture velocityTexture;

    PBFComputeConfig[] configTransfer;
    PBFComputeVariables[] varsTransfer;
    ComputeBuffer configBuffer;
    ComputeBuffer varsBuffer;

    public ComputeBuffer velocity;
    public ComputeBuffer position;
    public ComputeBuffer lastPosition;
    public ComputeBuffer density;
    ComputeBuffer particleBuffer;
    ComputeBuffer lambda;

    ComputeBuffer cellContainingParticle;
    ComputeBuffer particlesByCell;
    ComputeBuffer cellStart;
    ComputeBuffer cellParticleCount;

    ComputeBuffer obstacleData;
    Vector4[] obstacleDataToTransfer;
    Collider2D[] obstacleColliders;
    ContactFilter2D obstacleFilter;

    public ComputeBuffer foamParticleCounter;
    public ComputeBuffer foamParticle;
    ComputeBuffer foamParticleBuffer;

    ComputeBuffer noise;

    int updateCounter;
    bool updateSimSettings;

    int numObstaclesProperty;

    int numParticleThreadGroups;
    int numFoamParticleThreadGroups;
    int texThreadGroupsX;
    int texThreadGroupsY;

    public event Action Initialized;
    public event Action SettingsChanged;

    private void OnDrawGizmos()
    {
        if (configuration.drawGizmos)
        {
            //to visualize the grid bounds
            Gizmos.color = Color.yellow;
            float w = configuration.width * simSettings.cellSize;
            float h = configuration.height * simSettings.cellSize;
            var p = transform.position;
            var q = transform.position + h * Vector3.up;
            Gizmos.DrawLine(p, q);
            p = q;
            q += w * Vector3.right;
            Gizmos.DrawLine(p, q);
            p = q;
            q -= h * Vector3.up;
            Gizmos.DrawLine(p, q);
            p = q;
            q -= w * Vector3.right;
            Gizmos.DrawLine(p, q);
        }
    }

    private void OnValidate()
    {
        updateSimSettings = true;
    }

    private void Awake()
    {
        computeShader = Instantiate(_computeShader);

        numObstaclesProperty = Shader.PropertyToID("numObstacles");
    }


    //INITIALIZATION

    private void Initialize()
    {
        var numCells = configuration.width * configuration.height;

        //create buffers
        configTransfer = new PBFComputeConfig[1];
        varsTransfer = new PBFComputeVariables[1];
        configBuffer = new ComputeBuffer(1, MiscTools.Stride<PBFComputeConfig>());
        varsBuffer = new ComputeBuffer(1, MiscTools.Stride<PBFComputeVariables>());

        velocity = new ComputeBuffer(configuration.numParticles, 8);
        position = new ComputeBuffer(configuration.numParticles, 8);
        lastPosition = new ComputeBuffer(configuration.numParticles, 8);
        particleBuffer = new ComputeBuffer(configuration.numParticles, 8);
        density = new ComputeBuffer(configuration.numParticles, 4);
        lambda = new ComputeBuffer(configuration.numParticles, 4);

        cellContainingParticle = new ComputeBuffer(configuration.numParticles, 4);
        particlesByCell = new ComputeBuffer(configuration.numParticles, 4);
        cellStart = new ComputeBuffer(numCells + 1, 4);
        cellParticleCount = new ComputeBuffer(numCells, 4);

        obstacleData = new ComputeBuffer(MAX_NUM_OBSTACLES, 16, ComputeBufferType.Default, ComputeBufferMode.SubUpdates);
        obstacleDataToTransfer = new Vector4[MAX_NUM_OBSTACLES];
        obstacleColliders = new Collider2D[MAX_NUM_OBSTACLES];

        obstacleFilter = ContactFilter2D.noFilter;
        obstacleFilter.useTriggers = false;
        obstacleFilter.SetLayerMask(simSettings.obstacleMask);

        foamParticle = new ComputeBuffer(configuration.numFoamParticles, 32);
        foamParticleBuffer = new ComputeBuffer(configuration.numFoamParticles, 32);
        foamParticleCounter = new ComputeBuffer(2, 4);
        foamParticleCounter.SetData(new int[2]);

        //configure compute shader
        configTransfer[0] = new(configuration);
        configBuffer.SetData(configTransfer);

        //generate particle noise values
        var noiseData = new Vector2[configuration.numParticles];
        for (int i = 0; i < noiseData.Length; i++)
        {
            noiseData[i] = new(MathTools.RandomFloat(0f, 1f), MathTools.RandomFloat(0.25f, 1.75f));
        }
        noise = new ComputeBuffer(noiseData.Length, 8);
        noise.SetData(noiseData);

        //configure density texture
        densityTexture = new(densityTexSettings.texWidth, densityTexSettings.texHeight, 0)
        {
            graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat,
            dimension = UnityEngine.Rendering.TextureDimension.Tex2D,
            enableRandomWrite = true
        };

        velocityTexture = new(densityTexSettings.texWidth, densityTexSettings.texHeight, 0)
        {
            graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16_SFloat,
            dimension = UnityEngine.Rendering.TextureDimension.Tex2D,
            enableRandomWrite = true
        };

        texThreadGroupsX = (int)Mathf.Ceil(densityTexSettings.texWidth / 16f);
        texThreadGroupsY = (int)Mathf.Ceil(densityTexSettings.texHeight / 16f);

        //bind buffers to compute shader
        computeShader.SetBuffer(configBuffer, "configBuffer", computePredictedPositions, countParticles, setCellStarts, sortParticlesByCell, calculateDensity, 
            calculateLambda, calculatePositionDelta, addPositionDelta, storeSolvedVelocity, calculateVorticityConfinementForce, integrateParticles, handleWallCollisions, scrollNoise, 
            updateDensityTexture, spawnFoam, updateFoam);
        computeShader.SetBuffer(varsBuffer, "varsBuffer", recalculateAntiClusterCoefficient, computePredictedPositions, countParticles, calculateDensity, 
            calculateLambda, calculatePositionDelta, storeSolvedVelocity, calculateVorticityConfinementForce, integrateParticles, handleWallCollisions, scrollNoise, updateDensityTexture, 
            spawnFoam, updateFoam);
        computeShader.SetBuffer(density, "density", calculateDensity, calculateLambda, calculateVorticityConfinementForce, updateDensityTexture, spawnFoam, updateFoam);
        computeShader.SetBuffer(lambda, "lambda", calculateLambda, calculatePositionDelta);
        computeShader.SetBuffer(velocity, "velocity", computePredictedPositions, storeSolvedVelocity, calculateVorticityConfinementForce,
            integrateParticles, handleWallCollisions, updateDensityTexture, spawnFoam, updateFoam);
        computeShader.SetBuffer(position, "position", computePredictedPositions, countParticles, calculateDensity, calculateLambda, addPositionDelta, calculatePositionDelta, 
            storeSolvedVelocity, calculateVorticityConfinementForce, integrateParticles, handleWallCollisions, updateDensityTexture, spawnFoam, updateFoam);
        computeShader.SetBuffer(lastPosition, "lastPosition", computePredictedPositions, storeSolvedVelocity, calculateDensity, integrateParticles, handleWallCollisions, updateDensityTexture);
        computeShader.SetBuffer(particleBuffer, "particleBuffer", calculatePositionDelta, addPositionDelta, calculateVorticityConfinementForce, integrateParticles);
        computeShader.SetBuffer(cellContainingParticle, "cellContainingParticle", countParticles, sortParticlesByCell);
        computeShader.SetBuffer(particlesByCell, "particlesByCell", sortParticlesByCell, calculateDensity, calculateLambda, calculatePositionDelta, calculateVorticityConfinementForce, 
            updateDensityTexture, spawnFoam, updateFoam);
        computeShader.SetBuffer(cellStart, "cellStart", setCellStarts, sortParticlesByCell, calculateDensity, calculateLambda, calculatePositionDelta, calculateVorticityConfinementForce,
            updateDensityTexture, spawnFoam, updateFoam);
        computeShader.SetBuffer(cellParticleCount, "cellParticleCount", countParticles, setCellStarts, sortParticlesByCell);
        computeShader.SetBuffer(obstacleData, "obstacleData", integrateParticles);

        computeShader.SetBuffer(foamParticle, "foamParticle", spawnFoam, updateFoam, transferFoamSurvivorsToBuffer, transferFoamSurvivorsBack);
        computeShader.SetBuffer(foamParticleBuffer, "foamParticleBuffer", transferFoamSurvivorsToBuffer, transferFoamSurvivorsBack);
        computeShader.SetBuffer(foamParticleCounter, "foamParticleCounter", spawnFoam, updateFoam, transferFoamSurvivorsToBuffer, transferFoamSurvivorsBack, completeFoamUpdate);

        computeShader.SetBuffer(noise, "noise", scrollNoise, updateDensityTexture);
        computeShader.SetTexture(densityTexture, "densityTex", updateDensityTexture, spawnFoam, updateFoam);
        computeShader.SetTexture(velocityTexture, "velocityTex", updateDensityTexture, updateFoam);

        numParticleThreadGroups = (int)Mathf.Ceil((float)configuration.numParticles / THREADS_PER_GROUP);
        numFoamParticleThreadGroups = (int)Mathf.Ceil((float)configuration.numFoamParticles / THREADS_PER_GROUP);

        InitializeParticlePhysics();
        cellParticleCount.SetData(new uint[numCells]);//to set all to 0

        updateSimSettings = true;
        Initialized?.Invoke();
    }

    private void OnEnable()
    {
        Initialize();
    }

    private void OnDisable()
    {
        configBuffer.Release();
        varsBuffer.Release();

        density.Release();
        lambda.Release();
        velocity.Release();
        position.Release();
        lastPosition.Release();
        particleBuffer.Release();

        cellContainingParticle.Release();
        particlesByCell.Release();
        cellStart.Release();
        cellParticleCount.Release();

        obstacleData.Release();

        foamParticle.Release();
        foamParticleBuffer.Release();
        foamParticleCounter.Release();

        noise.Release();

        densityTexture.Release();
        Destroy(densityTexture);

        velocityTexture.Release();
        Destroy(velocityTexture);
    }

    private void InitializeParticlePhysics()
    {
        var initialPositions = new Vector2[configuration.numParticles];

        velocity.SetData(initialPositions);

        var x0 = simSettings.cellSize + 0.5f * configuration.releaseSpacing;
        var x = x0;
        var y = simSettings.cellSize * (configuration.height - 1) - 0.5f * configuration.releaseSpacing;
        var xmax = simSettings.cellSize * Mathf.Min(configuration.releaseWidth, configuration.width - 1) - 0.5f * configuration.releaseSpacing;

        for (int k = 0; k < configuration.numParticles; k++)
        {
            x += configuration.releaseSpacing;
            if (x > xmax)
            {
                x = x0;
                y -= configuration.releaseSpacing;//allowed to go past bottom of box
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
            var dt = simSettings.updateFrequency * Time.deltaTime / simSettings.stepsPerUpdate;
            UpdateSimSettings(dt);
        }

        if (--updateCounter < 0)
        {
            updateCounter += simSettings.updateFrequency;
            SetObstacleData();

            for (int i = 0; i < simSettings.stepsPerUpdate; i++)
            {
                RunSimulationStep();
            }
        }
    }

    private void RunSimulationStep()
    {
        computeShader.Dispatch(computePredictedPositions, numParticleThreadGroups, 1, 1);

        for (int i = 0; i < simSettings.pressureSolveIterations; i++)
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
        computeShader.Dispatch(integrateParticles, numParticleThreadGroups, 1, 1);
        computeShader.Dispatch(handleWallCollisions, numParticleThreadGroups, 1, 1);
        
        //update foam particles
        computeShader.Dispatch(spawnFoam, numParticleThreadGroups, 1, 1);
        computeShader.Dispatch(updateFoam, numFoamParticleThreadGroups, 1, 1);
        computeShader.Dispatch(transferFoamSurvivorsToBuffer, numFoamParticleThreadGroups, 1, 1);
        computeShader.Dispatch(transferFoamSurvivorsBack, configuration.numFoamParticles, 1, 1);
        computeShader.Dispatch(completeFoamUpdate, 1, 1, 1);
    }

    //maybe put settings into a single struct so we only have to do one set
    private void UpdateSimSettings(float dt)
    {
        varsTransfer[0] = new(configuration, simSettings, foamParticleSettings, densityTexSettings, dt);
        varsBuffer.SetData(varsTransfer);

        computeShader.Dispatch(recalculateAntiClusterCoefficient, 1, 1, 1);

        updateSimSettings = false;
        SettingsChanged?.Invoke();
    }

    //this is for dynamic obstacles moving through the fluid
    //we'll handle static colliders like ground differently
    private void SetObstacleData()
    {
        var worldWidth = configuration.width * simSettings.cellSize;
        var worldHeight = configuration.height * simSettings.cellSize;
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
                    var tScale = Mathf.Min(simSettings.velocityBasedObstacleScaleMultiplier * s2, simSettings.obstacleUpscaleMax);
                    var tRepulsion = Mathf.Min(simSettings.velocityBasedObstacleRepulsionMultiplier * s2, 1);
                    var r = Mathf.Max(o.bounds.extents.x, o.bounds.extents.y);
                    obstacleDataToTransfer[numObstacles++] = new Vector4(o.bounds.center.x - transform.position.x, o.bounds.center.y - transform.position.y, tScale * r, tRepulsion);
                }
            }
        }

        computeShader.SetInt(numObstaclesProperty, numObstacles);
        obstacleData.SetData(obstacleDataToTransfer);
    }

    public void UpdateDensityTexture()
    {
        computeShader.Dispatch(countParticles, numParticleThreadGroups, 1, 1);
        computeShader.Dispatch(setCellStarts, 1, 1, 1);
        computeShader.Dispatch(sortParticlesByCell, numParticleThreadGroups, 1, 1);
        computeShader.Dispatch(scrollNoise, numParticleThreadGroups, 1, 1);
        computeShader.Dispatch(updateDensityTexture, texThreadGroupsX, texThreadGroupsY, 1);
    }
}
