struct PBFConfig
{
    uint width;
    uint height;
    uint numCells;
    uint numParticles;
    uint numFoamParticles;
};

struct PBFVariables
{
    uint kernelDeg;
    float dt;
    float dtInverse;
    float cellSize;
    float worldWidth;
    float worldHeight;
    float smoothingRadius;
    float smoothingRadiusSqrd;
    float2 gravity;
    float antiClusterK;
    float antiClusterDQ;
    uint antiClusterN;
    float epsilon;
    float restDensity;
    float vorticityConfinement;
    float viscosity;
    float collisionBounciness;
    float obstacleRepulsion;
    float surfaceNormalThreshold;
    
    //foam particles
    float trappedAirDiffuseRate;
    float foamSpawnRadiusMultiplier;
    float foamSpawnRadiusMax;
    float foamLifetimeMin;
    float foamLifetimeMax;
    float sprayThreshold;
    float foamThreshold;
    float bubbleThreshold;
    float bubbleBuoyancy;
    float bubbleDrag;
    float sprayDrag;
    float foamParticleSmoothingRadius;
    float foamParticleSmoothingRadiusSqrd;
    float trappedAirNormalizer;
    float trappedAirThreshold;
    float kineticEnergyNormalizer;
    float kineticEnergyThreshold;
    
    //density tex
    uint texWidth;
    uint texHeight;
    float texelSizeX;
    float texelSizeY;
    float timeBlur;
    float densityTexSmoothingRadius;
    float densityTexSmoothingRadiusSqrd;
    float noiseSmoothingRadius;
    float noiseSmoothingRadiusSqrd;
    float noiseVelocityRadius;
    float noiseVelocityRadiusSqrd;
    float noiseScrollRate;
    float2 noiseStretch;
    float noiseTimeBlur;
    float noiseVelocityInfluence;
    float noiseVelocityInfluenceMax;
    float foamSmoothingRadius;
    float foamSmoothingRadiusSqrd;
};