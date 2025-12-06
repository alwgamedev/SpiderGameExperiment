using UnityEngine;

public class GeyserMesh : MonoBehaviour
{
    [SerializeField] int verticesY;
    [SerializeField] float width0;
    [SerializeField] float width1;
    [SerializeField] float vertexMultiplicity;
    [SerializeField] float height;
    [SerializeField] float fadeHeight;
    [SerializeField] float fadeResetLerpRate;

    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    Material material;

    ParticleSystem particles;

    Collider2D playerCollider;
    Bounds geyserBounds;
    bool playerWasInContact;

    private void Start()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        material = new Material(meshRenderer.material);
        meshRenderer.material = material;
        particles = GetComponentInChildren<ParticleSystem>();
        playerCollider = Spider.Player.GetComponent<Collider2D>();

        EnableParticleCollision(false);
        ResetFade();
        CaptureBounds();
    }

    private void Update()
    {
        if (PlayerMakingContact())
        {
            if (!playerWasInContact)
            {
                EnableParticleCollision(true);
                playerWasInContact = true;
            }
            UpdateFade();
        }
        else
        {
            if (playerWasInContact)
            {
                playerWasInContact = false;
                EnableParticleCollision(false);
            }
            LerpResetFade();
        }
    }

    public void GenerateMesh()
    {
        var mesh = new Mesh();
        Vector3[] vertices = new Vector3[2 * verticesY];
        int[] triangles = new int[6 * (verticesY - 1)];
        Vector2[] uv = new Vector2[vertices.Length];

        //var t1 = 0.5f;
        //var b0 = (vertexWidth - width0) / Mathf.Pow(t1, 2 * vertexMultiplicity);
        //var b1 = (vertexWidth - width1) / Mathf.Pow(1 - t1, 2 * vertexMultiplicity);
        //var b = b1 - b0 - 1;
        //var t2 = 0.5f * (-b + Mathf.Sqrt(b * b - 4 * b0));
        //var t0 = b0 / t2;

        //float Curve(float t)//t from 0-1 along mesh height
        //{
        //    return - (t - t0) * Mathf.Pow(t - t1, 2 * vertexMultiplicity) * (t - t2) + vertexWidth;
        //}

        float Curve(float t)
        {
            return (width0 - width1) * Mathf.Pow(t - 1, 2 * vertexMultiplicity) + width1;
        }

        float dt = 1 / (float)(verticesY - 1);
        float t = 0;

        for (int i = 0; i < verticesY; i++)
        {
            var h = t * height;
            var w = Curve(t);
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

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uv;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        if (meshFilter == null)
        {
            meshFilter = GetComponent<MeshFilter>();
        }
        meshFilter.mesh = mesh;
    }

    private void UpdateFade()
    {
        SetFade(playerCollider.bounds.min.y);
    }

    private void ResetFade()
    {
        material.SetFloat("_FadeStart", 1 - (fadeHeight / height));
        material.SetFloat("_FadeEnd", 1);
    }

    private void LerpResetFade()
    {
        var s = material.GetFloat("_FadeStart");
        var g = 1 - (fadeHeight / height); 
        if (s < g)
        {
            material.SetFloat("_FadeStart", Mathf.Lerp(s, 1 - g, fadeResetLerpRate * Time.deltaTime));
        }
        var e = material.GetFloat("_FadeEnd");
        if (e < 1)
        {
            material.SetFloat("_FadeEnd", Mathf.Lerp(e, 1, fadeResetLerpRate * Time.deltaTime));
        }
    }

    //fadeEnd is world y-coord
    private void SetFade(float fadeEnd)
    {
        fadeEnd = Mathf.Max(fadeEnd - transform.position.y, 0);
        var fadeStart = Mathf.Max(fadeEnd - fadeHeight, 0);
        fadeEnd /= height;
        fadeStart /= height;
        material.SetFloat("_FadeStart", fadeStart);
        material.SetFloat("_FadeEnd", fadeEnd);
    }

    private bool PlayerMakingContact()
    {
        return playerCollider.bounds.Intersects(geyserBounds);
    }

    private void EnableParticleCollision(bool val)
    {
        var c = particles.collision;
        c.enabled = val;
    }

    private void CaptureBounds()
    {
        geyserBounds = new(transform.position + 0.5f * height * Vector3.up, new Vector3(2 * width1, height));
    }
}
