using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.U2D;

public class SpriteShapeMeshGenerator : MonoBehaviour
{
    public Mesh mesh;

    [SerializeField] SpriteShapeController spriteShapeController;
    [SerializeField] MeshFilter meshFilter;
    [SerializeField] int arcLengthSamples;
    [SerializeField] float splineSampleRate;//number of sample points per unit of distance between sprite shape vertices

    [Header("Refinement Settings")]
    [SerializeField] float minAngleRad;
    [SerializeField] int refinementIterations;
    [SerializeField] float boundingBoxSize = 3;
    [SerializeField] int geomSmoothingIterations;
    [SerializeField] float geomSmoothingWeight;
    [SerializeField] float borderWidth;

    public void GenerateMesh()
    {
        var spline = spriteShapeController.spline;

        //VERTICES

        var vertices = new List<Vector3>();//use list bc we don't know how many vertices yet, since we're subdividing sprite shape vertices

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
                var pj = BezierUtility.BezierPoint(pRightTangent, p, q, qLeftTangent, s);
                vertices.Add(pj);
            }
        }

        //TRIANGLES

        //it outs the refined graph (polygon border), which you could use to get a "tight" uv if you want
        var triangles = Triangulator.TriangulatePolygon(vertices, minAngleRad, refinementIterations, boundingBoxSize, out var refinedPolygon, out var neighbors);

        int t = 0;
        while (t < triangles.Count)
        {
            var t0 = triangles[t++];
            var t1 = triangles[t++];
            var t2 = triangles[t++];

            var p0 = spriteShapeController.transform.TransformPoint(vertices[t0]);
            var p1 = spriteShapeController.transform.TransformPoint(vertices[t1]);
            var p2 = spriteShapeController.transform.TransformPoint(vertices[t2]);

            Debug.DrawLine(p0, p1, Color.red, 3);
            Debug.DrawLine(p1, p2, Color.red, 3);
            Debug.DrawLine(p2, p0, Color.red, 3);
        }

        //for now let's just do a simple bounding box uv
        var bb = Triangulator.BoundingBox(vertices);//(max, min)
        var max = bb.Item1;
        var min = bb.Item2;
        var span = max - min;
        var uv = vertices.Select(v => new Vector2((v.x - min.x) / span.x, (v.y - min.y) / span.y)).ToArray();

        var uv1 = GenerateUV1(refinedPolygon, vertices, neighbors, borderWidth);
        SmoothData(uv1, neighbors, geomSmoothingIterations, geomSmoothingWeight);

        //CREATE MESH
        mesh = new();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uv);
        mesh.SetUVs(1, uv1);
        mesh.RecalculateNormals();
    }

    public void ApplyMesh()
    {
        if (meshFilter && mesh)
        {
            meshFilter.mesh = mesh;
        }
    }

    //uv1.x = "neighbor density" (sum over neighbors of 1 / distance)
    //uv1.y = distance to border
    private Vector2[] GenerateUV1(IList<(int, int)> polygon, IList<Vector3> vertices, List<int>[] neighbors, float borderWidth)
    {
        var geomData = new Vector2[vertices.Count];
        HashSet<int> borderVertices = new();
        HashSet<int> seen = new();
        Queue<int> queue = new();

        foreach (var e in polygon)
        {
            borderVertices.Add(e.Item1);
            borderVertices.Add(e.Item2);
        }

        var max = 0f;
        var borderWidth2 = borderWidth * borderWidth;
        for (int i = 0; i < vertices.Count; i++)
        {
            if (neighbors[i] == null)
            {
                continue;
            }

            var sum = 0f;
            foreach (var n in neighbors[i])
            {
                sum += 1 / Vector2.Distance(vertices[i], vertices[n]);
            }

            max = Math.Max(max, sum);
            geomData[i].x = sum;

            queue.Clear();
            seen.Clear();
            queue.Enqueue(i);
            seen.Add(i);

            var distanceToBorder = Mathf.Infinity;
            while (queue.Count != 0)
            {
                var j = queue.Dequeue();
                var d = Vector2.SqrMagnitude(vertices[j] - vertices[i]);
                if (d < borderWidth2)
                {
                    if (borderVertices.Contains(j))
                    {
                        distanceToBorder = Mathf.Min(distanceToBorder, Mathf.Sqrt(d));
                    }
                    else
                    {
                        if (neighbors[j] != null)
                        {
                            foreach (var k in neighbors[j])
                            {
                                if (!seen.Contains(k))
                                {
                                    queue.Enqueue(k);
                                    seen.Add(k);
                                }
                            }
                        }
                    }
                }
            }

            geomData[i].y = Mathf.Clamp(distanceToBorder / borderWidth, 0, 1f);
        }

        for (int i = 0; i < geomData.Length; i++)
        {
            geomData[i].x /= max;
        }

        return geomData;
    }

    //private Vector2[] GenerateUV1(IList<Vector3> vertices, IList<int> triangles, IList<(int, int)> graph,  Vector2 bbMax, Vector2 bbMin, float cellSize, float densitySmoothingRadius)
    //{
    //    var geomData = new Vector2[vertices.Count];
    //    HashSet<int> graphVertices = new();

    //    foreach (var e in graph)
    //    {
    //        graphVertices.Add(e.Item1);
    //        graphVertices.Add(e.Item2);
    //    }

    //    //x-coord will be density of vertices near that vertex
    //    //y-coord will be overshadowing
    //    //(then we can use this data how we want in shader)
    //    //(no point in generating a "height map" here because we can do that in shader with noise)

    //    //organize vertices into grid
    //    var span = bbMax - bbMin;
    //    var gridWidth = (int)Mathf.Ceil(span.x / cellSize);
    //    var gridHeight = (int)Mathf.Ceil(span.y / cellSize);
    //    var numCells = gridWidth * gridHeight;

    //    int Index(int i, int j) => i * gridWidth + j;

    //    var cellVertexCount = new int[numCells];
    //    var cellContainingVertex = new int[vertices.Count];

    //    for (int i = 0; i < vertices.Count; i++)
    //    {
    //        var p = vertices[i];
    //        var a = Mathf.Clamp((int)((p.y - bbMin.y) / cellSize), 0, gridHeight - 1);
    //        var b = Mathf.Clamp((int)((p.x - bbMin.x) / cellSize), 0, gridWidth - 1);
    //        var cell = Index(a, b);
    //        cellVertexCount[cell]++;
    //        cellContainingVertex[i] = cell;
    //    }

    //    var cellStart = new int[numCells + 1];
    //    for (int i = 1; i < cellStart.Length; i++)
    //    {
    //        cellStart[i] = cellStart[i - 1] + cellVertexCount[i - 1];
    //    }

    //    var verticesByCell = new int[vertices.Count];
    //    for (int i = 0; i < cellContainingVertex.Length; i++)
    //    {
    //        var cell = cellContainingVertex[i];
    //        var j = cellStart[cell] + --cellVertexCount[cell];
    //        verticesByCell[j] = i;
    //    }

    //    var maxDens = 0f;
    //    for (int i = 0; i < vertices.Count; i++)
    //    {
    //        Vector2 p = vertices[i];
    //        var min = new Vector2(p.x - densitySmoothingRadius - bbMin.x, p.y - densitySmoothingRadius - bbMin.y);
    //        var max = new Vector2(p.x + densitySmoothingRadius - bbMin.x, p.y + densitySmoothingRadius - bbMin.y);
    //        var r2 = Mathf.Min((p - min).sqrMagnitude / 2, (p - max).sqrMagnitude / 2);
    //        var aMin = Mathf.Clamp((int)(min.y / cellSize), 0, gridHeight - 1);
    //        var bMin = Mathf.Clamp((int)(min.x / cellSize), 0, gridWidth - 1);
    //        var aMax = Mathf.Clamp((int)(max.y / cellSize), 0, gridHeight - 1);
    //        var bMax = Mathf.Clamp((int)(max.x / cellSize), 0, gridWidth - 1);

    //        int occupiedCells = 0;
    //        for (int a = aMin; a < aMax + 1; a++)
    //        {
    //            for (int b = bMin; b < bMax + 1; b++)
    //            {
    //                var cell = Index(a, b);
    //                if (cellStart[cell] < cellStart[cell + 1])
    //                {
    //                    occupiedCells++;
    //                    for (int v = cellStart[cell]; v < cellStart[cell + 1]; v++)
    //                    {
    //                        var d2 = Vector2.SqrMagnitude((Vector2)vertices[v] - p);
    //                        if (d2 < r2)
    //                        {
    //                            geomData[i].x += SmoothingKernel(densityKernelDeg, d2, r2);
    //                        }
    //                    }
    //                }
    //            }
    //        }

    //        geomData[i].x /= occupiedCells;
    //        maxDens = Mathf.Max(geomData[i].x, maxDens);
    //        geomData[i].y = 1;
    //    }

    //    for (int i = 0; i < vertices.Count; i++)
    //    {
    //        geomData[i].x = geomData[i].x / maxDens;
    //        if (graphVertices.Contains(i))
    //        {
    //            geomData[i].x = Mathf.SmoothStep(geomData[i].x, 1, borderShadow);
    //        }
    //    }

    //    //float SmoothingKernel(float N, float dist2, float radius2)
    //    //{
    //    //    return Mathf.Pow(1 - dist2 / radius2, N);
    //    //}

    //    return geomData;
    //}

    private void SmoothData(Vector2[] data, List<int>[] neighbors, int smoothingIterations, float smoothingWeight)
    {
        var dataCopy = new Vector2[data.Length];
        for (int i = 0; i < smoothingIterations; i++)
        {
            Array.Copy(data, dataCopy, data.Length);
            for (int j = 0; j < data.Length; j++)
            {
                var n = neighbors[j].Count;
                if (n == 0)
                {
                    continue;
                }

                var sum = Vector2.zero;
                foreach (var k in neighbors[i])
                {
                    sum += dataCopy[k];
                }

                sum /= n;
                data[j] = Vector2.Lerp(dataCopy[j], sum, smoothingWeight);
            }
        }
    }
}