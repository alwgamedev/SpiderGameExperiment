using UnityEngine;
using Unity.Mathematics;

public class PBFDensityRenderer : MonoBehaviour
{
    [SerializeField] PBFluid pbFluid;

    [Header("Material Settings")]
    [SerializeField] Shader shader;
    [SerializeField] Color colorMin;
    [SerializeField] Color colorMax;
    [SerializeField] float normalizer;
    [SerializeField] float noiseNormalizer;
    [SerializeField] float threshold;

    Mesh mesh;
    Material material;

    int colorMinProperty;
    int colorMaxProperty;
    int normalizerProperty;
    int noiseNormalizerProperty;
    int thresholdProperty;

    bool updateProperties;

    private void Awake()
    {
        colorMinProperty = Shader.PropertyToID("colorMin");
        colorMaxProperty = Shader.PropertyToID("colorMax");
        normalizerProperty = Shader.PropertyToID("normalizer");
        noiseNormalizerProperty = Shader.PropertyToID("noiseNormalizer");
        thresholdProperty = Shader.PropertyToID("threshold");
    }

    private void OnEnable()
    {
        pbFluid.Initialized += OnPBFInitialized;

        if (pbFluid.enabled)
        {
            OnPBFInitialized();
        }
    }

    private void OnDisable()
    {
        if (pbFluid != null)
        {
            pbFluid.Initialized -= OnPBFInitialized;
        }
    }

    private void OnValidate()
    {
        updateProperties = true;
    }

    private void OnPBFInitialized()
    {
        material = new Material(shader);
        material.SetTexture("densityTex", pbFluid.densityTexture);

        CreateMesh();

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

            Graphics.DrawMesh(mesh, pbFluid.transform.position, Quaternion.identity, material, 0);
        }
    }

    private void UpdateProperties()
    {
        material.SetColor(colorMinProperty, colorMin);
        material.SetColor(colorMaxProperty, colorMax);
        material.SetFloat(normalizerProperty, normalizer);
        material.SetFloat(noiseNormalizerProperty, noiseNormalizer);
        material.SetFloat(thresholdProperty, threshold);

        updateProperties = false;
    }

    private void CreateMesh()//we can make it look like a circle in shader
    {
        mesh = new();
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

        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
    }
}