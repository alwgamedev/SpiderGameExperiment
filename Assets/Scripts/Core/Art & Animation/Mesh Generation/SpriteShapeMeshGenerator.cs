using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;

[ExecuteInEditMode]
public class SpriteShapeMeshGenerator : MonoBehaviour
{
    public Mesh mesh;

    [SerializeField] SpriteShapeController spriteShapeController;
    [SerializeField] MeshFilter meshFilter;
    [SerializeField] int arcLengthSamples;
    [SerializeField] float splineSampleRate;//number of sample points per unit of distance between sprite shape vertices
    [SerializeField] float maxTriangleArea;
    [SerializeField] int geomSmoothingIterations;
    [SerializeField] float geomSmoothingWeight;
    [SerializeField] float shadowDistanceMax;
    [SerializeField] float shadowCastIncrement;

    [Header("Refinement Settings")]
    [SerializeField] float minAngleRad;
    [SerializeField] int refinementIterations;
    [SerializeField] int repairIterations;
    [SerializeField] float boundingBoxSize = 3;

    public void GenerateMesh()
    {
        var spline = spriteShapeController.spline;

        //VERTICES

        var vertices = new List<Vector3>();//use list bc we don't know how many vertices yet, since we're subdividing sprite shape vertices

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
            var pRightTangent = p + spline.GetRightTangent(i);
            var q = spline.GetPosition(i1);
            var qLeftTangent = q + spline.GetLeftTangent(i1);

            var arcLength = 0f;
            var p0 = p;
            for (int j = 1; j < arcLengthSamples + 1; j++)
            {
                var p1 = BezierUtility.BezierPoint(pRightTangent, p, q, qLeftTangent, (float)j / arcLengthSamples);
                arcLength += Vector2.Distance(p0, p1);
                p0 = p1;
            }

            int numSubPoints = (int)Mathf.Ceil(arcLength * splineSampleRate);

            for (int j = 0; j < numSubPoints; j++)
            {
                var s = (float)j / numSubPoints;
                var p1 = BezierUtility.BezierPoint(pRightTangent, p, q, qLeftTangent, s);
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


        //UV -- we could just do a simple bounding box uv instead

        var uv = new Vector2[vertices.Count];
        Vector2[] uvCorner = new Vector2[] { new(0, 0), new(1, 0), new(1, 1), new(0, 1) };
        int[] uvCornerVertex = new int[] { lowerLeftIndex, lowerRightIndex, upperRightIndex, upperLeftIndex };
        float[] meshEdgeLength = new float[vertices.Count];
        //bool ccw = true;//does going through the vertices in order traverse the polygon ccw or cw (polygon assumed to live in xy plane)

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
                    //if (k != startVertex)
                    //{
                    //    ccw = false;
                    //}
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
        //this approach sucks and produces a lot of shitty little sliver triangles
        //we'll switch to Delaunay triangulation, hopefully for more even triangulation

        var triangles = Triangulator.TriangulatePolygon(vertices, Mathf.PI / 6, refinementIterations, repairIterations, boundingBoxSize);

        int t = 0;
        while (t < triangles.Count)
        {
            var t0 = triangles[t++];
            var t1 = triangles[t++];
            var t2 = triangles[t++];

            var p0 = spriteShapeController.transform.TransformPoint(vertices[t0]);
            var p1 = spriteShapeController.transform.TransformPoint(vertices[t1]);
            var p2 = spriteShapeController.transform.TransformPoint(vertices[t2]);

            Debug.DrawLine(p0, p1, Color.red, 10);
            Debug.DrawLine(p1, p2, Color.red, 10);
            Debug.DrawLine(p2, p0, Color.red, 10);
        }

        //List<int> verticesRemaining = Enumerable.Range(0, vertices.Count).ToList();
        //var triangles = new int[3 * (vertices.Count - 2)];

        //int t = 0;
        //while (t < triangles.Length)
        //{
        //    int j = Random.Range(0, verticesRemaining.Count);//add some randomness to the triangulation
        //    bool goDown = j % 2 == 0;
        //    bool convexVertexFound = false;

        //    for (int i = 0; i < verticesRemaining.Count; i++)
        //    {
        //        var k = goDown ? j - i : j + i;
        //        if (k < 0)
        //        {
        //            k += verticesRemaining.Count;
        //        }
        //        int i0 = k % verticesRemaining.Count;
        //        int i1 = (k + 1) % verticesRemaining.Count;
        //        int i2 = (k + 2) % verticesRemaining.Count;

        //        var v0 = verticesRemaining[i0];
        //        var v1 = verticesRemaining[i1];
        //        var v2 = verticesRemaining[i2];
        //        Vector2 w1 = vertices[v0] - vertices[v1];
        //        Vector2 w2 = vertices[v2] - vertices[v1];
        //        var c = ccw ? MathTools.Cross2D(w1, w2) : -MathTools.Cross2D(w1, w2);

        //        if (c > 0)
        //        {
        //            //concave vertex
        //            continue;
        //        }

        //        //triangle may hit exterior of polygon, once we've removed a lot of vertices. slow af to check but we don't care
        //        bool badTriangle = false;
        //        var n0 = ccw ? ((Vector2)(vertices[v1] - vertices[v0])).CCWPerp() : ((Vector2)(vertices[v1] - vertices[v0])).CWPerp();
        //        var p0 = vertices[v0];
        //        var n1 = ccw ? ((Vector2)(vertices[v2] - vertices[v1])).CCWPerp() : ((Vector2)(vertices[v2] - vertices[v1])).CWPerp();
        //        var p1 = vertices[v1];
        //        var n2 = ccw ? ((Vector2)(vertices[v0] - vertices[v2])).CCWPerp() : ((Vector2)(vertices[v0] - vertices[v2])).CWPerp();
        //        var p2 = vertices[v2];
        //        for (int v = 0; v < vertices.Count; v++)
        //        {
        //            var p = vertices[v];
        //            if (Vector2.Dot(p - p0, n0) > 0 && Vector2.Dot(p - p1, n1) > 0 && Vector2.Dot(p - p2, n2) > 0)
        //            {
        //                badTriangle = true;
        //                break;
        //            }
        //        }

        //        if (badTriangle)
        //        {
        //            continue;
        //        }

        //        //triangles need to be CW bc that's the convention unity uses
        //        if (ccw)
        //        {
        //            triangles[t++] = v0;
        //            triangles[t++] = v2;
        //            triangles[t++] = v1;
        //        }
        //        else
        //        {
        //            triangles[t++] = v0;
        //            triangles[t++] = v1;
        //            triangles[t++] = v2;
        //        }

        //        verticesRemaining.RemoveAt(i1);
        //        convexVertexFound = true;
        //        break;
        //    }

        //    if (!convexVertexFound)
        //    {
        //        Debug.Log("Triangulation failed.");
        //        return;
        //    }

        //}


        //GEOM DATA

        //var geomData = new List<Vector2>();//to be stored into mesh.uv2 for use in shaders
        //var pc = spriteShapeController.GetComponent<PolygonCollider2D>();
        //if (!pc)
        //{
        //    Debug.Log($"({gameObject.name}): sprite shape has no collider, so shadows will not be generated.");
        //}

        //for (int i = 0; i < vertices.Count; i++)
        //{
        //    var im = i == 0 ? vertices.Count - 1 : i - 1;
        //    var ip = i == vertices.Count - 1 ? 0 : i + 1;
        //    Vector2 w1 = (vertices[im] - vertices[i]).normalized;
        //    Vector2 w2 = (vertices[ip] - vertices[i]).normalized;
        //    var c = ccw ? -MathTools.Cross2D(w1, w2) : MathTools.Cross2D(w1, w2);//c > 0 means convex vertex
        //    geomData[i].x = Mathf.Clamp(c + 1, 0, 1);

        //    if (pc)
        //    {
        //        float dy = shadowCastIncrement;
        //        Vector2 q = spriteShapeController.transform.TransformPoint(vertices[i]);
        //        bool rayHit = false;
        //        while (dy < shadowDistanceMax)
        //        {
        //            var q1 = new Vector2(q.x, q.y + dy);
        //            if (pc.OverlapPoint(q1))
        //            {
        //                geomData[i].y = dy / shadowDistanceMax;
        //                rayHit = true;
        //                Debug.DrawLine(q, q1, Color.red, 5);
        //                break;
        //            }
        //            else
        //            {
        //                dy += shadowCastIncrement;
        //            }
        //        }

        //        if (!rayHit)
        //        {
        //            geomData[i].y = 1;
        //        }
        //    }
        //    else
        //    {
        //        geomData[i].y = 1;
        //    }

        //    //w2 = -Mathf.Sign(c) * (0.5f * (w1 + w2)).normalized;//outward "normal" at the corner
        //    //geomData[i] = new(c + 1, w2.y + 1);
        //    //first coordinate is 0 - 1 with 0 = concave, 1 = convex
        //    //second coordinate is 0 - 1 with 1 = outwd normal points up, 0 = outwd normal points down(whether vertex is on the "underside")
        //    //in future we may want a better approach to 2nd coordinate, where we also count whether a vertex is overshadowed by vertices above it,
        //    //not just is the vertex facing down
        //}


        //SUBDIVIDE BIG TRIANGLES

        //var triangleList = triangles.ToList();
        //var uvList = uv.ToList();
        //var geomDataList = geomData.ToList();

        //int m = 0;
        //while (m < triangleList.Count)
        //{
        //    var v0 = triangleList[m];
        //    var v1 = triangleList[m + 1];
        //    var v2 = triangleList[m + 2];
        //    var triangleArea = MathTools.Cross2D(vertices[v0] - vertices[v1], vertices[v2] - vertices[v1]);
        //    if (triangleArea < 0)
        //    {
        //        Debug.Log("mis-oriented triangle");
        //    }

        //    if (Mathf.Abs(triangleArea) < maxTriangleArea)
        //    {
        //        m += 3;//onto the next triangle lads
        //        continue;
        //    }

        //    //if triangle too big, split into four triangles using the midpoints of each edge
        //    var p0 = 0.5f * (vertices[v0] + vertices[v1]);
        //    var p1 = 0.5f * (vertices[v1] + vertices[v2]);
        //    var p2 = 0.5f * (vertices[v2] + vertices[v0]);
        //    vertices.Add(p0);//add new vertices at the end of the vertex list, so existing triangles still reference the correct vertices
        //    var v3 = vertices.Count - 1;
        //    vertices.Add(p1);
        //    var v4 = vertices.Count - 1;
        //    vertices.Add(p2);
        //    var v5 = vertices.Count - 1;

        //    //add uv and geomData for the new vertices
        //    uvList.Add(0.5f * (uvList[v0] + uvList[v1]));//uv for v3
        //    uvList.Add(0.5f * (uvList[v1] + uvList[v2]));//uv for v4
        //    uvList.Add(0.5f * (uvList[v2] + uvList[v0]));//uv for v5
        //    geomDataList.Add(Vector2.one);
        //    geomDataList.Add(Vector2.one);
        //    geomDataList.Add(Vector2.one);
        //    //var h3 = 0.5f * (geomDataList[v0] + geomDataList[v1]);
        //    //var h4 = 0.5f * (geomDataList[v1] + geomDataList[v2]);
        //    //var h5 = 0.5f * (geomDataList[v2] + geomDataList[v0]);
        //    //h3 = Vector2.Lerp(h3, Vector2.one, interiorVertexBrightening);
        //    //h4 = Vector2.Lerp(h4, Vector2.one, interiorVertexBrightening);
        //    //h5 = Vector2.Lerp(h5, Vector2.one, interiorVertexBrightening);
        //    //geomDataList.Add(h3);
        //    //geomDataList.Add(h4);
        //    //geomDataList.Add(h5);

        //    //replace triangle v0-v1-v2 with v3-v4-v5
        //    triangleList[m] = v3;
        //    triangleList[m + 1] = v4;
        //    triangleList[m + 2] = v5;

        //    //add triangles v1-v4-v3, v0-v3-v5, and v2-v5-v4 to triangleList
        //    triangleList.Add(v1);
        //    triangleList.Add(v4);
        //    triangleList.Add(v3);

        //    triangleList.Add(v0);
        //    triangleList.Add(v3);
        //    triangleList.Add(v5);

        //    triangleList.Add(v2);
        //    triangleList.Add(v5);
        //    triangleList.Add(v4);

        //    //don't increment m because we may need to further break up the new triangle at index m
        //}


        //GEOM DATA SMOOTHING

        //for (int i = 0; i < geomSmoothingIterations; i++)
        //{
        //    m = 0;
        //    while (m < triangleList.Count)
        //    {
        //        var v0 = triangleList[m];
        //        var v1 = triangleList[m + 1];
        //        var v2 = triangleList[m + 2];
        //        //var g0 = geomDataList[v0];
        //        //var g1 = geomDataList[v1];
        //        //var g2 = geomDataList[v2];

        //        geomDataList[v0] = Vector2.Lerp(geomDataList[v0], 0.5f * (geomDataList[v1] + geomDataList[v2]), geomSmoothingWeight);//0.5f * g0 + 0.25f * (g1 + g2);
        //        geomDataList[v1] = Vector2.Lerp(geomDataList[v1], 0.5f * (geomDataList[v2] + geomDataList[v0]), geomSmoothingWeight);//0.5f * g1 + 0.25f * (g2 + g0);
        //        geomDataList[v2] = Vector2.Lerp(geomDataList[v2], 0.5f * (geomDataList[v0] + geomDataList[v1]), geomSmoothingWeight);//0.5f * g2 + 0.25f * (g0 + g1);

        //        m += 3;
        //    }
        //}

        //CREATE MESH
        mesh = new();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uv);
        //mesh.SetUVs(1, geomData);
        mesh.RecalculateNormals();
    }

    public void ApplyMesh()
    {
        if (meshFilter && mesh)
        {
            meshFilter.mesh = mesh;
        }
    }
}