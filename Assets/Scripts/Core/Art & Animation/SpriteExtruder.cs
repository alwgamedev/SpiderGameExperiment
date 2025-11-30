using UnityEngine;

public class SpriteExtruder : MonoBehaviour
{
    [SerializeField] Sprite sprite;
    [SerializeField] MeshFilter meshFilter;
    [SerializeField] Vector3 extrusion;

    public void GenerateMesh()
    {
        var spriteVertices = sprite.vertices;
        var spriteUV = sprite.uv;
        var spriteTriangles = sprite.triangles;

        Vector3[] vertices = new Vector3[2 * spriteVertices.Length];
        Vector2[] uv = new Vector2[vertices.Length];
        int[] triangles = new int[8 * spriteTriangles.Length];

        //vertices order: all front vertices, then all back vertices
        for (int i = 0; i < spriteVertices.Length; i++)
        {
            vertices[i] = spriteVertices[i];
            vertices[spriteVertices.Length + i] = (Vector3)spriteVertices[i] + extrusion;
        }

        for (int i = 0; i < spriteTriangles.Length / 3; i++)
        {
            var j = 24 * i;
            var front0 = spriteTriangles[3 * i];
            var front1 = spriteTriangles[3 * i + 1];
            var front2 = spriteTriangles[3 * i + 2];
            var back0 = front0 + spriteVertices.Length;
            var back1 = front1 + spriteVertices.Length;
            var back2 = front2 + spriteVertices.Length;
            //front triangle
            triangles[j] = front0;//corresponds to a vertex in the first half of new vertices array, which comes from the original sprite vertices
            triangles[++j] = front1;
            triangles[++j] = front2;
            //back triangle
            triangles[++j] = back0;
            triangles[++j] = back2;
            triangles[++j] = back1;
            //quad extruding 01 edge of front triangle
            triangles[++j] = front0;
            triangles[++j] = back0;
            triangles[++j] = back1;
            triangles[++j] = front0;
            triangles[++j] = back1;
            triangles[++j] = front1;
            //quad extruding 12 edge of front triangle
            triangles[++j] = front1;
            triangles[++j] = back1;
            triangles[++j] = back2;
            triangles[++j] = front1;
            triangles[++j] = back2;
            triangles[++j] = front2;
            //quad extruding 20 edge of front triangle
            triangles[++j] = front2;
            triangles[++j] = back2;
            triangles[++j] = back0;
            triangles[++j] = front2;
            triangles[++j] = back0;
            triangles[++j] = front0;
        }

        for (int i = 0; i < spriteUV.Length; i++)
        {
            uv[i] = spriteUV[i];
            uv[i + spriteUV.Length] = spriteUV[i];
        }

        var mesh = new Mesh
        {
            vertices = vertices,
            triangles = triangles,
            uv = uv
        };
        mesh.RecalculateNormals();
        meshFilter.mesh = mesh;
    }
}