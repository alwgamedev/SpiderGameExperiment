using UnityEngine;

public class GeyserMesh : MonoBehaviour
{
    [SerializeField] int verticesY;
    [SerializeField] float width0;
    [SerializeField] float width1;
    [SerializeField] float vertexWidth;
    [SerializeField] float vertexMultiplicity;
    [SerializeField] float height;

    Mesh mesh;

    public void GenerateMesh()
    {
        var mf = GetComponent<MeshFilter>();
        mesh = new Mesh();
        Vector3[] vertices = new Vector3[2 * verticesY];
        int[] triangles = new int[6 * (verticesY - 1)];
        Vector2[] uv = new Vector2[vertices.Length];

        var t1 = 0.5f;
        var b0 = (vertexWidth - width0) / Mathf.Pow(t1, 2 * vertexMultiplicity);
        var b1 = (vertexWidth - width1) / Mathf.Pow(1 - t1, 2 * vertexMultiplicity);
        var b = b1 - b0 - 1;
        var t2 = 0.5f * (-b + Mathf.Sqrt(b * b - 4 * b0));
        var t0 = b0 / t2;

        float Curve(float t)//t from 0-1 along mesh height
        {
            return - (t - t0) * Mathf.Pow(t - t1, 2 * vertexMultiplicity) * (t - t2) + vertexWidth;
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
        mf.mesh = mesh;
    }
}
