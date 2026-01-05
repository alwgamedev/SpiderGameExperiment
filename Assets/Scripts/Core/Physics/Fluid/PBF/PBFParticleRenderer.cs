using UnityEngine;

public class PBFParticleRenderer : MonoBehaviour
{
    [SerializeField] PBFluid pbFluid;
    [SerializeField] Mesh particleMesh;
    [SerializeField] Shader particleShader;
    [SerializeField] Color particleColorMin;
    [SerializeField] Color particleColorMax;
    [SerializeField] float particleRadiusMin;
    [SerializeField] float particleRadiusMax;
    [SerializeField] float densityNormalizer;

    Material particleMaterial;
    GraphicsBuffer commandBuffer;
    GraphicsBuffer.IndirectDrawIndexedArgs[] commandData;

    int pivotPositionProperty;
    int particleColorMinProperty;
    int particleColorMaxProperty;
    int particleRadiusMinProperty;
    int particleRadiusMaxProperty;
    int densityNormalizerProperty;

    bool updateMaterialProperties;

    private void Awake()
    {
        pivotPositionProperty = Shader.PropertyToID("pivotPosition");
        particleColorMinProperty = Shader.PropertyToID("particleColorMin");
        particleColorMaxProperty = Shader.PropertyToID("particleColorMax");
        particleRadiusMinProperty = Shader.PropertyToID("particleRadiusMin");
        particleRadiusMaxProperty = Shader.PropertyToID("particleRadiusMax");
        densityNormalizerProperty = Shader.PropertyToID("densityNormalizer");
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
        //only needs to be done once
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

    private void OnPBFInitialized()
    {
        particleMaterial = new Material(particleShader);

        //bind buffers to material
        particleMaterial.SetBuffer("particleDensity", pbFluid.density);
        particleMaterial.SetBuffer("particleVelocity", pbFluid.velocity);
        particleMaterial.SetBuffer("particlePosition", pbFluid.position);

        if (particleMesh == null)
        {
            CreateBoxMesh();
        }

        updateMaterialProperties = true;
    }

    private void OnValidate()
    {
        updateMaterialProperties = true;
    }

    //RENDERING

    private void LateUpdate()
    {
        if (pbFluid.enabled)
        {
            if (updateMaterialProperties)
            {
                UpdateMaterialProperties();
            }

            particleMaterial.SetVector(pivotPositionProperty, pbFluid.transform.position);

            var renderParams = new RenderParams(particleMaterial)
            {
                worldBounds = new(Vector3.zero, new(10000, 10000, 10000))//better options?
            };
            commandData[0].indexCountPerInstance = particleMesh.GetIndexCount(0);
            commandData[0].instanceCount = (uint)pbFluid.configuration.numParticles;
            commandBuffer.SetData(commandData);
            Graphics.RenderMeshIndirect(in renderParams, particleMesh, commandBuffer);
            //there's also RenderPrimitives and RenderMeshPrimitives?
            //you should test if there's a significant difference in performance for us
        }
    }

    private void UpdateMaterialProperties()
    {
        particleMaterial.SetColor(particleColorMinProperty, particleColorMin.linear);
        particleMaterial.SetColor(particleColorMaxProperty, particleColorMax.linear);
        particleMaterial.SetFloat(particleRadiusMinProperty, particleRadiusMin);
        particleMaterial.SetFloat(particleRadiusMaxProperty, particleRadiusMax);
        particleMaterial.SetFloat(densityNormalizerProperty, densityNormalizer);

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