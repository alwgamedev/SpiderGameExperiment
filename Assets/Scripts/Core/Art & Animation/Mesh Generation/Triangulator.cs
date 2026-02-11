using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class Triangulator
{
    public static List<int> TriangulatePolygon(IList<Vector3> vertices, float minAngleRad, 
        int maxRefinementIterations, int maxRepairIterations, float boundingBoxSize)
    {
        List<(int, int)> graph = Enumerable.Range(0, vertices.Count).Select(i => (i, i == vertices.Count - 1 ? 0 : i + 1)).ToList();
        HashSet<(int, int, int)> triangles = new();
        HashSet<(int, int, int)> badTriangles = new();
        HashSet<(int, int)> cut = new();
        HashSet<int> encroachedSegments = new();

        //if any perimeter corners have angle < 90, we'll lop off a small pointy bit at that corner -- or just make that pointy bit a triangle

        //add 3x bounding box
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

        //if (debugTransform)
        //{
        //    var p0 = debugTransform.TransformPoint(vertices[bb0]);
        //    var p1 = debugTransform.TransformPoint(vertices[bb1]);
        //    var p2 = debugTransform.TransformPoint(vertices[bb2]);
        //    var p3 = debugTransform.TransformPoint(vertices[bb3]);

        //    Debug.DrawLine(p0, p1, Color.blue, 10);
        //    Debug.DrawLine(p1, p2, Color.blue, 10);
        //    Debug.DrawLine(p2, p3, Color.blue, 10);
        //    Debug.DrawLine(p3, p0, Color.blue, 10);
        //}

        graph.Add((bb0, bb1));
        graph.Add((bb1, bb2));
        graph.Add((bb2, bb3));
        graph.Add((bb3, bb0));

        triangles.Add((bb0, bb1, bb2));
        triangles.Add((bb2, bb3, bb0));

        for (int i = 0; i < vertices.Count - 4; i++)
        {
            AddVertexToTriangulation(vertices, triangles, badTriangles, cut, i);
        }

        RefineDelaunayTriangulation(graph, vertices, triangles, badTriangles, cut, encroachedSegments, 
            minAngleRad, maxRefinementIterations, maxRepairIterations);

        //remove auxiliary vertices and build final triangle list
        var triangleList = new List<int>();
        foreach (var t in triangles)
        {
            if (IsAuxiliaryVertex(t.Item1) || IsAuxiliaryVertex(t.Item2) || IsAuxiliaryVertex(t.Item3))
            {
                continue;
            }

            triangleList.Add(t.Item1 > bb3 ? t.Item1 - 4 : t.Item1);
            triangleList.Add(t.Item2 > bb3 ? t.Item2 - 4 : t.Item2);
            triangleList.Add(t.Item3 > bb3 ? t.Item3 - 4 : t.Item3);
        }

        vertices.RemoveAt(bb3);
        vertices.RemoveAt(bb2);
        vertices.RemoveAt(bb1);
        vertices.RemoveAt(bb0);

        bool IsAuxiliaryVertex(int v)
        {
            return !(v < bb0) && !(v > bb3);
        }
        //trim off external triangles... remember to also remove any vertices outside the boundary (does this happen? -- yes it appears so)
        //and return triangle list

        return triangleList;
    }

    //Ruppert's algorithm
    public static void RefineDelaunayTriangulation(IList<(int, int)> graph, IList<Vector3> vertices, ICollection<(int, int, int)> triangles, 
        ICollection<(int, int, int)> badTriangles, ICollection<(int, int)> cut, ICollection<int> encroachedSegments, 
        float minAngleRad, int maxRefinementIterations, int maxRepairIterations)
    {
        Debug.Log("beginning refinement...");
        float sineMinAngle = Mathf.Sin(minAngleRad);
        int i = 0;
        while (i < maxRefinementIterations)
        {
            bool changed = false;

            if (Repair(graph, vertices, triangles, badTriangles, cut, maxRepairIterations))
            {
                changed = true;
            }
            if (SplitFirstSkinnyTriangle(graph, vertices, triangles, badTriangles, cut, encroachedSegments, sineMinAngle))
            {
                changed = true;
            }

            if (changed)
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

    private static bool Repair(IList<(int, int)> graph, IList<Vector3> vertices, ICollection<(int, int, int)> triangles, 
        ICollection<(int, int, int)> badTriangles, ICollection<(int, int)> cut, int maxIterations)
    {
        int i = 0;
        while (i < maxIterations)
        {
            if (SplitFirstEncroachedSegment(graph, vertices, triangles, badTriangles, cut))
            {
                i++;
            }
            else
            {
                break;
            }
        }

        Debug.Log($"Repair terminated after {i}/{maxIterations} iterations");

        return i > 0;
    }

    private static bool SplitFirstEncroachedSegment(IList<(int, int)> graph, IList<Vector3> vertices, ICollection<(int, int, int)> triangles, 
        ICollection<(int, int, int)> badTriangles, ICollection<(int, int)> cut)
    {
        for (int i = 0; i < graph.Count; i++)
        {
            var p0 = vertices[graph[i].Item1];
            var p1 = vertices[graph[i].Item2];
            var p = 0.5f * (p0 + p1);
            var r2 = (p - p0).sqrMagnitude;

            foreach (var v in vertices)
            {
                if ((v - p).sqrMagnitude <= r2)
                {
                    SplitSegment(graph, vertices, triangles, badTriangles, cut, i);
                    return true;
                }
            }
        }

        return false;
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
            //Debug.Log($"splitting triangle {triangle.Item1}: {p0}, {triangle.Item2}: {p1}, {triangle.Item3}: {p2} by adding its circumcenter {c}");
            vertices.Add(c);
            AddVertexToTriangulation(vertices, triangles, badTriangles, cut, vertices.Count - 1);
        }
        else
        {
            Debug.Log("Instead of splitting triangle, we'll split the segments its circumcenter encroaches upon");
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
        Debug.Log($"Adding vertex {p}");
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
            //a == 0 can (and will!) happen when you split a segment
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
                if ((s.Item2 == e0 && s.Item1 == e1) /*|| (s.Item1 == e0 && s.Item2 == e1)*/
                    || (s.Item3 == e0 && s.Item2 == e1) /*|| (s.Item2 == e0 && s.Item3 == e1)*/
                    || (s.Item1 == e0 && s.Item3 == e1) /*|| (s.Item3 == e0 && s.Item1 == e1)*/)
                {
                    return false;
                }
            }

            return true;
        }
    }

    private static (Vector2, Vector2) BoundingBox(IList<Vector3> vertices)
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
}