using System;
using UnityEngine;

[Serializable]
struct ColorTuple
{
    public Color c0;
    public Color c1;
}

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
    [SerializeField] float maxThresholdMult;

    [Header("Foam Particle Settings")]
    [SerializeField] Shader particleShader;
    [SerializeField] float lifeFadeTime;
    [SerializeField] float velocityStretchFactor;
    [SerializeField] float velocityStretchMax;
    [SerializeField] ColorTuple particleColorMin;
    [SerializeField] ColorTuple particleColorSpray;
    [SerializeField] ColorTuple particleColorFoam;
    [SerializeField] ColorTuple particleColorBubble;
    [SerializeField] ColorTuple particleColorMax;
    [SerializeField] Vector2 particleRadiusMin;
    [SerializeField] Vector2 particleRadiusSpray;
    [SerializeField] Vector2 particleRadiusFoam;
    [SerializeField] Vector2 particleRadiusBubble;
    [SerializeField] Vector2 particleRadiusMax;

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
    int dtProperty;
    int velocityStretchFactorProperty;
    int velocityStretchMaxProperty;
    int particleColorMin0Property;
    int particleColorMin1Property;
    int particleColorSpray0Property;
    int particleColorSpray1Property;
    int particleColorFoam0Property;
    int particleColorFoam1Property;
    int particleColorBubble0Property;
    int particleColorBubble1Property;
    int particleColorMax0Property;
    int particleColorMax1Property;
    int particleRadiusMinProperty;
    int particleRadiusSprayProperty;
    int particleRadiusFoamProperty;
    int particleRadiusBubbleProperty;
    int particleRadiusMaxProperty;

    int sprayThresholdProperty;
    int foamThresholdProperty;
    int bubbleThresholdProperty;
    int densityMaxProperty;
    int lifeFadeTimeProperty;

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
        dtProperty = Shader.PropertyToID("dt");
        velocityStretchFactorProperty = Shader.PropertyToID("velocityStretchFactor");
        velocityStretchMaxProperty = Shader.PropertyToID("velocityStretchMax");
        particleColorMin0Property = Shader.PropertyToID("particleColorMin0");
        particleColorMin1Property = Shader.PropertyToID("particleColorMin1");
        particleColorSpray0Property = Shader.PropertyToID("particleColorSpray0");
        particleColorSpray1Property = Shader.PropertyToID("particleColorSpray1");
        particleColorFoam0Property = Shader.PropertyToID("particleColorFoam0");
        particleColorFoam1Property = Shader.PropertyToID("particleColorFoam1");
        particleColorBubble0Property = Shader.PropertyToID("particleColorBubble0");
        particleColorBubble1Property = Shader.PropertyToID("particleColorBubble1");
        particleColorMax0Property = Shader.PropertyToID("particleColorMax0");
        particleColorMax1Property = Shader.PropertyToID("particleColorMax1");
        particleRadiusMinProperty = Shader.PropertyToID("particleRadiusMin");
        particleRadiusSprayProperty = Shader.PropertyToID("particleRadiusSpray");
        particleRadiusFoamProperty = Shader.PropertyToID("particleRadiusFoam");
        particleRadiusBubbleProperty = Shader.PropertyToID("particleRadiusBubble");
        particleRadiusMaxProperty = Shader.PropertyToID("particleRadiusMax");

        sprayThresholdProperty = Shader.PropertyToID("sprayThreshold");
        foamThresholdProperty = Shader.PropertyToID("foamThreshold");
        bubbleThresholdProperty = Shader.PropertyToID("bubbleThreshold");
        densityMaxProperty = Shader.PropertyToID("densityMax");
        lifeFadeTimeProperty = Shader.PropertyToID("lifeFadeTime");
    }

    private void OnEnable()
    {
        pbFluid.Initialized += OnPBFInitialized;
        pbFluid.SettingsChanged += OnPBFSettingsChanged;

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
            pbFluid.SettingsChanged -= OnPBFSettingsChanged;
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
        particleMaterial.SetBuffer("particle", pbFluid.foamParticle);
        particleMaterial.SetBuffer("particleCounter", pbFluid.foamParticleCounter);

        CreateDensityMesh();
        CreateFoamParticleMesh();

        updateProperties = true;
    }

    private void OnPBFSettingsChanged()
    {
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

            particleMaterial.SetVector(pivotPositionProperty, pbFluid.transform.position);
            particleMaterial.SetFloat(dtProperty, Time.deltaTime);

            pbFluid.UpdateDensityTexture();

            Graphics.DrawMesh(densityMesh, pbFluid.transform.position, Quaternion.identity, densityMaterial, 0);
            var renderParams = new RenderParams(particleMaterial)
            {
                worldBounds = new(Vector3.zero, new(10000, 10000, 10000))//better options?
            };
            commandData[0].indexCountPerInstance = particleMesh.GetIndexCount(0);
            commandData[0].instanceCount = (uint)pbFluid.configuration.numFoamParticles;//it would be nice if we could set this from the foamParticleCounter but idk how bad it would be to get data from GPU
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

        particleMaterial.SetFloat(velocityStretchFactorProperty, velocityStretchFactor);
        particleMaterial.SetFloat(velocityStretchMaxProperty, velocityStretchMax);
        particleMaterial.SetFloat(sprayThresholdProperty, pbFluid.foamParticleSettings.sprayThreshold);
        particleMaterial.SetFloat(foamThresholdProperty, pbFluid.foamParticleSettings.foamThreshold);
        particleMaterial.SetFloat(bubbleThresholdProperty, pbFluid.foamParticleSettings.bubbleThreshold);
        particleMaterial.SetFloat(densityMaxProperty, maxThresholdMult * pbFluid.simSettings.restDensity);
        particleMaterial.SetFloat(lifeFadeTimeProperty, lifeFadeTime);

        particleMaterial.SetColor(particleColorMin0Property, particleColorMin.c0.linear);
        particleMaterial.SetColor(particleColorMin1Property, particleColorMin.c1.linear);
        particleMaterial.SetColor(particleColorSpray0Property, particleColorSpray.c0.linear);
        particleMaterial.SetColor(particleColorSpray1Property, particleColorSpray.c1.linear);
        particleMaterial.SetColor(particleColorFoam0Property, particleColorFoam.c0.linear);
        particleMaterial.SetColor(particleColorFoam1Property, particleColorFoam.c1.linear);
        particleMaterial.SetColor(particleColorBubble0Property, particleColorBubble.c0.linear);
        particleMaterial.SetColor(particleColorBubble1Property, particleColorBubble.c1.linear);
        particleMaterial.SetColor(particleColorMax0Property, particleColorMax.c0.linear);
        particleMaterial.SetColor(particleColorMax1Property, particleColorMax.c1.linear);

        particleMaterial.SetVector(particleRadiusMinProperty, particleRadiusMin);
        particleMaterial.SetVector(particleRadiusSprayProperty, particleRadiusSpray);
        particleMaterial.SetVector(particleRadiusFoamProperty, particleRadiusFoam);
        particleMaterial.SetVector(particleRadiusBubbleProperty, particleRadiusBubble);
        particleMaterial.SetVector(particleRadiusMaxProperty, particleRadiusMax); 

        updateProperties = false;
    }

    private void CreateDensityMesh()//we can make it look like a circle in shader
    {
        densityMesh = new();
        float w = pbFluid.configuration.width * pbFluid.simSettings.cellSize;
        float h = pbFluid.configuration.height * pbFluid.simSettings.cellSize;
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