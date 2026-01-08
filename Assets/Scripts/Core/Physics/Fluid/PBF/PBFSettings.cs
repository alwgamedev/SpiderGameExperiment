using System;
using UnityEngine;

[Serializable]
public struct PBFConfiguration
{
    public int width;
    public int height;
    public int releaseWidth;
    public float releaseSpacing;
    public int numParticles;
    public int numFoamParticles;
    public bool drawGizmos;
}


[Serializable]
public struct PBFSimSettings
{
    public float cellSize;
    public int updateFrequency;
    public int stepsPerUpdate;
    public int pressureSolveIterations;
    public int kernelDeg;
    public float restDensity;
    public float gravityScale;
    public float vorticityConfinement;
    public float viscosity;
    public float antiClusterK;
    public float antiClusterDQ;
    public int antiClusterN;
    public float epsilon;
    public float collisionBounciness;
    public LayerMask obstacleMask;
    public float obstacleRepulsion;
    public float velocityBasedObstacleRepulsionMultiplier;
    public float velocityBasedObstacleScaleMultiplier;
    public float obstacleUpscaleMax;
    public float surfaceNormalThreshold;

    public float obstacleBuoyancy;
    public float obstacleDrag;
}

[Serializable]
public struct PBFFoamParticleSettings
{
    public float trappedAirDiffuseRate;
    public float spawnRadiusMultiplier;
    public float spawnRadiusMax;
    public float lifetimeMin;
    public float lifetimeMax;
    public float sprayThreshold;
    public float foamThreshold;
    public float bubbleThreshold;
    public float bubbleBuoyancy;
    public float bubbleDrag;
    public float sprayDrag;
    public float smoothingRadius;
    public float trappedAirNormalizer;
    public float trappedAirThreshold;
    public float kineticEnergyNormalizer;
    public float kineticEnergyThreshold;
}

[Serializable]
public struct PBFDensityTexSettings
{
    public int texWidth;
    public int texHeight;
    public float timeBlur;
    public float smoothingRadius;
    public float noiseSmoothingRadius;
    public float noiseVelocityRadius;
    public float noiseScrollRate;
    public Vector2 noiseStretch;
    public float noiseTimeBlur;
    public float noiseVelocityInfluence;
    public float noiseVelocityInfluenceMax;
    public float foamSmoothingRadius;
}

public struct PBFComputeConfig
{
    public int width;
    public int height;
    public int numCells;
    public int numParticles;
    public int numFoamParticles;

    public PBFComputeConfig(PBFConfiguration config)
    {
        width = config.width;
        height = config.height;
        numCells = config.width * config.height;
        numParticles = config.numParticles;
        numFoamParticles = config.numFoamParticles;
    }
}

public struct PBFComputeVariables
{
    public int kernelDeg;
    public float dt;
    public float dtInverse;
    public float cellSize;
    public float worldWidth;
    public float worldHeight;
    public float smoothingRadius;
    public float smoothingRadiusSqrd;
    public Vector2 gravity;
    public float antiClusterK;
    public float antiClusterDQ;
    public int antiClusterN;
    public float epsilon;
    public float restDensity;
    public float vorticityConfinement;
    public float viscosity;
    public float collisionBounciness;
    public float obstacleRepulsion;
    public float surfaceNormalThreshold;

    //FOAM PARTICLES
    public float trappedAirDiffuseRate;
    public float foamSpawnRadiusMultiplier;
    public float foamSpawnRadiusMax;
    public float foamLifetimeMin;
    public float foamLifetimeMax;
    public float sprayThreshold;
    public float foamThreshold;
    public float bubbleThreshold;
    public float bubbleBuoyancy;
    public float bubbleDrag;
    public float sprayDrag;
    public float foamParticleSmoothingRadius;
    public float foamParticleSmoothingRadiusSqrd;
    public float trappedAirNormalizer;
    public float trappedAirThreshold;
    public float kineticEnergyNormalizer;
    public float kineticEnergyThreshold;

    //DENSITY TEX
    public int texWidth;
    public int texHeight;
    public float texelSizeX;//world width of texel = worldWidth / texWidth
    public float texelSizeY;
    public float timeBlur;
    public float densityTexSmoothingRadius;
    public float densityTexSmoothingRadiusSqrd;
    public float noiseSmoothingRadius;
    public float noiseSmoothingRadiusSqrd;
    public float noiseVelocityRadius;
    public float noiseVelocityRadiusSqrd;
    public float noiseScrollRate;
    public Vector2 noiseStretch;
    public float noiseTimeBlur;
    public float noiseVelocityInfluence;
    public float noiseVelocityInfluenceMax;
    public float foamSmoothingRadius;
    public float foamSmoothingRadiusSqrd;

    public PBFComputeVariables(PBFConfiguration config, PBFSimSettings settings, PBFFoamParticleSettings foamParticleSettings,
        PBFDensityTexSettings densityTexSettings, float dt)
    {
        float w = config.width * settings.cellSize;
        float h = config.height * settings.cellSize;

        kernelDeg = settings.kernelDeg;
        this.dt = dt;
        dtInverse = 1 / dt;
        cellSize = settings.cellSize;
        worldWidth = w;
        worldHeight = h;
        smoothingRadius = 0.5f * settings.cellSize;
        smoothingRadiusSqrd = 0.25f * settings.cellSize * settings.cellSize;
        gravity = settings.gravityScale * Physics2D.gravity;
        antiClusterK = settings.antiClusterK;
        antiClusterDQ = settings.antiClusterDQ;
        antiClusterN = settings.antiClusterN;
        epsilon = settings.epsilon;
        restDensity = settings.restDensity;
        vorticityConfinement = settings.vorticityConfinement;
        viscosity = settings.viscosity;
        collisionBounciness = settings.collisionBounciness;
        obstacleRepulsion = settings.obstacleRepulsion;
        surfaceNormalThreshold = settings.surfaceNormalThreshold;

        trappedAirDiffuseRate = foamParticleSettings.trappedAirDiffuseRate;
        foamSpawnRadiusMultiplier = foamParticleSettings.spawnRadiusMultiplier;
        foamSpawnRadiusMax = foamParticleSettings.spawnRadiusMax;
        foamLifetimeMin = foamParticleSettings.lifetimeMin;
        foamLifetimeMax = foamParticleSettings.lifetimeMax;
        sprayThreshold = foamParticleSettings.sprayThreshold;
        foamThreshold = foamParticleSettings.foamThreshold;
        bubbleThreshold = foamParticleSettings.bubbleThreshold;
        bubbleBuoyancy = foamParticleSettings.bubbleBuoyancy;
        bubbleDrag = foamParticleSettings.bubbleDrag;
        sprayDrag = foamParticleSettings.sprayDrag;
        foamParticleSmoothingRadius = foamParticleSettings.smoothingRadius;
        foamParticleSmoothingRadiusSqrd = foamParticleSettings.smoothingRadius * foamParticleSettings.smoothingRadius;
        trappedAirNormalizer = foamParticleSettings.trappedAirNormalizer;
        trappedAirThreshold = foamParticleSettings.trappedAirThreshold;
        kineticEnergyNormalizer = foamParticleSettings.kineticEnergyNormalizer;
        kineticEnergyThreshold = foamParticleSettings.kineticEnergyThreshold;

        texWidth = densityTexSettings.texWidth;
        texHeight = densityTexSettings.texHeight;
        texelSizeX = w / densityTexSettings.texWidth;//world width of texel = worldWidth / texWidth
        texelSizeY = h / densityTexSettings.texHeight;
        timeBlur = densityTexSettings.timeBlur;
        densityTexSmoothingRadius = densityTexSettings.smoothingRadius;
        densityTexSmoothingRadiusSqrd = densityTexSettings.smoothingRadius * densityTexSettings.smoothingRadius;
        noiseSmoothingRadius = densityTexSettings.noiseSmoothingRadius;
        noiseSmoothingRadiusSqrd = densityTexSettings.noiseSmoothingRadius * densityTexSettings.noiseSmoothingRadius;
        noiseVelocityRadius = densityTexSettings.noiseVelocityRadius;
        noiseVelocityRadiusSqrd = densityTexSettings.noiseVelocityRadius * densityTexSettings.noiseVelocityRadius;
        noiseScrollRate = densityTexSettings.noiseScrollRate;
        noiseStretch = densityTexSettings.noiseStretch;
        noiseTimeBlur = densityTexSettings.noiseTimeBlur;
        noiseVelocityInfluence = densityTexSettings.noiseVelocityInfluence;
        noiseVelocityInfluenceMax = densityTexSettings.noiseVelocityInfluenceMax;
        foamSmoothingRadius = densityTexSettings.foamSmoothingRadius;
        foamSmoothingRadiusSqrd = densityTexSettings.foamSmoothingRadius * densityTexSettings.foamSmoothingRadius;
    }
}