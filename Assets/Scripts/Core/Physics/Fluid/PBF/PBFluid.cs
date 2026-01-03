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

    [Header("Configuration")]
    public int initialWidth;
    public float initialSpacing;
    public int width;
    public int height;
    public float cellSize;
    public int numParticles;
    public int numFoamParticles;
    [SerializeField] bool drawGizmos;

    [Header("Simulation Settings")]
    [SerializeField] int updateFrequency;
    [SerializeField] int stepsPerUpdate;
    [SerializeField] int pressureSolveIterations;
    [SerializeField] int kernelDeg;
    [SerializeField] float restDensity;
    [SerializeField] float gravityScale;
    [SerializeField] float vorticityConfinement;
    [SerializeField] float viscosity;
    [SerializeField] float antiClusterK;
    [SerializeField] float antiClusterDQ;
    [SerializeField] int antiClusterN;
    [SerializeField] float epsilon;
    [SerializeField] float collisionBounciness;
    [SerializeField] LayerMask obstacleMask;
    [SerializeField] float obstacleRepulsion;
    [SerializeField] float velocityBasedObstacleRepulsionMultiplier;
    [SerializeField] float velocityBasedObstacleScaleMultiplier;
    [SerializeField] float obstacleUpscaleMax;

    [Header("Foam Particles")]
    [SerializeField] float foamSpawnRate;
    [SerializeField] float foamSpawnRadiusMultiplier;
    [SerializeField] float foamSpawnRadiusMax;
    [SerializeField] float foamLifetimeMin;
    [SerializeField] float foamLifetimeMax;
    [SerializeField] float bubbleThreshold;
    [SerializeField] float foamThreshold;
    [SerializeField] float bubbleBuoyancy;
    [SerializeField] float bubbleDrag;

    [Header("Density Texture")]
    [SerializeField] int texWidth;
    [SerializeField] int texHeight;
    [SerializeField] float timeBlur;
    [SerializeField] float densityTexSmoothingRadius;
    [SerializeField] float noiseSmoothingRadius;
    [SerializeField] float noiseVelocityRadius;
    [SerializeField] float noiseScrollRate;
    [SerializeField] Vector2 noiseStretch;
    [SerializeField] float noiseTimeBlur;
    [SerializeField] float noiseVelocityInfluence;
    [SerializeField] float noiseVelocityInfluenceMax;
    [SerializeField] float foamSmoothingRadius;
    [SerializeField] float foamVelocityInfluence;
    [SerializeField] float foamVelocityThreshold;

    [HideInInspector] public ComputeShader computeShader;
    [HideInInspector] public RenderTexture densityTexture;
    [HideInInspector] public RenderTexture velocityTexture;

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

    //this is getting dumb (combine the properties into one struct)
    int kernelDegProperty;
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

    int foamSpawnRateProperty;
    int foamSpawnRadiusMultiplierProperty;
    int foamSpawnRadiusMaxProperty;
    int foamLifetimeMinProperty;
    int foamLifetimeMaxProeprty;
    int bubbleThresholdProperty;
    int foamThresholdProperty;
    int bubbleBuoyancyProperty;
    int bubbleDragProperty;
    
    int texWidthProperty;
    int texHeightProperty;
    int texelSizeXProperty;
    int texelSizeYProperty;
    int timeBlurProperty;
    int densityTexSmoothingRadiusProperty;
    int densityTexSmoothingRadiusSqrdProperty;
    int noiseSmoothingRadiusProperty;
    int noiseSmoothingRadiusSqrdProperty;
    int noiseVelocityRadiusProperty;
    int noiseVelocityRadiusSqrdProperty;
    int noiseScrollRateProperty;
    int noiseStretchProperty;
    int noiseTimeBlurProperty;
    int noiseVelocityInfluenceProperty;
    int noiseVelocityInfluenceMaxProperty;
    int foamSmoothingRadiusProperty;
    int foamSmoothingRadiusSqrdProperty;
    int foamVelocityInfluenceProperty;
    int foamVelocityThresholdProperty;

    int numParticleThreadGroups;
    int numFoamParticleThreadGroups;
    int texThreadGroupsX;
    int texThreadGroupsY;

    public event Action Initialized;

    private void OnDrawGizmos()
    {
        if (drawGizmos)
        {
            //to visualize the grid bounds
            Gizmos.color = Color.yellow;
            float w = width * cellSize;
            float h = height * cellSize;
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

        kernelDegProperty = Shader.PropertyToID("kernelDeg");
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

        foamSpawnRateProperty = Shader.PropertyToID("foamSpawnRate");
        foamSpawnRadiusMultiplierProperty = Shader.PropertyToID("foamSpawnRadiusMultiplier");
        foamSpawnRadiusMaxProperty = Shader.PropertyToID("foamSpawnRadiusMax");
        foamLifetimeMinProperty = Shader.PropertyToID("foamLifetimeMin");
        foamLifetimeMaxProeprty = Shader.PropertyToID("foamLifetimeMax");
        bubbleThresholdProperty = Shader.PropertyToID("bubbleThreshold");
        foamThresholdProperty = Shader.PropertyToID("foamThreshold");
        bubbleBuoyancyProperty = Shader.PropertyToID("bubbleBuoyancy");
        bubbleDragProperty = Shader.PropertyToID("bubbleDrag");

        texWidthProperty = Shader.PropertyToID("texWidth");
        texHeightProperty = Shader.PropertyToID("texHeight");
        texelSizeXProperty = Shader.PropertyToID("texelSizeX");
        texelSizeYProperty = Shader.PropertyToID("texelSizeY");
        timeBlurProperty = Shader.PropertyToID("timeBlur");
        densityTexSmoothingRadiusProperty = Shader.PropertyToID("densityTexSmoothingRadius");
        densityTexSmoothingRadiusSqrdProperty = Shader.PropertyToID("densityTexSmoothingRadiusSqrd");
        noiseSmoothingRadiusProperty = Shader.PropertyToID("noiseSmoothingRadius");
        noiseSmoothingRadiusSqrdProperty = Shader.PropertyToID("noiseSmoothingRadiusSqrd");
        noiseVelocityRadiusProperty = Shader.PropertyToID("noiseVelocityRadius");
        noiseVelocityRadiusSqrdProperty = Shader.PropertyToID("noiseVelocityRadiusSqrd");
        noiseScrollRateProperty = Shader.PropertyToID("noiseScrollRate");
        noiseStretchProperty = Shader.PropertyToID("noiseStretch");
        noiseTimeBlurProperty = Shader.PropertyToID("noiseTimeBlur");
        noiseVelocityInfluenceProperty = Shader.PropertyToID("noiseVelocityInfluence");
        noiseVelocityInfluenceMaxProperty = Shader.PropertyToID("noiseVelocityInfluenceMax");
        foamSmoothingRadiusProperty = Shader.PropertyToID("foamSmoothingRadius");
        foamSmoothingRadiusSqrdProperty = Shader.PropertyToID("foamSmoothingRadiusSqrd");
        foamVelocityInfluenceProperty = Shader.PropertyToID("foamVelocityInfluence");
        foamVelocityThresholdProperty = Shader.PropertyToID("foamVelocityThreshold");
    }


    //INITIALIZATION

    private void Initialize()
    {
        var numCells = width * height;

        //create buffers
        velocity = new ComputeBuffer(numParticles, 8);
        position = new ComputeBuffer(numParticles, 8);
        lastPosition = new ComputeBuffer(numParticles, 8);
        particleBuffer = new ComputeBuffer(numParticles, 8);
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

        foamParticle = new ComputeBuffer(numFoamParticles, 20);
        foamParticleBuffer = new ComputeBuffer(numFoamParticles, 20);
        foamParticleCounter = new ComputeBuffer(2, 4);
        foamParticleCounter.SetData(new int[2]);

        //configure compute shader
        computeShader.SetInt("width", width);
        computeShader.SetInt("height", height);
        computeShader.SetInt("numCells", numCells);
        computeShader.SetInt("numParticles", numParticles);
        computeShader.SetInt("numFoamParticles", numFoamParticles);

        //generate particle noise values
        var noiseData = new Vector2[numParticles];
        for (int i = 0; i < noiseData.Length; i++)
        {
            noiseData[i] = new(MathTools.RandomFloat(0f, 1f), MathTools.RandomFloat(0.25f, 1.75f));
        }
        noise = new ComputeBuffer(noiseData.Length, 8);
        noise.SetData(noiseData);

        //configure density texture
        densityTexture = new(texWidth, texHeight, 0)
        {
            graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat,
            dimension = UnityEngine.Rendering.TextureDimension.Tex2D,
            enableRandomWrite = true
        };

        velocityTexture = new(texWidth, texHeight, 0)
        {
            graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16_SFloat,
            dimension = UnityEngine.Rendering.TextureDimension.Tex2D,
            enableRandomWrite = true
        };

        computeShader.SetInt(texWidthProperty, texWidth);
        computeShader.SetInt(texHeightProperty, texHeight);

        texThreadGroupsX = (int)Mathf.Ceil(texWidth / 16f);
        texThreadGroupsY = (int)Mathf.Ceil(texHeight / 16f);


        //bind buffers to compute shader
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
            updateDensityTexture, updateFoam);
        computeShader.SetBuffer(cellStart, "cellStart", setCellStarts, sortParticlesByCell, calculateDensity, calculateLambda, calculatePositionDelta, calculateVorticityConfinementForce,
            updateDensityTexture, updateFoam);
        computeShader.SetBuffer(cellParticleCount, "cellParticleCount", countParticles, setCellStarts, sortParticlesByCell);
        computeShader.SetBuffer(obstacleData, "obstacleData", integrateParticles);

        computeShader.SetBuffer(foamParticle, "foamParticle", spawnFoam, updateFoam, transferFoamSurvivorsToBuffer, transferFoamSurvivorsBack);
        computeShader.SetBuffer(foamParticleBuffer, "foamParticleBuffer", transferFoamSurvivorsToBuffer, transferFoamSurvivorsBack);
        computeShader.SetBuffer(foamParticleCounter, "foamParticleCounter", spawnFoam, updateFoam, transferFoamSurvivorsToBuffer, transferFoamSurvivorsBack, completeFoamUpdate);

        computeShader.SetBuffer(noise, "noise", scrollNoise, updateDensityTexture);
        computeShader.SetTexture(densityTexture, "densityTex", updateDensityTexture, spawnFoam, updateFoam);
        computeShader.SetTexture(velocityTexture, "velocityTex", updateDensityTexture, updateFoam);

        numParticleThreadGroups = (int)Mathf.Ceil((float)numParticles / THREADS_PER_GROUP);
        numFoamParticleThreadGroups = (int)Mathf.Ceil((float)numFoamParticles / THREADS_PER_GROUP);

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
        var initialPositions = new Vector2[numParticles];

        velocity.SetData(initialPositions);

        var x0 = cellSize + 0.5f * initialSpacing;
        var x = x0;
        var y = cellSize * (height - 1) - 0.5f * initialSpacing;
        var xmax = cellSize * Mathf.Min(initialWidth, width - 1) - 0.5f * initialSpacing;
        //var ymin = cellSize + 0.5f * spacing;

        for (int k = 0; k < numParticles; k++)
        {
            x += initialSpacing;
            if (x > xmax)
            {
                x = x0;
                y -= initialSpacing;//allowed to go past bottom of box
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
        computeShader.Dispatch(integrateParticles, numParticleThreadGroups, 1, 1);
        computeShader.Dispatch(handleWallCollisions, numParticleThreadGroups, 1, 1);
        
        //update foam particles
        computeShader.Dispatch(spawnFoam, numParticleThreadGroups, 1, 1);
        computeShader.Dispatch(updateFoam, numFoamParticleThreadGroups, 1, 1);
        computeShader.Dispatch(transferFoamSurvivorsToBuffer, numFoamParticleThreadGroups, 1, 1);
        computeShader.Dispatch(transferFoamSurvivorsBack, numFoamParticles, 1, 1);
        computeShader.Dispatch(completeFoamUpdate, 1, 1, 1);
    }

    //maybe put settings into a single struct so we only have to do one set
    private void UpdateSimSettings(float dt)
    {
        float w = width * cellSize;
        float h = height * cellSize;

        computeShader.SetInt(kernelDegProperty, kernelDeg);
        computeShader.SetFloat(dtProperty, dt);
        computeShader.SetFloat(dtInverseProperty, 1 / dt);
        computeShader.SetFloat(cellSizeProperty, cellSize);
        computeShader.SetFloat(worldWidthProperty, w);
        computeShader.SetFloat(worldHeightProperty, h);
        computeShader.SetFloat(smoothingRadiusProperty, 0.5f * cellSize);
        computeShader.SetFloat(smoothingRadiusSqrdProperty, 0.25f * cellSize * cellSize);
        computeShader.SetVector(gravityProperty, gravityScale * Physics2D.gravity);
        computeShader.SetFloat(antiClusterKProperty, antiClusterK);
        computeShader.SetFloat(antiClusterDQProperty, antiClusterDQ);
        computeShader.SetInt(antiClusterNProperty, antiClusterN);
        computeShader.SetFloat(epsilonProperty, epsilon);
        computeShader.SetFloat(restDensityProperty, restDensity);
        computeShader.SetFloat(vorticityConfinementProperty, vorticityConfinement);
        computeShader.SetFloat(viscosityProperty, viscosity);
        computeShader.SetFloat(collisionBouncinessProperty, collisionBounciness);
        computeShader.SetFloat(obstacleRepulsionProperty, obstacleRepulsion);

        computeShader.SetFloat(foamSpawnRateProperty, foamSpawnRate);
        computeShader.SetFloat(foamSpawnRadiusMultiplierProperty, foamSpawnRadiusMultiplier);
        computeShader.SetFloat(foamSpawnRadiusMaxProperty, foamSpawnRadiusMax);
        computeShader.SetFloat(foamLifetimeMinProperty, foamLifetimeMin);
        computeShader.SetFloat(foamLifetimeMaxProeprty, foamLifetimeMax);
        computeShader.SetFloat(bubbleThresholdProperty, bubbleThreshold);
        computeShader.SetFloat(foamThresholdProperty, foamThreshold);
        computeShader.SetFloat(bubbleBuoyancyProperty, bubbleBuoyancy);
        computeShader.SetFloat(bubbleDragProperty, bubbleDrag);

        computeShader.SetFloat(texelSizeXProperty, w / texWidth);
        computeShader.SetFloat(texelSizeYProperty, h / texHeight);
        computeShader.SetFloat(densityTexSmoothingRadiusProperty, densityTexSmoothingRadius);
        computeShader.SetFloat(densityTexSmoothingRadiusSqrdProperty, densityTexSmoothingRadius * densityTexSmoothingRadius);
        computeShader.SetFloat(noiseSmoothingRadiusProperty, noiseSmoothingRadius);
        computeShader.SetFloat(noiseSmoothingRadiusSqrdProperty, noiseSmoothingRadius * noiseSmoothingRadius);
        computeShader.SetFloat(noiseVelocityRadiusProperty, noiseVelocityRadius);
        computeShader.SetFloat(noiseVelocityRadiusSqrdProperty, noiseVelocityRadius * noiseVelocityRadius);
        computeShader.SetFloat(noiseScrollRateProperty, noiseScrollRate);
        computeShader.SetVector(noiseStretchProperty, noiseStretch);
        computeShader.SetFloat(timeBlurProperty, timeBlur);
        computeShader.SetFloat(noiseTimeBlurProperty, noiseTimeBlur);
        computeShader.SetFloat(noiseVelocityInfluenceProperty, noiseVelocityInfluence);
        computeShader.SetFloat(noiseVelocityInfluenceMaxProperty, noiseVelocityInfluenceMax);
        computeShader.SetFloat(foamSmoothingRadiusProperty, foamSmoothingRadius);
        computeShader.SetFloat(foamSmoothingRadiusSqrdProperty, foamSmoothingRadius * foamSmoothingRadius);
        computeShader.SetFloat(foamVelocityInfluenceProperty, foamVelocityInfluence);
        computeShader.SetFloat(foamVelocityThresholdProperty, foamVelocityThreshold);

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

    public void UpdateDensityTexture()
    {
        computeShader.Dispatch(countParticles, numParticleThreadGroups, 1, 1);
        computeShader.Dispatch(setCellStarts, 1, 1, 1);
        computeShader.Dispatch(sortParticlesByCell, numParticleThreadGroups, 1, 1);
        computeShader.Dispatch(scrollNoise, numParticleThreadGroups, 1, 1);
        computeShader.Dispatch(updateDensityTexture, texThreadGroupsX, texThreadGroupsY, 1);
    }
}
