using UnityEngine;

public class FLIPFluid : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField] int cols;
    [SerializeField] int rows;
    [SerializeField] float cellSize;

    [Header("Simulation")]
    [SerializeField] int pushApartIterations;
    [SerializeField] int solveIterations;
    [SerializeField] float overRelaxation;
    [SerializeField] float densitySpringConstant;
    [SerializeField] float obstacleSpeedNormalizer;

    [Header("Particles")]
    [SerializeField] int numParticles;
    [SerializeField] float particleRadius;
    [SerializeField] LayerMask collisionMask;
    [SerializeField] float collisionBounciness;

    [Header("Rendering")]
    [SerializeField] Mesh particleMesh;
    [SerializeField] Shader shader;

    ComputeBuffer densityGrid;
    ComputeBuffer velocityGrid;
    ComputeBuffer prevVelocityGrid;
    ComputeBuffer obstaclePresent;
    ComputeBuffer obstacleVelocity;
    ComputeBuffer particlePosition;
    ComputeBuffer particleVelocity;

    Material material;
    Bounds bounds;
    ComputeBuffer argsBuffer;

    private void Initialize()
    {
        //also if mesh is null we can create a circle mesh here
    }

    private void OnEnable()
    {
        Initialize();
    }

    private void LateUpdate()
    {
        material.SetBuffer("_ParticlePosition", particlePosition);
        //also do args buffer and bounds
        Graphics.DrawMeshInstancedIndirect(particleMesh, 0, material, bounds, argsBuffer);
    }

    private void OnDisable()
    {
        densityGrid?.Release();
        velocityGrid?.Release();
        prevVelocityGrid?.Release();
        obstaclePresent?.Release();
        obstacleVelocity?.Release();
        particlePosition?.Release();
        particleVelocity?.Release();
        argsBuffer?.Release();
    }
}
