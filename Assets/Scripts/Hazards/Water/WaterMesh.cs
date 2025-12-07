using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class WaterMesh : MonoBehaviour
{
    [Min(.01f)][SerializeField] float halfWidth;
    [Min(.01f)][SerializeField] float halfHeight;
    [SerializeField] float surfaceColliderBuffer;
    [Min(2)][SerializeField] int numSprings;
    [SerializeField] float springConstant;
    [SerializeField] float dampingFactor;
    [SerializeField] float agitationScale;
    [SerializeField] float agitationPower = 1;//higher power allows you to agitate water more, and throw player less
    [SerializeField] float waveSpreadRate;
    [SerializeField] int waveSmoothingIterations;

    Spring1D[] springs;
    float[] deltas;//i -> springs[i + 1].disp - springs[i].disp

    float width;
    float springSpacing;

    Mesh mesh;
    Vector3[] vertices;
    Vector3 v;

    float CalculateSpringSacing() => 2 * halfWidth / (numSprings - 1);

    private void Start()
    {
        width = 2 * halfWidth;
        springSpacing = CalculateSpringSacing();
        springs = new Spring1D[numSprings];

        for (int i = 0; i < numSprings; i++)
        {
            springs[i] = new();
        }
        deltas = new float[numSprings - 1];

        GenerateMesh();
    }

    private void FixedUpdate()
    {
        UpdateSprings();
        PropagateWaves();
    }

    private void Update()
    {
        UpdateMeshVertices();
    }

    public void AgitateWater(float x, float y, float agitatorHalfWidth, float velocityY)
    {
        x -= transform.position.x - halfWidth;

        if (x < 0 || x > width)
            return;

        int iMin = (int)Mathf.Clamp((x - agitatorHalfWidth) / springSpacing, 0, numSprings - 1);
        int iMax = (int)Mathf.Clamp((x + agitatorHalfWidth) / springSpacing, 0, numSprings - 1);

        velocityY = Mathf.Sign(velocityY) * Mathf.Pow(Mathf.Abs(velocityY), agitationPower);
        velocityY *= agitationScale * Time.deltaTime;

        for (int i = iMin; i <= iMax; i++)
        {
            springs[i].ApplyAcceleration(velocityY);
        }
    }

    //world y-coord of wave at given world x-coord
    public float WaveYPosition(float x)
    {
        x -= transform.position.x - halfWidth;
        if (x < 0 || x > width)
        {
            return transform.position.y - halfHeight;
        }

        float d = x / springSpacing;
        int i = (int)d;
        if (i >= numSprings - 1)
        {
            return transform.position.y + vertices[i].y;
        }
        return transform.position.y +
            Mathf.Lerp(vertices[i].y, vertices[i + 1].y, d - i);
    }

    public void ResizeBoxCollider()
    {
        if (TryGetComponent(out BoxCollider2D b))
        {
            b.size = new(2 * halfWidth, 2 * halfHeight + surfaceColliderBuffer);
            b.offset = 0.5f * surfaceColliderBuffer * Vector2.up;
        }
    }

    public void GenerateMesh()
    {
        var meshFilter = GetComponent<MeshFilter>();

        mesh = new();

        vertices = new Vector3[2 * numSprings];
        var uv = new Vector2[2 * numSprings];
        var triangles = new int[6 * (numSprings - 1)];

        var springSpacing = CalculateSpringSacing();//springSpacing field not yet set if this is called in editor

        for (int i = 0; i < numSprings; i++)
        {
            var x = -halfWidth + i * springSpacing;
            vertices[i] = new(x, halfHeight);
            vertices[i + numSprings] = new(x, -halfHeight);
        }

        for (int i = 0; i < numSprings; i++)
        {
            var x = i / (numSprings - 1);
            uv[i] = new(x, 1);
            uv[numSprings + i] = new(x, 0);
        }

        for (int i = 0; i < numSprings - 1; i++)
        {
            triangles[6 * i] = i;
            triangles[6 * i + 1] = numSprings + i;
            triangles[6 * i + 2] = numSprings + i + 1;
            triangles[6 * i + 3] = numSprings + i + 1;
            triangles[6 * i + 4] = i + 1;
            triangles[6 * i + 5] = i;
            //all triangles oriented CCW
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uv;

        meshFilter.mesh = mesh;
    }


    //SIMULATION
    private void UpdateMeshVertices()
    {
        for (int i = 0; i < numSprings; i++)
        {
            v = vertices[i];
            v.y = halfHeight + springs[i].Displacement;
            vertices[i] = v;
        }

        mesh.SetVertices(vertices);
    }

    private void UpdateSprings()
    {
        foreach (var s in springs)
        {
            s.Update(springConstant, dampingFactor);
        }
    }

    private void PropagateWaves()
    {
        for (int i = 0; i < waveSmoothingIterations; i++)
        {
            for (int j = 1; j < numSprings; j++)
            {
                deltas[j - 1] = waveSpreadRate * (springs[j].Displacement - springs[j - 1].Displacement);

                springs[j - 1].ApplyAcceleration(deltas[j - 1]);
                springs[j].ApplyAcceleration(-deltas[j - 1]);
            }

            for (int j = 1; j < numSprings; j++)
            {
                springs[j - 1].ApplyVelocity(deltas[j - 1]);
                springs[j].ApplyVelocity(-deltas[j - 1]);
            }
        }
    }
}