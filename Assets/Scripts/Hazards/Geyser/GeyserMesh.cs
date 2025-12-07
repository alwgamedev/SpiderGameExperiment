using UnityEngine;

public class GeyserMesh : MonoBehaviour
{
    const string fadeStartProperty = "_FadeStart";
    const string fadeEndProperty = "_FadeEnd";

    [SerializeField] int verticesY;
    [SerializeField] float width0;
    [SerializeField] float width1;
    [SerializeField] float vertexMultiplicity;
    [SerializeField] float height;
    [SerializeField] float fadeHeight;
    [SerializeField] float fadeResetLerpRate;

    Vector3[] vertices;

    Mesh mesh;
    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    Material material;
    Bounds collisionBounds;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        material = new Material(meshRenderer.material);
        meshRenderer.material = material;
    }

    public void DoStart()
    {
        GenerateMesh();
        //^in the future this could slow scene transitions, so we can get the mesh using
        //mesh = meshFilter.mesh (creates copy of mesh...)
        //meshFilter.mesh = mesh
        ResetFade();
        RecalculateCollisionBounds();
    }

    public bool Overlap(Bounds bounds)
    {
        return bounds.Intersects(collisionBounds);
    }


    //MATERIAL FADE

    public void ResetFade()
    {
        material.SetFloat(fadeStartProperty, Mathf.Max(1 - (fadeHeight / height), 0));
        material.SetFloat(fadeEndProperty, 1);
    }

    public void LerpResetFade(float dt)
    {
        var s = material.GetFloat(fadeStartProperty);
        var g = Mathf.Max(1 - (fadeHeight / height), 0);
        if (s < g)
        {
            material.SetFloat(fadeStartProperty, Mathf.Lerp(s, g, fadeResetLerpRate * dt));
        }
        var e = material.GetFloat(fadeEndProperty);
        if (e < 1)
        {
            material.SetFloat(fadeEndProperty, Mathf.Lerp(e, 1, fadeResetLerpRate * dt));
        }
    }

    //fadeEnd is world y-coord
    public void SetFade(float fadeEnd)
    {
        //compute mesh v-coords for fadeStart and fadeEnd
        fadeEnd -= transform.position.y;
        var fadeStart = (fadeEnd - fadeHeight) / height;
        fadeEnd /= height;

        material.SetFloat("_FadeStart", fadeStart);
        material.SetFloat("_FadeEnd", fadeEnd);
    }

    //MESH

    //we could just adjust transform scale y (doesn't seem to affect scale of the particle system child),
    //but I don't really like the idea of that
    public void SetHeight(float y)
    {
        y = Mathf.Max(y, 0.1f);
        var scaleFactor = y / height;
        for (int i = 0; i < vertices.Length; i++)
        {
            var v = vertices[i];
            v.y *= scaleFactor;//this is generally not a good idea due to loss of precision over repeated calls...
            vertices[i] = v;
        }
        mesh.SetVertices(vertices);

        //shader uses bounds so need to update
        height = y;
        var b = mesh.bounds;
        var c = b.center;
        var s = b.size;
        c.y *= scaleFactor;
        s.y *= scaleFactor;
        b.center = c;
        b.size = s;
        mesh.bounds = b;

        RecalculateCollisionBounds();
    }

    public void GenerateMesh()
    {
        mesh = new Mesh();
        /*Vector3[]*/ vertices = new Vector3[2 * verticesY];
        int[] triangles = new int[6 * (verticesY - 1)];
        Vector2[] uv = new Vector2[vertices.Length];

        float dt = 1 / (float)(verticesY - 1);
        float t = 0;

        for (int i = 0; i < verticesY; i++)
        {
            var h = t * height;
            var w = ProfileCurve(t);
            vertices[i] = new(w, h);
            vertices[i + verticesY] = new(-w, h);
            uv[i] = new(0, t);
            uv[i + verticesY] = new(1, t);
            t += dt;
        }

        for (int i = 0; i < verticesY - 1; i++)
        {
            int j = 6 * i;
            triangles[j] = i;
            triangles[++j] = i + 1 + verticesY;
            triangles[++j] = i + 1;
            triangles[++j] = i;
            triangles[++j] = i + verticesY;
            triangles[++j] = i + verticesY + 1;
        }

        mesh.vertices = vertices;//automatically calculates bounds when you first set vertices (shader needs bounds)
        mesh.triangles = triangles;
        mesh.uv = uv;
        mesh.RecalculateNormals();
        if (meshFilter == null)
        {
            meshFilter = GetComponent<MeshFilter>();
        }
        meshFilter.mesh = mesh;
    }

    //t from 0-1 along height
    private float ProfileCurve(float t)
    {
        return (width0 - width1) * Mathf.Pow(t - 1, 2 * vertexMultiplicity) + width1;
    }

    private void RecalculateCollisionBounds()
    {
        collisionBounds = new(transform.position + 0.5f * height * Vector3.up, new Vector3(2 * width1, height));
    }
}
