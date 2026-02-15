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
    [SerializeField] PolygonCollider2D polygonCollider;
    [SerializeField] int arcLengthSamples;
    [SerializeField] float splineSampleRate;//number of sample points per unit of distance between sprite shape vertices

    [Header("Refinement Settings")]
    [SerializeField] float minAngleRad;
    [SerializeField] int refinementIterations;
    [SerializeField] float boundingBoxSize = 3;

    [Header("Geometry Settings")]
    [SerializeField] float borderWidth;
    [SerializeField] int geomSpreadIterations;
    [SerializeField] float highlightSpreadRate;
    [SerializeField] float shadowSpreadRate;
    [SerializeField] int geomSmoothingIterations;
    [SerializeField] float geomSmoothingRate;

    public void GenerateMesh(bool updateCollider)
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
                pj = spriteShapeController.transform.TransformPoint(pj);
                pj += transform.position - spriteShapeController.transform.position;
                pj = transform.InverseTransformPoint(pj);
                vertices.Add(pj);
            }
        }

        //TRIANGLES

        //it outs the refined graph (polygon border), which you could use to get a "tight" uv if you want
        var triangles = Triangulator.TriangulatePolygon(vertices, minAngleRad, refinementIterations, boundingBoxSize, out var refinedPolygon, out var neighbors);

        //int t = 0;
        //while (t < triangles.Count)
        //{
        //    var t0 = triangles[t++];
        //    var t1 = triangles[t++];
        //    var t2 = triangles[t++];

        //    var p0 = transform.TransformPoint(vertices[t0]);
        //    var p1 = transform.TransformPoint(vertices[t1]);
        //    var p2 = transform.TransformPoint(vertices[t2]);

        //    Debug.DrawLine(p0, p1, Color.red, 3);
        //    Debug.DrawLine(p1, p2, Color.red, 3);
        //    Debug.DrawLine(p2, p0, Color.red, 3);
        //}


        //UV -- for now just a simple bounding box uv

        var bb = Triangulator.BoundingBox(vertices);//(max, min)
        var max = bb.Item1;
        var min = bb.Item2;
        var span = max - min;
        var uv = vertices.Select(v => new Vector2((v.x - min.x) / span.x, (v.y - min.y) / span.y)).ToArray();


        //UV1&2 -- other geometry data to use in shaders

        HashSet<int> polygonVertices = new();
        for (int i = 0; i < refinedPolygon.Count; i++)
        {
            var e = refinedPolygon[i];
            polygonVertices.Add(e.Item1);
            //var color = (i % 3) switch
            //{
            //    0 => Color.red,
            //    1 => Color.green,
            //    _ => Color.blue
            //};
            //Debug.DrawLine(transform.TransformPoint(vertices[e.Item1]), transform.TransformPoint(vertices[e.Item2]), color, 60);
        }

        var uv1 = GenerateUV1(vertices, refinedPolygon, polygonVertices, neighbors, borderWidth);
        var uv2 = GenerateUV2(vertices, refinedPolygon, polygonVertices);
        SpreadGeomData(uv2, neighbors, geomSpreadIterations, highlightSpreadRate, shadowSpreadRate);
        SmoothData(uv2, neighbors, geomSmoothingIterations, geomSmoothingRate);

        //CREATE MESH
        mesh = new();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uv);
        mesh.SetUVs(1, uv1);
        mesh.SetUVs(2, uv2);
        mesh.RecalculateNormals();

        if (updateCollider && polygonCollider)
        {
            polygonCollider.points = refinedPolygon.Select(e => (Vector2)vertices[e.Item1]).ToArray();
        }
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
    private Vector2[] GenerateUV1(IList<Vector3> vertices, IList<(int, int)> polygon, HashSet<int> polygonVertices, List<int>[] neighbors, float borderWidth)
    {
        var uv1 = new Vector2[vertices.Count];
        HashSet<int> seen = new();
        Queue<int> queue = new();

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
            uv1[i].x = sum;

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
                    if (polygonVertices.Contains(j))
                    {
                        distanceToBorder = Mathf.Min(distanceToBorder, Mathf.Sqrt(d));
                    }
                    else if (neighbors[j] != null)
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

            uv1[i].y = Mathf.Clamp(distanceToBorder / borderWidth, 0, 1f);
        }

        for (int i = 0; i < uv1.Length; i++)
        {
            uv1[i].x /= max;
        }

        return uv1;
    }

    //uv2.x = convexity wrt polygon interior (-1 = concave in toward interior, 1 = convex)    
    //uv2.y = top-side highlight/underside shadow (-1 = shadow, 1 = highlight)
    //we'll start with border vertices, then spread to neighbors within certain distance
    private Vector2[] GenerateUV2(IList<Vector3> vertices, IList<(int, int)> polygon, HashSet<int> polygonVertices)
    {
        //polygon is CW oriented (sprite shape spline  always cw oriented)

        var uv2 = new Vector2[vertices.Count];

        for (int i = 0; i < polygon.Count; i++)
        {
            var e0 = polygon[i > 0 ? i - 1 : polygon.Count - 1];
            var e1 = polygon[i];

            Vector2 u0 = (vertices[e0.Item1] - vertices[e0.Item2]).normalized;
            Vector2 u1 = (vertices[e1.Item2] - vertices[e1.Item1]).normalized;
            var convexity = MathTools.Cross2D(u0, u1);
            var outwardNormal = convexity == 0 ? u1.CCWPerp() : -Mathf.Sign(convexity) * (0.5f * (u0 + u1)).normalized;

            if (outwardNormal == Vector2.zero)
            {
                outwardNormal = u1.CCWPerp();
            }
            //var color = i == 0 ? Color.aquamarine : Color.pink;
            //Debug.DrawLine(transform.TransformPoint(vertices[e1.Item1]), transform.TransformPoint((Vector2)vertices[e1.Item1] + outwardNormal), color, 60);

            uv2[e1.Item1].x = convexity;
            uv2[e1.Item1].y = outwardNormal.y;
        }

        return uv2;
    }

    //rewrite to smooth only from vertices have values above a threshold
    //and only to neighbors within a certain distance
    //(and you may want to use a copy of the data for checking the thresholds, so it always checks the starting values)
    private void SmoothData(Vector2[] data, List<int>[] neighbors, int smoothingIterations, float smoothingWeight)
    {
        var dataCopy = new Vector2[data.Length];
        for (int i = 0; i < smoothingIterations; i++)
        {
            Array.Copy(data, dataCopy, data.Length);
            for (int j = 0; j < data.Length; j++)
            {
                var sum = Vector2.zero;
                foreach (var k in neighbors[j])
                {
                    sum += dataCopy[k];
                }

                sum /= neighbors[j].Count;
                data[j] = Vector2.Lerp(dataCopy[j], sum, smoothingWeight);
            }
        }
    }

    private void SpreadGeomData(Vector2[] data, List<int>[] neighbors, int smoothingIterations, float highlightSpreadRate, float shadowSpreadRate)
    {
        var dataCopy = new Vector2[data.Length];
        for (int i = 0; i < smoothingIterations; i++)
        {
            bool anyNonzero = false;
            Array.Copy(data, dataCopy, data.Length);
            for (int j = 0; j < data.Length; j++)
            {
                if (data[j] != Vector2.zero)
                {
                    continue;
                }

                anyNonzero = true;
                var sum = Vector2.zero;
                var count = 0;
                foreach (var k in neighbors[j])
                {
                    if (dataCopy[k] != Vector2.zero)
                    {
                        sum += (dataCopy[k].y > 0 ? highlightSpreadRate  : shadowSpreadRate) * dataCopy[k];
                        count++;
                    }
                }

                if (count != 0)
                {
                    data[j] = sum / count;
                }
            }

            if (!anyNonzero)
            {
                Debug.Log($"Finished spread after {i} iterations");
                break;
            }
        }
    }
}