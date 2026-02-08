using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.U2D;

public class SpriteShapeMeshGenerator : MonoBehaviour
{
    public Mesh mesh;

    [SerializeField] SpriteShapeController spriteShapeController;
    [SerializeField] float sampleRate;//number of sample points per unit of distance between sprite shape vertices
    //[SerializeField] List<Vector3> vertices;

    //private void OnDrawGizmos()
    //{
    //    if (vertices != null && vertices.Count > 1)
    //    {
    //        for (int i = 1; i < vertices.Count; i++)
    //        {
    //            Gizmos.color = Color.Lerp(Color.yellow, Color.red, (float)i / vertices.Count);
    //            Gizmos.DrawLine(vertices[i - 1], vertices[i]);
    //        }
    //        Gizmos.DrawLine(vertices[^1], vertices[0]);
    //    }
    //}

    public void GenerateMesh()
    {
        var spline = spriteShapeController.spline;



        //VERTICES

        var vertices = new List<Vector3>();

        int upperRightIndex = 0;
        float upperRight = -Mathf.Infinity;
        int lowerRightIndex = 0;
        float lowerRight = -Mathf.Infinity;
        int lowerLeftIndex = 0;
        float lowerLeft = -Mathf.Infinity;
        int upperLeftIndex = 0;
        float upperLeft = -Mathf.Infinity;
        var ne = new Vector2(1, 1);
        var se = new Vector2(1, -1);
        var nw = new Vector2(-1, 1);
        var sw = new Vector2(-1, -1);

        var numPoints = spline.GetPointCount();
        for (int i = 0; i < numPoints; i++)
        {

            var i1 = (i + 1) % numPoints;
            var p = spline.GetPosition(i);//already local position
            var q = spline.GetPosition(i1);
            int numSubPoints = (int)Mathf.Ceil(Vector2.Distance(p, q) * sampleRate);

            for (int j = 0; j < numSubPoints; j++)
            {
                var s = (float)j / numSubPoints;
                var p1 = BezierUtility.BezierPoint(p + spline.GetRightTangent(i), p, q, q + spline.GetLeftTangent(i1), s);
                vertices.Add(p1);

                var a = Vector2.Dot(p1, ne);
                if (a > upperRight)
                {
                    upperRightIndex = vertices.Count - 1;
                    upperRight = a;
                }

                a = Vector2.Dot(p1, se);
                if (a > lowerRight)
                {
                    lowerRightIndex = vertices.Count - 1;
                    lowerRight = a;
                }

                a = Vector2.Dot(p1, sw);
                if (a > lowerLeft)
                {
                    lowerLeftIndex = vertices.Count - 1;
                    lowerLeft = a;
                }

                a = Vector2.Dot(p1, nw);
                if (a > upperLeft)
                {
                    upperLeftIndex = vertices.Count - 1;
                    upperLeft = a;
                }
            }
        }


        //UV
        var uv = new Vector2[vertices.Count];
        Vector2[] uvCorner = new Vector2[] { new(0, 0), new(1, 0), new(1, 1), new(0, 1) };
        int[] uvCornerVertex = new int[] { lowerLeftIndex, lowerRightIndex, upperRightIndex, upperLeftIndex };
        float[] meshEdgeLength = new float[vertices.Count];
        bool ccw = true;//does going through the vertices in order traverse the polygon ccw or cw (polygon assumed to live in xy plane)

        var uvEdgeStart = 0;
        for (int i = 0; i < 4; i++)
        {
            var totalMeshEdgeLength = 0f;
            var startVertex = uvCornerVertex[uvEdgeStart];
            var uvEdgeEnd = -1;

            var next = uvEdgeStart + 1;
            if (next == 4)
            {
                next = 0;
            }
            var prev = uvEdgeStart - 1;
            if (prev == -1)
            {
                prev = 3;
            }

            for (int j = 0; j < vertices.Count; j++)
            {
                int k = (startVertex + j) % vertices.Count;

                if (k == uvCornerVertex[next])
                {
                    uvEdgeEnd = next;
                    break;
                }
                
                if (k == uvCornerVertex[prev])
                {
                    if (k != startVertex)
                    {
                        ccw = false;
                    }
                    uvEdgeEnd = prev;
                    break;
                }

                var l = Vector2.Distance(vertices[k], vertices[(k + 1) % vertices.Count]);
                meshEdgeLength[k] = l;
                totalMeshEdgeLength += l;
            }

            if (uvEdgeEnd < 0)
            {
                Debug.Log("Unable to generate UVs for mesh (uv vertices were not in sequential order).");
                return;
            }

            int endVertex = uvCornerVertex[uvEdgeEnd];
            Vector2 uv0 = uvCorner[uvEdgeStart];
            Vector2 d = uvCorner[uvEdgeEnd] - uv0;
            uv[startVertex] = uv0;
            int vertex = startVertex;
            while (vertex != endVertex)
            {
                uv0 += meshEdgeLength[vertex] / totalMeshEdgeLength * d;
                vertex = (vertex + 1) % vertices.Count;
                uv[vertex] = uv0;
            }

            uvEdgeStart = uvEdgeEnd;
        }

        //TRIANGLES

        List<int> verticesRemaining = Enumerable.Range(0, vertices.Count).ToList();
        var triangles = new int[3 * (vertices.Count - 2)];

        int t = 0;
        while (t < triangles.Length)
        {
            int j = Random.Range(0, verticesRemaining.Count);
            bool foundConvexVertex = false;

            for (int i = 0; i < verticesRemaining.Count; i++)
            {
                int i0 = (j + i) % verticesRemaining.Count;
                int i1 = (j + i + 1) % verticesRemaining.Count;
                int i2 = (j + i + 2) % verticesRemaining.Count;

                var v0 = verticesRemaining[i0];
                var v1 = verticesRemaining[i1];
                var v2 = verticesRemaining[i2];
                Vector2 w1 = vertices[v0] - vertices[v1];
                Vector2 w2 = vertices[v2] - vertices[v1];
                var c = ccw ? MathTools.Cross2D(w1, w2) : -MathTools.Cross2D(w1, w2);
                if (c > 0)
                {
                    //concave vertex
                    continue;
                }

                //triangle may hit exterior of polygon, once we've removed a lot of vertices. slow af to check but we don't care
                bool badTriangle = false;
                var n0 = ccw ? ((Vector2)(vertices[v1] - vertices[v0])).CCWPerp() : ((Vector2)(vertices[v1] - vertices[v0])).CWPerp();
                var p0 = vertices[v0];
                var n1 = ccw ? ((Vector2)(vertices[v2] - vertices[v1])).CCWPerp() : ((Vector2)(vertices[v2] - vertices[v1])).CWPerp();
                var p1 = vertices[v1];
                var n2 = ccw ? ((Vector2)(vertices[v0] - vertices[v2])).CCWPerp() : ((Vector2)(vertices[v0] - vertices[v2])).CWPerp();
                var p2 = vertices[v2];
                for (int v = 0; v < vertices.Count; v++)
                {
                    var p = vertices[v];
                    if (Vector2.Dot(p - p0, n0) > 0 && Vector2.Dot(p - p1, n1) > 0 && Vector2.Dot(p - p2, n2) > 0)
                    {
                        badTriangle = true;
                        break;
                    }
                }

                if (badTriangle)
                {
                    continue;
                }

                //triangles need to be CW bc that's the convention unity uses
                if (ccw)
                {
                    triangles[t++] = v0;
                    triangles[t++] = v2;
                    triangles[t++] = v1;
                }
                else
                {
                    triangles[t++] = v0;
                    triangles[t++] = v1;
                    triangles[t++] = v2;
                }

                verticesRemaining.RemoveAt(i1);
                foundConvexVertex = true;
                break;
            }

            if (!foundConvexVertex)
            {
                Debug.Log("Generating triangles failed.");//shouldn't happen anymore
                return;
            }
        }

        //HEIGHT MAP -- suck

        //var heightMap = new Vector2[vertices.Count];//to be stored into mesh.uv2 for use in shaders
        //for (int i = 0; i < vertices.Count - 1; i++)
        //{
        //    Vector2 w1 = (vertices[i] - vertices[i + 1]).normalized;
        //    Vector2 w2 = (vertices[(i + 2) % vertices.Count] - vertices[i + 1]).normalized;
        //    var c = ccw ? MathTools.Cross2D(w1, w2) : -MathTools.Cross2D(w1, w2);//c > 0 means concave vertex
        //    c = 0.5f * (c + 1);
        //    w2 = Mathf.Sign(c) * 0.5f * (w1 + w2).normalized;//outward "normal" at the corner
        //    heightMap[i] = new(0.5f * (c + 1), 0.5f * (1 - w2.y));
        //}

        //create mesh

        mesh = new();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uv);
        //mesh.SetUVs(1, heightMap);
        mesh.RecalculateNormals();
    }
}