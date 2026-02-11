using UnityEngine;
using System.Collections.Generic;

public static class BWTriangulator
{
    public static List<int> Triangulate(List<Vector3> vertices)
    {
        HashSet<(int, int, int)> triangles = new();
        HashSet<(int, int, int)> badTriangles = new();
        HashSet<(int, int)> badTriangleBoundary = new();

        var st = SuperTriangle(vertices);
        vertices.Add(st.Item1);
        var st0 = vertices.Count - 1;
        vertices.Add(st.Item2);
        var st1 = vertices.Count - 1;
        vertices.Add(st.Item3);
        var st2 = vertices.Count - 1;

        triangles.Add((st0, st1, st2));

        for (int i = 0; i < vertices.Count; i++)
        {
            Vector2 p = vertices[i];
            badTriangles.Clear();
            badTriangleBoundary.Clear();

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
                MathTools.TryIntersectLine(0.5f * (p0 + p1), (p1 - p0).CCWPerp(), 0.5f * (p0 + p2), (p2 - p0).CCWPerp(), out var c);
                var r2 = Vector2.SqrMagnitude(p0 - c);

                if (Vector2.SqrMagnitude(p - c) < r2)
                {
                    badTriangles.Add(t);
                }
            }

            foreach (var t in badTriangles)
            {
                triangles.Remove(t);

                var t0 = t.Item1;
                var t1 = t.Item2;
                var t2 = t.Item3;

                if (IsBoundaryEdge(t, t0, t1))
                {
                    badTriangleBoundary.Add((t0, t1));
                }
                if (IsBoundaryEdge(t, t1, t2))
                {
                    badTriangleBoundary.Add((t1, t2));
                }
                if (IsBoundaryEdge(t, t2, t0))
                {
                    badTriangleBoundary.Add((t2, t0));
                }
            }

            foreach (var e in badTriangleBoundary)
            {
                var t0 = e.Item1;
                var t1 = e.Item2;
                var t2 = i;

                Vector2 v1 = vertices[t1] - vertices[t0];
                Vector2 v2 = vertices[t2] - vertices[t0];

                if (MathTools.Cross2D(v1, v2) > 0)//(t0, t1, t2) is CCW-oriented
                {
                    triangles.Add((t0, t2, t1));
                }
                else
                {
                    triangles.Add((t0, t1, t2));
                }
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

        //remove super triangle vertices
        vertices.RemoveRange(vertices.Count - 3, 3);

        var triangleList = new List<int>();
        foreach (var t in triangles)
        {
            if (IsSuperTriangleVertex(t.Item1) || IsSuperTriangleVertex(t.Item2) || IsSuperTriangleVertex(t.Item3))
            {
                continue;
            }

            triangleList.Add(t.Item1);
            triangleList.Add(t.Item2);
            triangleList.Add(t.Item3);
        }

        bool IsSuperTriangleVertex(int v)
        {
            return v == st0 || v == st1 || v == st2;
        }

        return triangleList;
    }

    private static (Vector3, Vector3, Vector3) SuperTriangle(List<Vector3> vertices)
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

        var p = 0.5f * (max + min);

        //expand bounding box by a factor of 1.25
        max = 1.25f * (max - p) + p;
        min = 1.25f * (min - p) + p;

        //make a bigly super triangle around the box
        var a1 = max + 5 * Vector2.one;
        var a2 = new Vector2(2 * p.x - a1.x, a1.y);
        var lr = new Vector2(max.x, min.y);
        var m = (a1.y - lr.y) / (a1.x - lr.x);
        var b = new Vector2(p.x, m * (p.x - lr.x) + lr.y);

        return (a1, b, a2);
    }
}