using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class Triangulator
{
    public static List<int> TriangulatePolygon(IList<Vector3> vertices, float minAngleRad,
        int maxRefinementIterations, float boundingBoxSize, out List<(int, int)> refinedPolygon, out List<int>[] neighbors)
    {
        refinedPolygon = Enumerable.Range(0, vertices.Count).Select(i => (i, i == vertices.Count - 1 ? 0 : i + 1)).ToList();
        HashSet<(int, int, int)> triangles = new();
        HashSet<(int, int, int)> badTriangles = new();
        HashSet<(int, int)> cut = new();
        HashSet<int> encroachedSegments = new();
        HashSet<int> externalVertices = new();

        var bb = BoundingBox(vertices);
        var max = bb.Item1;
        var min = bb.Item2;
        var mid = 0.5f * (max + min);

        max = boundingBoxSize * (max - mid) + mid;
        min = boundingBoxSize * (min - mid) + mid;

        var bb0 = vertices.Count;
        vertices.Add(max);//upper right of bounding box
        var bb1 = vertices.Count;
        vertices.Add(new(max.x, min.y));//lower right of bounding box
        var bb2 = vertices.Count;
        vertices.Add(min);//lower left of bounding box
        var bb3 = vertices.Count;
        vertices.Add(new(min.x, max.y));//upper left of bounding box

        externalVertices.Add(bb0);
        externalVertices.Add(bb1);
        externalVertices.Add(bb2);
        externalVertices.Add(bb3);

        refinedPolygon.Add((bb0, bb1));
        refinedPolygon.Add((bb1, bb2));
        refinedPolygon.Add((bb2, bb3));
        refinedPolygon.Add((bb3, bb0));

        triangles.Add((bb0, bb1, bb2));
        triangles.Add((bb2, bb3, bb0));

        for (int i = 0; i < vertices.Count - 4; i++)
        {
            AddVertexToTriangulation(vertices, triangles, badTriangles, cut, i);
        }

        RefineDelaunayTriangulation(refinedPolygon, vertices, triangles, badTriangles, cut, encroachedSegments,
            minAngleRad, maxRefinementIterations);

        FindExternalVertices(refinedPolygon, vertices, triangles, externalVertices, out neighbors);

        var triangleList = new List<int>();
        foreach (var t in triangles)
        {
            var t0 = t.Item1;
            var t1 = t.Item2;
            var t2 = t.Item3;

            if (externalVertices.Contains(t0) || externalVertices.Contains(t1) || externalVertices.Contains(t2))
            {
                continue;
            }

            var t0Before = t0;
            var t1Before = t1;
            var t2Before = t2;

            foreach (var v in externalVertices)
            {
                if (t0Before > v)
                {
                    t0--;
                }
                if (t1Before > v)
                {
                    t1--;
                }
                if (t2Before > v)
                {
                    t2--;
                }
            }
            triangleList.Add(t0);
            triangleList.Add(t1);
            triangleList.Add(t2);
        }

        for (int i = 0; i < refinedPolygon.Count; i++)
        {
            var e0 = refinedPolygon[i].Item1;
            var e1 = refinedPolygon[i].Item2;

            var e0Before = e0;
            var e1Before = e1;

            foreach (var v in externalVertices)
            {
                if (e0Before > v)
                {
                    e0--;
                }
                if (e1Before > v)
                {
                    e1--;
                }
            }

            refinedPolygon[i] = (e0, e1);
        }

        for (int i = 0; i < neighbors.Length; i++)
        {
            if (neighbors[i] == null || externalVertices.Contains(i))
            {
                continue;
            }

            int j = 0;
            while (j < neighbors[i].Count)
            {
                var v = neighbors[i][j];
                if (externalVertices.Contains(v))
                {
                    neighbors[i].RemoveAt(j);
                }
                else
                {
                    var vBefore = v;
                    foreach (var w in externalVertices)
                    {
                        if (vBefore > w)
                        {
                            v--;
                        }
                    }

                    neighbors[i][j] = v;
                    j++;
                }
            }
        }

        int a = 0;//index we're copying to
        int b = 0;//index we're copying from
        while (b < neighbors.Length)
        {
            if (externalVertices.Contains(b))
            {
                b++;
            }
            else
            {
                neighbors[a] = neighbors[b];
                a++;
                b++;
            }
        }

        Array.Resize(ref neighbors, vertices.Count - externalVertices.Count);

        var externalVertexList = externalVertices.ToList();
        externalVertexList.Sort();
        for (int i = externalVertexList.Count - 1; i > -1; i--)
        {
            vertices.RemoveAt(externalVertexList[i]);
        }

        return triangleList;
    }

    //Ruppert's algorithm
    public static void RefineDelaunayTriangulation(IList<(int, int)> graph, IList<Vector3> vertices, ICollection<(int, int, int)> triangles,
        ICollection<(int, int, int)> badTriangles, ICollection<(int, int)> cut, ICollection<int> encroachedSegments,
        float minAngleRad, int maxRefinementIterations)
    {
        float sineMinAngle = Mathf.Sin(minAngleRad);
        int i = 0;
        while (i < maxRefinementIterations)
        {
            if (SplitFirstSkinnyTriangle(graph, vertices, triangles, badTriangles, cut, encroachedSegments, sineMinAngle))
            {
                i++;
            }
            else
            {
                break;
            }
        }

        Debug.Log($"Completed refinement after {i}/{maxRefinementIterations} iterations.");
    }

    public static (Vector2, Vector2) BoundingBox(IList<Vector3> vertices)
    {
        Vector2 max = new(-Mathf.Infinity, -Mathf.Infinity);
        Vector2 min = new(Mathf.Infinity, Mathf.Infinity);

        for (int i = 0; i < vertices.Count; i++)
        {
            max.x = Mathf.Max(vertices[i].x, max.x);
            max.y = Mathf.Max(vertices[i].y, max.y);
            min.x = Mathf.Min(vertices[i].x, min.x);
            min.y = Mathf.Min(vertices[i].y, min.y);
        }

        return (max, min);
    }

    private static void FindExternalVertices(IEnumerable<(int, int)> graph, IList<Vector3> vertices, IEnumerable<(int, int, int)> triangles, 
        ICollection<int> externalVertices, out List<int>[] neighbors)
    {
        neighbors = new List<int>[vertices.Count];

        foreach (var t in triangles)
        {
            var t0 = t.Item1;
            var t1 = t.Item2;
            var t2 = t.Item3;

            //we expect at most 12 neighbors for min angle = pi / 6
            neighbors[t0] ??= new(16);
            neighbors[t1] ??= new(16);
            neighbors[t2] ??= new(16);

            neighbors[t0].Add(t1);
            neighbors[t0].Add(t2);

            neighbors[t1].Add(t2);
            neighbors[t1].Add(t0);

            neighbors[t2].Add(t1);
            neighbors[t2].Add(t0);
        }

        HashSet<int> border = new();
        foreach (var e in graph)
        {
            border.Add(e.Item1);
            border.Add(e.Item2);
        }

        Queue<int> queue = new();
        bool[] seen = new bool[vertices.Count];
        foreach (var v in externalVertices)
        {
            queue.Enqueue(v);
            seen[v] = true;
        }

        while (queue.Count != 0)
        {
            var v = queue.Dequeue();
            externalVertices.Add(v);

            if (neighbors[v] == null)
            {
                continue;
            }

            foreach (var n in neighbors[v])
            {
                if (!seen[n])
                {
                    seen[n] = true;
                    if (!border.Contains(n))
                    {
                        queue.Enqueue(n);
                    }
                }
            }
        }
    }

    private static void SplitSegment(IList<(int, int)> graph, IList<Vector3> vertices, ICollection<(int, int, int)> triangles,
        ICollection<(int, int, int)> badTriangles, ICollection<(int, int)> cut, int segment)
    {
        var i0 = graph[segment].Item1;
        var i1 = graph[segment].Item2;

        var p0 = vertices[i0];
        var p1 = vertices[i1];
        var p = 0.5f * (p0 + p1);

        var i2 = vertices.Count;
        graph[segment] = (i0, i2);
        graph.Insert(segment + 1, (i2, i1));
        vertices.Add(p);
        AddVertexToTriangulation(vertices, triangles, badTriangles, cut, vertices.Count - 1);
    }

    private static bool SplitFirstSkinnyTriangle(IList<(int, int)> graph, IList<Vector3> vertices, ICollection<(int, int, int)> triangles,
        ICollection<(int, int, int)> badTriangles, ICollection<(int, int)> cut, ICollection<int> encroachedSegments, float sineMinAngle)
    {
        foreach (var t in triangles)
        {
            var p0 = vertices[t.Item1];
            var p1 = vertices[t.Item2];
            var p2 = vertices[t.Item3];

            //check angle 0
            var u = (p1 - p0).normalized;
            var v = (p2 - p0).normalized;
            if (Vector2.Dot(u, v) > 0 && Mathf.Abs(MathTools.Cross2D(u, v)) < sineMinAngle)
            {
                SplitTriangle(graph, vertices, triangles, badTriangles, cut, encroachedSegments, t);
                return true;
            }

            //check angle 1
            u = (p2 - p1).normalized;
            v = (p0 - p1).normalized;
            if (Vector2.Dot(u, v) > 0 && Mathf.Abs(MathTools.Cross2D(u, v)) < sineMinAngle)
            {
                SplitTriangle(graph, vertices, triangles, badTriangles, cut, encroachedSegments, t);
                return true;
            }

            //check angle 2
            u = (p1 - p2).normalized;
            v = (p0 - p2).normalized;
            if (Vector2.Dot(u, v) > 0 && Mathf.Abs(MathTools.Cross2D(u, v)) < sineMinAngle)
            {
                SplitTriangle(graph, vertices, triangles, badTriangles, cut, encroachedSegments, t);
                return true;
            }
        }
        return false;
    }

    private static void SplitTriangle(IList<(int, int)> graph, IList<Vector3> vertices, ICollection<(int, int, int)> triangles,
        ICollection<(int, int, int)> badTriangles, ICollection<(int, int)> cut, ICollection<int> encroachedSegments,
        (int, int, int) triangle)
    {
        var p0 = vertices[triangle.Item1];
        var p1 = vertices[triangle.Item2];
        var p2 = vertices[triangle.Item3];

        var c = MathTools.Circumcenter(p0, p1, p2);

        encroachedSegments.Clear();
        for (int i = 0; i < graph.Count; i++)
        {
            Vector2 q0 = vertices[graph[i].Item1];
            Vector2 q1 = vertices[graph[i].Item2];
            var q = 0.5f * (q0 + q1);
            var r2 = (q - q0).sqrMagnitude;

            if ((c - q).sqrMagnitude <= r2)
            {
                encroachedSegments.Add(i);
            }
        }

        if (encroachedSegments.Count == 0)
        {
            vertices.Add(c);
            AddVertexToTriangulation(vertices, triangles, badTriangles, cut, vertices.Count - 1);
        }
        else
        {
            foreach (var s in encroachedSegments)
            {
                SplitSegment(graph, vertices, triangles, badTriangles, cut, s);
            }
        }
    }

    private static void AddVertexToTriangulation(IList<Vector3> vertices, ICollection<(int, int, int)> triangles, ICollection<(int, int, int)> badTriangles,
        ICollection<(int, int)> cut, int vertexIndex)
    {
        Vector2 p = vertices[vertexIndex];
        badTriangles.Clear();
        cut.Clear();

        //find bad triangles (triangles with circumcircle containing p)
        foreach (var t in triangles)
        {
            var t0 = t.Item1;
            var t1 = t.Item2;
            var t2 = t.Item3;

            Vector2 p0 = vertices[t0];
            Vector2 p1 = vertices[t1];
            Vector2 p2 = vertices[t2];

            //find circumcenter
            var c = MathTools.Circumcenter(p0, p1, p2);
            var r2 = Vector2.SqrMagnitude(p0 - c);

            if ((p - c).sqrMagnitude <= r2)
            {
                badTriangles.Add(t);
            }
        }

        //remove bad triangles from triangulation and build cut boundary
        foreach (var t in badTriangles)
        {
            triangles.Remove(t);

            var t0 = t.Item1;
            var t1 = t.Item2;
            var t2 = t.Item3;

            if (IsBoundaryEdge(t, t0, t1))
            {
                cut.Add((t0, t1));
            }
            if (IsBoundaryEdge(t, t1, t2))
            {
                cut.Add((t1, t2));
            }
            if (IsBoundaryEdge(t, t2, t0))
            {
                cut.Add((t2, t0));
            }
        }

        //triangulate the cut
        foreach (var e in cut)
        {
            var t0 = e.Item1;
            var t1 = e.Item2;
            var t2 = vertexIndex;

            Vector2 v1 = vertices[t1] - vertices[t0];
            Vector2 v2 = vertices[t2] - vertices[t0];

            var a = MathTools.Cross2D(v1, v2);
            if (a > 0)//(t0, t1, t2) is CCW-oriented
            {
                triangles.Add((t0, t2, t1));
            }
            else if (a < 0)
            {
                triangles.Add((t0, t1, t2));
            }
            //a == 0 can (and will) happen when you split a segment! don't add the triangle in that case (when the three vertices are lined up on a segment)
        }

        bool IsBoundaryEdge((int, int, int) triangle, int e0, int e1)
        {
            foreach (var s in badTriangles)
            {
                if (s == triangle)
                {
                    continue;
                }

                //all triangles are oriented clockwise, so triangles sharing an edge will traverse it in opposite directions
                //(meaning we don't have to check equality of edges in the other direction)
                if ((s.Item2 == e0 && s.Item1 == e1) || (s.Item3 == e0 && s.Item2 == e1) || (s.Item1 == e0 && s.Item3 == e1))
                {
                    return false;
                }
            }

            return true;
        }
    }
}