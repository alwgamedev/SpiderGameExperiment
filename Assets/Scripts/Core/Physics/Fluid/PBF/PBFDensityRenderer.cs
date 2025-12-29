using UnityEngine;

public class PBFDensityRenderer : MonoBehaviour
{
    [SerializeField] PBFluid pbFluid;

    [Header("Render Settings")]
    [SerializeField] Shader shader;
    [SerializeField] Color color;
    [SerializeField] float densityNormalizer;

    [Header("Density Texture")]
    [SerializeField] int texWidth;
    [SerializeField] int texHeight;
    [SerializeField] float smoothingRadius;

    Mesh mesh;
    Material material;

    RenderTexture densityTexture;

    int threadGroupsX;
    int threadGroupsY;

    int colorProperty;
    int densityNormalizerProperty;
    int texWidthProperty;
    int texHeightProperty;
    int texelSizeXProperty;
    int texelSizeYProperty;
    int smoothingRadiusProperty;
    int smoothingRadiusSqrdProperty;

    bool updateMaterialProperties;

    private void Awake()
    {
        colorProperty = Shader.PropertyToID("color");
        densityNormalizerProperty = Shader.PropertyToID("densityNormalizer");
        texWidthProperty = Shader.PropertyToID("texWidth");
        texHeightProperty = Shader.PropertyToID("texHeight");
        texelSizeXProperty = Shader.PropertyToID("texelSizeX");
        texelSizeYProperty = Shader.PropertyToID("texelSizeY");
        smoothingRadiusProperty = Shader.PropertyToID("densityTexSmoothingRadius");
        smoothingRadiusSqrdProperty = Shader.PropertyToID("densityTexSmoothingRadiusSqrd");
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

        if (densityTexture != null)
        {
            densityTexture.Release();
            Destroy(densityTexture);
        }
    }

    private void OnPBFInitialized()
    {
        material = new Material(shader);

        densityTexture = new(texWidth, texHeight, 0)
        {
            graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16_SFloat,
            dimension = UnityEngine.Rendering.TextureDimension.Tex2D,
            enableRandomWrite = true
        };

        pbFluid.computeShader.SetInt(texWidthProperty, texWidth);
        pbFluid.computeShader.SetInt(texHeightProperty, texHeight);
        float w = pbFluid.width * pbFluid.cellSize;
        float h = pbFluid.height * pbFluid.cellSize;
        pbFluid.computeShader.SetFloat(texelSizeXProperty, w / texWidth);
        pbFluid.computeShader.SetFloat(texelSizeYProperty, h / texHeight);

        material.SetTexture("densityTex", densityTexture);
        pbFluid.computeShader.SetTexture(PBFluid.updateDensityTexture, "densityTex", densityTexture);

        threadGroupsX = (int)Mathf.Ceil(texWidth / 16f);
        threadGroupsY = (int)Mathf.Ceil(texHeight / 16f);

        CreateMesh();

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
                UpdateProperties();
            }

            pbFluid.UpdateDensityTexture(threadGroupsX, threadGroupsY);

            Graphics.DrawMesh(mesh, pbFluid.transform.position, Quaternion.identity, material, 0);
        }
    }

    private void UpdateProperties()
    {
        material.SetColor(colorProperty, color);
        material.SetFloat(densityNormalizerProperty, densityNormalizer);

        pbFluid.computeShader.SetFloat(smoothingRadiusProperty, smoothingRadius);
        pbFluid.computeShader.SetFloat(smoothingRadiusSqrdProperty, smoothingRadius * smoothingRadius);

        updateMaterialProperties = false;
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