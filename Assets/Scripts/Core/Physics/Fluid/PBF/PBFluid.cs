using System;
using UnityEngine;
using Unity.Collections;
using UnityEngine.Rendering;
using Unity.U2D.Physics;

public class PBFluid : MonoBehaviour
{
    const int THREADS_PER_GROUP = 256;
    const int SMALL_THREADS_PER_GROUP = 64;
    const int MAX_NUM_OBSTACLES = 16;//can make it more when we need to
    const int READBACK_QUEUE_SIZE = 5;

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

    internal ComputeShader computeShader;
    internal RenderTexture densityTexture;

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

    [SerializeField] PolygonPhysicsShapeComponent[] staticObstacleSource;
    ComputeBuffer staticObstacle;
    ComputeBuffer dynamicObstacle;
    ComputeBuffer obstacleDisplacement;
    [SerializeField] PhysicsQuery.QueryFilter obstacleFilter;
    PBFDynamicObstacle[] obstacleDataToTransfer;
    PBFDisplacementReadback[] displacementReadback;
    int incomingRequest;
    int outgoingRequest;

    public ComputeBuffer foamParticleCounter;
    public ComputeBuffer foamParticle;
    ComputeBuffer foamParticleBuffer;

    ComputeBuffer noise;

    bool updateSimSettings;

    int numStaticObstaclesProperty;
    int numDynamicObstaclesProperty;

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

        numStaticObstaclesProperty = Shader.PropertyToID("numStaticObstacles");
        numDynamicObstaclesProperty = Shader.PropertyToID("numDynamicObstacles");
    }


    //INITIALIZATION

    private void Initialize()
    {
        var numCells = configuration.width * configuration.height;

        //create buffers
        configTransfer ??= new PBFComputeConfig[1];
        varsTransfer ??= new PBFComputeVariables[1];
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

        //2do: only include obstacles whose aabb overlaps fluid bounding box
        int numStaticObstacles = 0;
        for (int i = 0; i < staticObstacleSource.Length; i++)
        {
            numStaticObstacles += staticObstacleSource[i].pps.subdividedPolygon.Length;
        }

        if (numStaticObstacles > 0)
        {
            var staticObstacleTemp = new NativeArray<PBFStaticPolygonObstacle>(numStaticObstacles, Allocator.Temp);
            int k = 0;
            for (int i = 0; i < staticObstacleSource.Length; i++)
            {
                var transformMtx = staticObstacleSource[i].transform.localToWorldMatrix;
                transformMtx = Matrix4x4.Translate(-transform.position) * transformMtx;
                var poly = staticObstacleSource[i].pps.subdividedPolygon;
                for (int j = 0; j < poly.Length; j++)
                {
                    var geom = poly[j].Transform(transformMtx, false);
                    var obstacle = new PBFStaticPolygonObstacle(geom);
                    staticObstacleTemp[k++] = obstacle;
                    Debug.Log($"OBSTACLE {k - 1}:");
                    staticObstacleTemp[k - 1].DebugLog(transform.position);
                }
            }

            staticObstacle = new ComputeBuffer(numStaticObstacles, MiscTools.Stride<PBFStaticPolygonObstacle>());
            computeShader.SetInt(numStaticObstaclesProperty, numStaticObstacles);
            staticObstacle.SetData(staticObstacleTemp);
        }
        else
        {
            computeShader.SetInt(numStaticObstaclesProperty, 0);
            staticObstacle = new ComputeBuffer(1, MiscTools.Stride<PBFStaticPolygonObstacle>());
            //compute buffer cannot have size 0, and can't be left unbound
        }

        dynamicObstacle = new ComputeBuffer(MAX_NUM_OBSTACLES, MiscTools.Stride<PBFDynamicObstacle>());
        obstacleDisplacement = new ComputeBuffer(MAX_NUM_OBSTACLES, 4);
        obstacleDataToTransfer ??= new PBFDynamicObstacle[MAX_NUM_OBSTACLES];

        if (displacementReadback == null)
        {
            displacementReadback = new PBFDisplacementReadback[READBACK_QUEUE_SIZE];
            for (int i = 0; i < displacementReadback.Length; i++)
            {
                displacementReadback[i] = new(MAX_NUM_OBSTACLES, this);
            }

            incomingRequest = 0;
            outgoingRequest = 1;
        }

        foamParticle = new ComputeBuffer(configuration.numFoamParticles, 32);
        foamParticleBuffer = new ComputeBuffer(configuration.numFoamParticles, 32);
        foamParticleCounter = new ComputeBuffer(2, 4);
        var foamParticleCounterInit = new NativeArray<int>(2, Allocator.Temp);
        foamParticleCounter.SetData(foamParticleCounterInit);//compute buffer data may not be zeroed out

        //configure compute shader
        configTransfer[0] = new(configuration);
        configBuffer.SetData(configTransfer);

        //generate particle noise values
        var noiseData = new NativeArray<Vector2>(configuration.numParticles, Allocator.Temp);//new Vector2[configuration.numParticles];
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
            dimension = TextureDimension.Tex2D,
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
        computeShader.SetBuffer(staticObstacle, "staticObstacle", handleWallCollisions, updateFoam);
        computeShader.SetBuffer(dynamicObstacle, "dynamicObstacle", integrateParticles);
        computeShader.SetBuffer(obstacleDisplacement, "obstacleDisplacement", integrateParticles, clearObstacleDisplacement);

        computeShader.SetBuffer(foamParticle, "foamParticle", spawnFoam, updateFoam, transferFoamSurvivorsToBuffer, transferFoamSurvivorsBack);
        computeShader.SetBuffer(foamParticleBuffer, "foamParticleBuffer", transferFoamSurvivorsToBuffer, transferFoamSurvivorsBack);
        computeShader.SetBuffer(foamParticleCounter, "foamParticleCounter", spawnFoam, updateFoam, transferFoamSurvivorsToBuffer, transferFoamSurvivorsBack, completeFoamUpdate);

        computeShader.SetBuffer(noise, "noise", scrollNoise, updateDensityTexture);
        computeShader.SetTexture(densityTexture, "densityTex", updateDensityTexture, spawnFoam, updateFoam);

        numParticleThreadGroups = (int)Mathf.Ceil((float)configuration.numParticles / THREADS_PER_GROUP);
        numFoamParticleThreadGroups = (int)Mathf.Ceil((float)configuration.numFoamParticles / THREADS_PER_GROUP);
        numObstacleThreadGroups = (int)Mathf.Ceil((float)MAX_NUM_OBSTACLES / SMALL_THREADS_PER_GROUP);

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

        staticObstacle.Release();
        dynamicObstacle.Release();
        obstacleDisplacement.Release();

        foamParticle.Release();
        foamParticleBuffer.Release();
        foamParticleCounter.Release();

        noise.Release();

        densityTexture.Release();
        Destroy(densityTexture);
    }

    private void OnDestroy()
    {
        if (displacementReadback != null)
        {
            for (int i = 0; i < displacementReadback.Length; i++)
            {
                if (displacementReadback[i] != null && displacementReadback[i].request.done)
                {
                    displacementReadback[i].Dispose();
                }

                //any requests still in progress will be disposed by their request callback when they complete
            }
        }
    }

    private void OnApplicationQuit()
    {
        //wait for requests to complete to make sure we get the callback that disposes the NA
        //(we don't mind the stall when quitting)
        if (displacementReadback != null)
        {
            for (int i = 0; i < displacementReadback.Length; i++)
            {
                if (displacementReadback[i] != null)
                {
                    displacementReadback[i].request.WaitForCompletion();
                }
            }
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
            var dt = Time.deltaTime / simSettings.stepsPerUpdate;
            UpdateSimSettings(dt);
        }

        var inc = displacementReadback[incomingRequest];
        var outg = displacementReadback[outgoingRequest];

        //handle completed requests
        if (inc.request.done)
        {
            ApplyBuoyanceForces(inc);
            incomingRequest = (incomingRequest + 1) % READBACK_QUEUE_SIZE;
        }

        //prepare new request
        if (outg.request.done)
        {
            SetObstacleData(displacementReadback[outgoingRequest]);
        }

        //run sim
        computeShader.Dispatch(clearObstacleDisplacement, numObstacleThreadGroups, 1, 1);
        for (int i = 0; i < simSettings.stepsPerUpdate; i++)
        {
            RunSimulationStep();
        }

        //send request
        if (outg.request.done)
        {
            outg.request = AsyncGPUReadback.RequestIntoNativeArray(ref outg.displacement, obstacleDisplacement, outg.callback);
            outgoingRequest = (outgoingRequest + 1) % READBACK_QUEUE_SIZE;
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
        varsTransfer[0] = new(configuration, simSettings, foamParticleSettings, densityTexSettings, PhysicsWorld.defaultWorld.gravity, dt);
        varsBuffer.SetData(varsTransfer);

        computeShader.Dispatch(recalculateAntiClusterCoefficient, 1, 1, 1);

        updateSimSettings = false;
        SettingsChanged?.Invoke();
    }

    //this is for dynamic obstacles moving through the fluid
    //we'll handle static colliders like ground differently
    private void SetObstacleData(PBFDisplacementReadback r)
    {
        var worldWidth = configuration.width * simSettings.cellSize;
        var worldHeight = configuration.height * simSettings.cellSize;
        var boxCenter = new Vector2(transform.position.x + 0.5f * worldWidth, transform.position.y + 0.5f * worldHeight);
        var boxSize = new Vector2(worldWidth, worldHeight);
        var boxTransform = new PhysicsTransform(boxCenter, PhysicsRotate.identity);
        var box = PolygonGeometry.CreateBox(boxSize, 0, boxTransform);
        var overlapResults = PhysicsWorld.defaultWorld.OverlapGeometry(box, obstacleFilter);

        //put non-null obstacles at the beginning of the compute buffer
        //and we'll use numObstacles to mark the end so we don't have to clear out the rest of the buffer
        r.numObstacles = 0;
        for (int i = 0; i < overlapResults.Length; i++)
        {
            if (r.numObstacles == MAX_NUM_OBSTACLES)
            {
                break;
            }

            var shape = overlapResults[i].shape;
            if (shape.isValid && shape.userData.objectValue is PBFDynamicObstacleSO o && o)
            {
                var spd = shape.body.linearVelocity.magnitude;
                var scale = Mathf.Min(simSettings.velocityBasedObstacleScaleMultiplier * spd, simSettings.obstacleUpscaleMax);
                var repulsion = Mathf.Min(simSettings.velocityBasedObstacleRepulsionMultiplier * spd, 1);

                r.obstacle[r.numObstacles] = shape;
                var localBB = shape.CalculateAABB(PhysicsTransform.identity);
                obstacleDataToTransfer[r.numObstacles++] = new()
                {
                    center = shape.transform.TransformPoint(localBB.center) - (Vector2)transform.position,
                    extents = o.extentsMultiplier * localBB.extents,
                    xDirection = shape.transform.rotation.direction,
                    speedScaledRadius = Mathf.Min(scale * o.repulsionRadius, o.repulsionRadiusMax),
                    repulsionMultiplier = repulsion
                };

                //var center = shape.transform.TransformPoint(localBB.center);
                //var right = o.extentsMultiplier * localBB.extents.x * shape.transform.rotation.direction;
                //var up = o.extentsMultiplier * localBB.extents.y * shape.transform.rotation.direction.CCWPerp();
                //Debug.DrawLine(center + right + up, center + right - up, Color.red);
                //Debug.DrawLine(center + right - up, center - right - up, Color.red);
                //Debug.DrawLine(center - right - up, center - right + up, Color.red);
                //Debug.DrawLine(center - right + up, center + right + up, Color.red);
                //var ssr = obstacleDataToTransfer[r.numObstacles - 1].speedScaledRadius;
                //right *= ssr;
                //up *= ssr;
                //Debug.DrawLine(center + right + up, center + right - up, Color.orange);
                //Debug.DrawLine(center + right - up, center - right - up, Color.orange);
                //Debug.DrawLine(center - right - up, center - right + up, Color.orange);
                //Debug.DrawLine(center - right + up, center + right + up, Color.orange);
            }
        }

        computeShader.SetInt(numDynamicObstaclesProperty, r.numObstacles);
        dynamicObstacle.SetData(obstacleDataToTransfer);
    }

    private void ApplyBuoyanceForces(PBFDisplacementReadback r)
    {
        var b = -simSettings.obstacleBuoyancy / simSettings.stepsPerUpdate * PhysicsWorld.defaultWorld.gravity;
        for (int i = 0; i < r.numObstacles; i++)
        {
            var o = r.obstacle[i];
            float d = r.displacement[i];
            if (o.isValid && d > 0)
            {
                var body = o.body;
                var v = body.linearVelocity;
                float extentsMult;
                if (o.userData.objectValue is PBFDynamicObstacleSO dfo && dfo)
                {
                    extentsMult = dfo.extentsMultiplier;
                }
                else
                {
                    extentsMult = 1;
                }

                var area = o.body.mass / o.definition.density;
                //^we'll multiply drag by displacement / area (so less drag when displace less fluid, and divide by area so this term not affected by size of object)
                //then we'll use sqrt(area) as the drag cross-section, so in the end we multiply by displacemt / sqrt(area)
                //(nonsense approach, but good enough)

                var f = d * (b - simSettings.obstacleDrag * extentsMult / Mathf.Sqrt(area) * v.magnitude * v);
                body.ApplyForceToCenter(f);
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
