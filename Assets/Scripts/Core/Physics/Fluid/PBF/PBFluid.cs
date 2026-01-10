using System;
using UnityEngine;
using Unity.Collections;
using UnityEngine.Rendering;

public class PBFluid : MonoBehaviour
{
    const int THREADS_PER_GROUP = 256;
    const int MAX_NUM_OBSTACLES = 1;//we'll make it more when we need to

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
    public const int clearObstacleDisplacement = 20;

    [SerializeField] ComputeShader _computeShader;

    public PBFConfiguration configuration;
    public PBFSimSettings simSettings;
    public PBFFoamParticleSettings foamParticleSettings;
    public PBFDensityTexSettings densityTexSettings;

    [HideInInspector] public ComputeShader computeShader;
    [HideInInspector] public RenderTexture densityTexture;

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

    struct ObstacleData
    {
        public Vector2 center;
        public Vector2 extents;
        public float speedScaledRadius;
        public float repulsionMultiplier;
    }

    ComputeBuffer obstacleData;
    ObstacleData[] obstacleDataTransfer;
    Collider2D[] colliderQueryBuffer;
    ContactFilter2D colliderFilter;
    PBFDynamicObstacle[] obstacle;
    int numObstacles;

    ComputeBuffer obstacleDisplacement;
    bool displacementReadbackInProgress;
    NativeArray<int> obstacleDisplacementNA;
    PBFDynamicObstacle[] obstacleSnapshot;//snapshots taken when we send a readback request (so we have the correct collider lookup to go with the displacements)
    int numObstaclesSnapshot;
    //PBFDynamicObstacle[] obstacleLastReadback;//copied from the snapshot when readback comes in, to be used for physics updates until the next readback comes in
    //int numObstaclesLastReadback;
    float lastReadbackTime;

    public ComputeBuffer foamParticleCounter;
    public ComputeBuffer foamParticle;
    ComputeBuffer foamParticleBuffer;

    ComputeBuffer noise;

    int updateCounter;
    bool updateSimSettings;

    int numObstaclesProperty;

    int numParticleThreadGroups;
    int numFoamParticleThreadGroups;
    int numObstacleThreadGroups;
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

        obstacleData = new ComputeBuffer(MAX_NUM_OBSTACLES, MiscTools.Stride<ObstacleData>());
        obstacleDataTransfer = new ObstacleData[MAX_NUM_OBSTACLES];
        colliderQueryBuffer = new Collider2D[MAX_NUM_OBSTACLES];

        obstacleDisplacement = new ComputeBuffer(MAX_NUM_OBSTACLES, 4);
        colliderQueryBuffer = new Collider2D[MAX_NUM_OBSTACLES];
        obstacle = new PBFDynamicObstacle[MAX_NUM_OBSTACLES];
        obstacleSnapshot = new PBFDynamicObstacle[MAX_NUM_OBSTACLES];
        //obstacleLastReadback = new PBFDynamicObstacle[MAX_NUM_OBSTACLES];
        lastReadbackTime = Time.time;

        colliderFilter = ContactFilter2D.noFilter;
        colliderFilter.useTriggers = false;
        colliderFilter.SetLayerMask(simSettings.obstacleMask);

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
        computeShader.SetBuffer(obstacleDisplacement, "obstacleDisplacement", integrateParticles, clearObstacleDisplacement);

        computeShader.SetBuffer(foamParticle, "foamParticle", spawnFoam, updateFoam, transferFoamSurvivorsToBuffer, transferFoamSurvivorsBack);
        computeShader.SetBuffer(foamParticleBuffer, "foamParticleBuffer", transferFoamSurvivorsToBuffer, transferFoamSurvivorsBack);
        computeShader.SetBuffer(foamParticleCounter, "foamParticleCounter", spawnFoam, updateFoam, transferFoamSurvivorsToBuffer, transferFoamSurvivorsBack, completeFoamUpdate);

        computeShader.SetBuffer(noise, "noise", scrollNoise, updateDensityTexture);
        computeShader.SetTexture(densityTexture, "densityTex", updateDensityTexture, spawnFoam, updateFoam);

        numParticleThreadGroups = (int)Mathf.Ceil((float)configuration.numParticles / THREADS_PER_GROUP);
        numFoamParticleThreadGroups = (int)Mathf.Ceil((float)configuration.numFoamParticles / THREADS_PER_GROUP);
        numObstacleThreadGroups = (int)Mathf.Ceil((float)MAX_NUM_OBSTACLES / THREADS_PER_GROUP);

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
        obstacleDisplacement.Release();
        //obstacleDisplacementNA.Dispose();
        //if (displacementRequest.done)
        //{
        //    obstacleDisplacementTransfer.Dispose();
        //}

        foamParticle.Release();
        foamParticleBuffer.Release();
        foamParticleCounter.Release();

        noise.Release();

        densityTexture.Release();
        Destroy(densityTexture);
    }

    private void OnDestroy()
    {
        if (!displacementReadbackInProgress && obstacleDisplacementNA.IsCreated)
        {
            obstacleDisplacementNA.Dispose();
        }
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
            computeShader.Dispatch(clearObstacleDisplacement, numObstacleThreadGroups, 1, 1);

            for (int i = 0; i < simSettings.stepsPerUpdate; i++)
            {
                RunSimulationStep();
            }
        }

        if (!displacementReadbackInProgress)
        {
            //a readback just finished, so copy snapshot data to use until next readback is ready
            //numObstaclesLastReadback = numObstaclesSnapshot;
            //Array.Copy(obstacleSnapshot, obstacleLastReadback, obstacleSnapshot.Length);

            ApplyBuoyanceForces(Mathf.Min(Time.time - lastReadbackTime, 4 * Time.deltaTime));
            lastReadbackTime = Time.time;

            //take new snapshot to get ready for next readback
            numObstaclesSnapshot = numObstacles;
            Array.Copy(obstacle, obstacleSnapshot, obstacle.Length);

            //send new readback
            SendDisplacementReadbackRequest();
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
        Array.Clear(colliderQueryBuffer, 0, colliderQueryBuffer.Length);
        Physics2D.OverlapBox(boxCenter, boxSize, 0, colliderFilter, colliderQueryBuffer);

        //put non-null obstacles at the beginning of the compute buffer
        //and we'll use numObstacles to mark the end so we don't have to clear out the rest of the buffer
        numObstacles = 0;
        for (int i = 0; i < MAX_NUM_OBSTACLES; i++)
        {
            var c = colliderQueryBuffer[i];
            if (c && c.gameObject.TryGetComponent(out PBFDynamicObstacle o))
            {
                var spd = o.Rigidbody.linearVelocity.magnitude;
                var tScale = Mathf.Min(simSettings.velocityBasedObstacleScaleMultiplier * spd, simSettings.obstacleUpscaleMax);
                var tRepulsion = Mathf.Min(simSettings.velocityBasedObstacleRepulsionMultiplier * spd, 1);
                obstacle[numObstacles] = o;
                obstacleDataTransfer[numObstacles++] = new()
                {
                    center = new(o.Collider.bounds.center.x - transform.position.x, o.Collider.bounds.center.y - transform.position.y),
                    extents = o.Collider.bounds.extents,
                    speedScaledRadius = Mathf.Min(tScale * o.RepulsionRadius, o.RepulsionRadiusMax),
                    repulsionMultiplier = tRepulsion
                };
            }
        }

        computeShader.SetInt(numObstaclesProperty, numObstacles);
        obstacleData.SetData(obstacleDataTransfer);
    }

    private void SendDisplacementReadbackRequest()
    {
        displacementReadbackInProgress = true;
        AsyncGPUReadback.Request(obstacleDisplacement, OnDisplacementReadbackComplete);
        //^RequestIntoNativeArray keeps disposing of my native array or something. no clue. so just this for now
    }

    private void OnDisplacementReadbackComplete(AsyncGPUReadbackRequest req)
    {
        if (!this)
        {
            if (obstacleDisplacementNA.IsCreated)
            {
                obstacleDisplacementNA.Dispose();
            }
            return;
        }

        if (!obstacleDisplacementNA.IsCreated)
        {
            obstacleDisplacementNA = new(req.GetData<int>(), Allocator.Persistent);
        }
        else
        {
            obstacleDisplacementNA.CopyFrom(req.GetData<int>());
        }
        displacementReadbackInProgress = false;
    }

    private void ApplyBuoyanceForces(float dt)
    {
        var b = -simSettings.obstacleBuoyancy * Physics2D.gravity;
        for (int i = 0; i < numObstaclesSnapshot; i++)
        {
            if (!(obstacleDisplacementNA[i] > 0))
            {
                return;
            }

            var o = obstacleSnapshot[i];
            if (o)
            {
                Debug.Log(obstacleDisplacementNA[i]);
                Debug.Log(o.Collider.bounds.extents);
                var v = o.Rigidbody.linearVelocity;
                var f = obstacleDisplacementNA[i] * b - o.Collider.bounds.size.x * simSettings.obstacleDrag * v.magnitude * v;
                o.Rigidbody.linearVelocity += dt * f;
            }
        }
    }

    //DENSITY TEX

    public void UpdateDensityTexture()
    {
        computeShader.Dispatch(countParticles, numParticleThreadGroups, 1, 1);
        computeShader.Dispatch(setCellStarts, 1, 1, 1);
        computeShader.Dispatch(sortParticlesByCell, numParticleThreadGroups, 1, 1);
        computeShader.Dispatch(scrollNoise, numParticleThreadGroups, 1, 1);
        computeShader.Dispatch(updateDensityTexture, texThreadGroupsX, texThreadGroupsY, 1);
    }
}
