using UnityEngine;

public class PBFRenderer : MonoBehaviour
{
    [SerializeField] PBFluid pbFluid;

    [Header("Density Shader Settings")]
    [SerializeField] Shader densityShader;
    [SerializeField] Color colorMin;
    [SerializeField] Color colorMax;
    [SerializeField] Color foamColor;
    [SerializeField] int smoothStepIterations;
    [SerializeField] float densityNormalizer;
    [SerializeField] float densityThreshold;
    [SerializeField] float noiseNormalizer;
    [SerializeField] float noiseThreshold;
    [SerializeField] float foaminessNormalizer;
    [SerializeField] float foaminessThreshold;

    [Header("Foam Particle Settings")]
    [SerializeField] Shader particleShader;
    [SerializeField] Color particleColor;
    [SerializeField] float particleRadius;

    Mesh densityMesh;
    Material densityMaterial;

    Mesh particleMesh;
    Material particleMaterial; 
    GraphicsBuffer commandBuffer;
    GraphicsBuffer.IndirectDrawIndexedArgs[] commandData;

    int colorMinProperty;
    int colorMaxProperty;
    int foamColorProperty;
    int smoothStepIterationsProperty;
    int densityNormalizerProperty;
    int densityThresholdProperty;
    int noiseNormalizerProperty;
    int noiseThresholdProperty;
    int foaminessNormalizerProperty;
    int foaminessThresholdProperty;

    int pivotPositionProperty;
    int particleColorProperty;
    int particleRadiusProperty;

    bool updateProperties;

    private void Awake()
    {
        colorMinProperty = Shader.PropertyToID("colorMin");
        colorMaxProperty = Shader.PropertyToID("colorMax");
        foamColorProperty = Shader.PropertyToID("foamColor");

        smoothStepIterationsProperty = Shader.PropertyToID("smoothStepIterations");
        densityNormalizerProperty = Shader.PropertyToID("densityNormalizer");
        densityThresholdProperty = Shader.PropertyToID("densityThreshold");
        noiseNormalizerProperty = Shader.PropertyToID("noiseNormalizer");
        noiseThresholdProperty = Shader.PropertyToID("noiseThreshold");
        foaminessNormalizerProperty = Shader.PropertyToID("foaminessNormalizer");
        foaminessThresholdProperty = Shader.PropertyToID("foaminessThreshold");

        pivotPositionProperty = Shader.PropertyToID("pivotPosition");
        particleColorProperty = Shader.PropertyToID("particleColor");
        particleRadiusProperty = Shader.PropertyToID("particleRadius");
    }

    private void OnEnable()
    {
        pbFluid.Initialized += OnPBFInitialized;

        if (pbFluid.enabled)
        {
            OnPBFInitialized();
        }
    }

    private void Start()
    {
        commandBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);
        commandData = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
    }

    private void OnDisable()
    {
        if (pbFluid != null)
        {
            pbFluid.Initialized -= OnPBFInitialized;
        }
    }

    private void OnDestroy()
    {
        commandBuffer?.Release();
    }

    private void OnValidate()
    {
        updateProperties = true;
    }

    private void OnPBFInitialized()
    {
        densityMaterial = new Material(Instantiate(densityShader));
        densityMaterial.SetTexture("densityTex", pbFluid.densityTexture);

        particleMaterial = new Material(particleShader);
        particleMaterial.SetBuffer("particles", pbFluid.foamParticle);
        particleMaterial.SetBuffer("particleCounter", pbFluid.foamParticleCounter);

        CreateDensityMesh();
        CreateFoamParticleMesh();

        updateProperties = true;
    }

    //RENDERING

    private void LateUpdate()
    {
        if (pbFluid.enabled)
        {
            if (updateProperties)
            {
                UpdateProperties();
            }

            pbFluid.UpdateDensityTexture();

            Graphics.DrawMesh(densityMesh, pbFluid.transform.position, Quaternion.identity, densityMaterial, 0);

            particleMaterial.SetVector(pivotPositionProperty, pbFluid.transform.position);
            var renderParams = new RenderParams(particleMaterial)
            {
                worldBounds = new(Vector3.zero, new(10000, 10000, 10000))//better options?
            };
            commandData[0].indexCountPerInstance = particleMesh.GetIndexCount(0);
            commandData[0].instanceCount = (uint)pbFluid.numFoamParticles;//it would be nice if we could set this from the foamParticleCounter but idk how bad it would be to get data from GPU
            commandBuffer.SetData(commandData);
            Graphics.RenderMeshIndirect(in renderParams, particleMesh, commandBuffer);
        }
    }

    private void UpdateProperties()
    {
        densityMaterial.SetColor(colorMinProperty, colorMin.linear);
        densityMaterial.SetColor(colorMaxProperty, colorMax.linear);
        densityMaterial.SetColor(foamColorProperty, foamColor.linear);

        densityMaterial.SetInt(smoothStepIterationsProperty, smoothStepIterations);
        densityMaterial.SetFloat(densityNormalizerProperty, densityNormalizer);
        densityMaterial.SetFloat(noiseNormalizerProperty, noiseNormalizer);
        densityMaterial.SetFloat(noiseThresholdProperty, noiseThreshold);
        densityMaterial.SetFloat(densityThresholdProperty, densityThreshold);
        densityMaterial.SetFloat(foaminessNormalizerProperty, foaminessNormalizer);
        densityMaterial.SetFloat(foaminessThresholdProperty, foaminessThreshold);

        particleMaterial.SetVector(pivotPositionProperty, pbFluid.transform.position);
        particleMaterial.SetColor(particleColorProperty, particleColor.linear);
        particleMaterial.SetFloat(particleRadiusProperty, particleRadius);

        updateProperties = false;
    }

    private void CreateDensityMesh()//we can make it look like a circle in shader
    {
        densityMesh = new();
        float w = pbFluid.width * pbFluid.cellSize;
        float h = pbFluid.height * pbFluid.cellSize;
        var vertices = new Vector3[]
        {
            new(0, 0),
            new(0, h),
            new(w, h),
            new(w, 0)
        };
        var uv = new Vector2[]
        {
            new(0,0), new(0,1), new(1,1), new(1,0)
        };
        var triangles = new int[]
        {
            0, 1, 2, 2, 3, 0
        };

        densityMesh.vertices = vertices;
        densityMesh.uv = uv;
        densityMesh.triangles = triangles;
        densityMesh.RecalculateNormals();
    }

    private void CreateFoamParticleMesh()
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