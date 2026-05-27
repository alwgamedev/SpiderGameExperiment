using andywiecko.BurstTriangulator;
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D;

[ExecuteAlways]
public class SpriteShapeMeshGenerator : MonoBehaviour
{
    public Mesh mesh;

    [SerializeField] SpriteShapeController spriteShapeController;
    [SerializeField] MeshFilter meshFilter;
    [SerializeField] int arcLengthSamples;//samples per spline segment used to calculate arc length
    [SerializeField] float splineSampleRate;//number of vertices per unit arc length
    [SerializeField] float maxTriangleArea;
    [SerializeField] Vector2 uv1ExtrapolationRadius;
    [SerializeField] Vector2 uv1FadeRate;
    [SerializeField] Vector2 crannyInitialVal;
    [SerializeField] Vector2 crannySpread;
    [SerializeField] Vector2 numCranny;
    [SerializeField] float crannyBorder;
    [SerializeField] float crannyBorderIterations;
    [SerializeField] float crannySpreadRandomizerMin;//0 - 1 with 1 = no randomization
    [SerializeField] Vector2[] perimeter;
    [SerializeField] bool drawPerimeterGizmo;
    [SerializeField] float triangulationDrawTime;

    public ReadOnlySpan<Vector2> GetPerimeter() => perimeter;

    public void GenerateMesh()
    {
        if (!spriteShapeController)
        {
            Debug.LogWarning($"No Sprite Shape Controller.");
            return;
        }

        var vertices = new NativeList<Vector2>(Allocator.TempJob);
        SplineSampler.SampleSpline(spriteShapeController.spline, arcLengthSamples, splineSampleRate, vertices);
        var (bbMin, bbMax) = BoundingBox(vertices.AsArray());
        var triangulator = Triangulate(vertices.AsArray(), maxTriangleArea);//uses Allocator.TempJob
        vertices.Dispose();

        DrawTriangulationOutput(triangulator);

        var positions = triangulator.Output.Positions;
        var triangles = triangulator.Output.Triangles;
        var halfEdges = triangulator.Output.Halfedges;

        var boundaryEdges = new NativeList<int>(Allocator.Temp);
        GetBoundaryEdges(boundaryEdges, halfEdges);

        Array.Resize(ref perimeter, boundaryEdges.Length);
        for (int i = 0; i < perimeter.Length; i++)
        {
            perimeter[i] = positions[triangles[boundaryEdges[i]]];
        }

        var uv = new NativeArray<Vector2>(positions.Length, Allocator.Temp);
        var uv1 = new NativeArray<Vector2>(positions.Length, Allocator.Temp);
        var uv2 = new NativeArray<Vector2>(positions.Length, Allocator.Temp);

        CalculateUV(uv, positions, bbMin, bbMax);
        CalculateBoundaryUV1(uv1, positions, triangles, boundaryEdges);//uv1 = (convexity, topside/underside)
        CalculateInteriorUV1(uv1, positions, triangles, boundaryEdges, uv1ExtrapolationRadius.x, uv1ExtrapolationRadius.y, uv1FadeRate.x, uv1FadeRate.y);
        CalculateUV2X(uv2, positions, triangles, boundaryEdges);//uv2[i].x = area of polygon centered at vert i (i.e. sum of triangle areas) 
        var spreadMin = new Vector2(crannySpread.x, crannySpread.x);
        var spreadMax = new Vector2(crannySpread.y, crannySpread.y);
        var spreadRandomizerMin = new Vector2(crannySpreadRandomizerMin, crannySpreadRandomizerMin);
        var numCrannies = (int)math.ceil(MathTools.RandomFloat(numCranny.x, numCranny.y) * (bbMax.x - bbMin.x) * (bbMax.y - bbMin.y));
        CalculateUV2Y(uv2, positions, triangles, halfEdges, numCrannies, crannyInitialVal, spreadMin, spreadMax, spreadRandomizerMin,
            crannyBorder, crannyBorderIterations, transform);
        // CalculateUV2Y(uv1, uv2, positions, triangles, boundaryEdges);//uv1.y = distance to border

        mesh = new();

        NativeArray<Vector3> positionsV3 = new(positions.Length, Allocator.Temp);
        for (int i = 0; i < positionsV3.Length; i++)
        {
            positionsV3[i] = positions[i];
        }

        var managedTriangles = new int[triangles.Length];
        triangles.AsArray().CopyTo(managedTriangles);

        mesh.SetVertices(positionsV3);
        mesh.SetTriangles(managedTriangles, 0);
        mesh.SetUVs(0, uv);
        mesh.SetUVs(1, uv1);
        mesh.SetUVs(2, uv2);
        mesh.RecalculateNormals();

        triangulator.Dispose();
    }


    public void ApplyMesh()
    {
        if (meshFilter && mesh)
        {
            meshFilter.mesh = mesh;
        }
    }

    void OnDestroy()
    {
        if (mesh)
        {
            if (Application.isPlaying)
            {
                Destroy(mesh);
            }
            else
            {
                DestroyImmediate(mesh);
            }
        }
    }

    [BurstCompile]
    private static (Vector2 min, Vector2 max) BoundingBox(NativeArray<Vector2> vertices)
    {
        float2 min = vertices[0];
        float2 max = vertices[0];

        for (int i = 1; i < vertices.Length; i++)
        {
            min = math.min(vertices[i], min);
            max = math.max(vertices[i], max);
        }

        return (min, max);
    }

    private void DrawTriangulationOutput(Triangulator<Vector2> triangulator)
    {
        if (triangulationDrawTime > 0)
        {
            var output = triangulator.Output;
            var p = output.Positions;
            var t = output.Triangles;
            var he = output.Halfedges;//halfEdges[j] = neighboring edge to edge j, where indices correspond to triangle indices
                                      //(i.e. he.Length = triangles.Length, and if triangles = { v0, v1, v2, ...} then he[0] corresponds to edge (v0, v1))

            for (int i = 0; i < output.Triangles.Length / 3; i++)
            {
                var j = 3 * i;
                var t0 = t[j];
                var t1 = t[j + 1];
                var t2 = t[j + 2];
                var p0 = transform.TransformPoint(p[t0]);
                var p1 = transform.TransformPoint(p[t1]);
                var p2 = transform.TransformPoint(p[t2]);

                Debug.DrawLine(p0, p1, he[j] < 0 ? Color.blue : Color.red, triangulationDrawTime);
                Debug.DrawLine(p1, p2, he[j + 1] < 0 ? Color.blue : Color.red, triangulationDrawTime);
                Debug.DrawLine(p2, p0, he[j + 2] < 0 ? Color.blue : Color.red, triangulationDrawTime);
            }
        }
    }

    [BurstCompile]
    private static Triangulator<Vector2> Triangulate(NativeArray<Vector2> vertices, float maxArea)
    {
        var constraintEdges = new NativeList<int>(Allocator.Temp);
        for (int i = 0; i < vertices.Length; i++)
        {
            constraintEdges.Add(i);
            constraintEdges.Add((i + 1) % vertices.Length);
        }

        var settings = new TriangulationSettings()
        {
            RefineMesh = true,
            RefinementThresholds = { Angle = Mathf.PI / 6, Area = maxArea },
            RestoreBoundary = true
        };

        var triangulator = new Triangulator<Vector2>(Allocator.TempJob)
        {
            Input = { Positions = vertices, ConstraintEdges = constraintEdges.AsArray() },
            Settings = settings
        };

        triangulator.Run();

        constraintEdges.Dispose();

        return triangulator;
    }

    private static int PrevIndexInTriangle(int j)
    {
        var k = j % 3;
        return k switch
        {
            0 => j + 2,
            _ => j - 1
        };
    }

    private static int NextIndexInTriangle(int j)
    {
        var k = j % 3;
        return k switch
        {
            2 => j - 2,
            _ => j + 1
        };
    }

    private static (int, int) EdgeVertices(int edge, ReadOnlySpan<int> triangles)
    {
        var v0 = triangles[edge];
        var v1 = triangles[NextIndexInTriangle(edge)];
        return (v0, v1);
    }

    [BurstCompile]
    private static int NextBoundaryEdge(int bdryEdge, ReadOnlySpan<int> halfEdges)
    {
        var nextEdge = NextIndexInTriangle(bdryEdge);
        var he = halfEdges[nextEdge];
        while (!(he < 0))
        {
            nextEdge = NextIndexInTriangle(he);
            he = halfEdges[nextEdge];
        }
        return nextEdge;
    }

    [BurstCompile]
    private static void GetBoundaryEdges(NativeList<int> edges, ReadOnlySpan<int> halfEdges)
    {
        for (int i = 0; i < halfEdges.Length; i++)
        {
            if (halfEdges[i] < 0)
            {
                var edge0 = i;
                edges.Add(edge0);

                var edge = NextBoundaryEdge(edge0, halfEdges);
                while (edge != edge0)
                {
                    edges.Add(edge);
                    edge = NextBoundaryEdge(edge, halfEdges);
                }

                return;
            }
        }
    }

    [BurstCompile]
    private static void CalculateUV(NativeArray<Vector2> uv, ReadOnlySpan<Vector2> vertices, Vector2 bbMin, Vector2 bbMax)
    {
        var bbSpan = bbMax - bbMin;
        for (int i = 0; i < vertices.Length; i++)
        {
            var p = vertices[i];
            uv[i] = new((p.x - bbMin.x) / bbSpan.x, (p.y - bbMin.y) / bbSpan.y);
        }
    }

    //uv1.x = convexity wrt polygon interior (-1 = concave in toward interior, 1 = convex)    
    //uv1.y = "visibility" i.e. top-side highlight vs. underside shadow (-1 = shadow, 1 = highlight)
    [BurstCompile]
    private static void CalculateBoundaryUV1(NativeArray<Vector2> uv1, ReadOnlySpan<Vector2> vertices, ReadOnlySpan<int> triangles,
        ReadOnlySpan<int> boundaryEdges)
    {
        var e0 = boundaryEdges[^1];
        var e0Verts = EdgeVertices(e0, triangles);
        var u0 = (vertices[e0Verts.Item1] - vertices[e0Verts.Item2]).normalized;

        for (int i = 0; i < boundaryEdges.Length; i++)
        {
            var e1 = boundaryEdges[i];
            var e1Verts = EdgeVertices(e1, triangles);
            var u1 = (vertices[e1Verts.Item2] - vertices[e1Verts.Item1]).normalized;

            var convexity = MathTools.Cross2D(u0, u1);
            var outwardNormal = math.select(-math.sign(convexity) * (0.5f * (u0 + u1)).normalized, u1.CCWPerp(), convexity == 0);
            outwardNormal = math.select(outwardNormal, u1.CCWPerp(), outwardNormal.Equals(0));

            uv1[e1Verts.Item1] = new(convexity, outwardNormal.y);
            u0 = -u1;
        }
    }

    [BurstCompile]
    private static void CalculateInteriorUV1(NativeArray<Vector2> uv1, ReadOnlySpan<Vector2> vertices, ReadOnlySpan<int> triangles,
        ReadOnlySpan<int> boundaryEdges, float radiusX, float radiusY, float fadeRateX, float fadeRateY)
    {
        NativeArray<bool> isBdryVertex = new(vertices.Length, Allocator.Temp);
        NativeArray<Vector2> temp = new(uv1.Length, Allocator.Temp);
        temp.CopyFrom(uv1);

        NativeArray<float> radiusModifier = new(boundaryEdges.Length, Allocator.Temp);

        for (int i = 0; i < boundaryEdges.Length; i++)
        {
            isBdryVertex[triangles[boundaryEdges[i]]] = true;
            radiusModifier[i] = MathTools.RandomFloat(0, 1);
        }

        for (int i = 0; i < vertices.Length; i++)
        {
            var p = vertices[i];
            var countX = 0;
            var countY = 0;
            var sum = Vector2.zero;
            for (int j = 0; j < boundaryEdges.Length; j++)
            {
                var v = triangles[boundaryEdges[j]];
                var dist = math.distance(p, vertices[v]);

                var val = temp[v];
                var tX = math.clamp(1 - dist / radiusX, 0, 1);
                tX = math.pow(tX, fadeRateX);
                var tY = math.clamp(1 - dist / radiusY, 0, 1);
                tY = math.pow(tY, fadeRateY);
                sum += new Vector2(tX * val.x, tY * val.y);
                countX = math.select(countX, countX + 1, tX > 0);
                countY = math.select(countY, countY + 1, tY > 0);
            }

            uv1[i] = new Vector2(sum.x / math.max(countX, 1), sum.y / math.max(countY, 1));
        }
    }

    //uv2.x = distance to border (in local space)
    [BurstCompile]
    private static void CalculateUV2X(NativeArray<Vector2> uv2,
        ReadOnlySpan<Vector2> vertices, ReadOnlySpan<int> triangles, ReadOnlySpan<int> boundaryEdges)
    {
        for (int i = 0; i < vertices.Length; i++)
        {
            var p = vertices[i];
            var minDist2 = Mathf.Infinity;

            for (int j = 0; j < boundaryEdges.Length; j++)
            {
                var edge = EdgeVertices(boundaryEdges[j], triangles);
                var p0 = vertices[edge.Item1];
                var p1 = vertices[edge.Item2];

                var w = p1 - p0;
                var t = math.dot(p - p0, w) / math.lengthsq(w);
                t = math.select(0, t, t > 0 && !(t > 1));//if t not in (0, 1] just check left endpt of edge

                var thisDist2 = math.distancesq(p, p0 + t * w);
                minDist2 = math.min(thisDist2, minDist2);
            }

            uv2[i] = new Vector2(math.sqrt(minDist2), uv2[i].y);
        }
    }

    //uv2.y = crannies
    [BurstCompile]
    private static void CalculateUV2Y(NativeArray<Vector2> uv2, ReadOnlySpan<Vector2> vertices, ReadOnlySpan<int> triangles, ReadOnlySpan<int> halfEdges,
        int numCrannies, Vector2 initialVal, Vector2 spreadMin, Vector2 spreadMax, Vector2 spreadRandomizerMin,
        float crannyBorder, float crannyBorderIterations, Transform transform)
    {
        var queue = new NativeQueue<(int, Vector2, bool)>(Allocator.TempJob);
        var seen = new NativeArray<bool>(vertices.Length, Allocator.TempJob);

        for (int i = 0; i < numCrannies; i++)
        {
            var edge0 = MathTools.RNG.Next(triangles.Length - 1);
            var v0 = triangles[edge0];
            int j = 0;
            while (uv2[v0].x < crannyBorder && edge0 < triangles.Length - 1 && j < crannyBorderIterations * triangles.Length)
            {
                edge0++;
                j++;
                v0 = triangles[edge0];
            }
            var val0 = MathTools.RandomFloat(initialVal.x, initialVal.y);
            var spreadDist = new Vector2(MathTools.RandomFloat(spreadMin.x, spreadMax.x), MathTools.RandomFloat(spreadMin.y, spreadMax.y));
            Spread(edge0, new(0, val0), spreadDist, spreadRandomizerMin, uv2, queue, seen, vertices, triangles, halfEdges, transform);
        }
        queue.Dispose();
        seen.Dispose();
    }

    static void BlendSet(int i, Vector2 val, NativeArray<Vector2> arr)
    {
        var cur = arr[i];
        cur.x = 1 - (1 - cur.x) * (1 - val.x);
        cur.y = 1 - (1 - cur.y) * (1 - val.y);
        arr[i] = cur;
    }

    static void Spread(int edge0, Vector2 val0, Vector2 spreadRadius, Vector2 spreadRandomizerMin,
        NativeArray<Vector2> arr, NativeQueue<(int e, Vector2 radius, bool outgoing)> queue, NativeArray<bool> seen,
        ReadOnlySpan<Vector2> vertices, ReadOnlySpan<int> triangles, ReadOnlySpan<int> halfEdges, Transform transform)
    {
        var v0 = triangles[edge0];
        BlendSet(v0, val0, arr);

        seen.FillArray(false, 0, seen.Length);
        queue.Clear();
        queue.Enqueue((edge0, spreadRadius, true));
        seen[v0] = true;

        var origin = vertices[v0];
        while (queue.Count != 0)
        {
            //we need the flexibility of outgoing/incoming edges to be able to queue boundary edges (which only have one edge side, so we don't get to decide)
            var (e, r, outgoing) = queue.Dequeue();
            var v = Vertex(e, outgoing, triangles);

            if (spreadRandomizerMin.x < 1)
            {
                r.x *= MathTools.RandomFloat(spreadRandomizerMin.x, 1);
            }
            if (spreadRandomizerMin.y < 1)
            {
                r.y *= MathTools.RandomFloat(spreadRandomizerMin.y, 1);
            }

            //search CW first
            var f = e;
            var fOutgoing = outgoing;
            while (TryGetNextEdgeCW(f, fOutgoing, halfEdges, out f, out fOutgoing) && !EqualOrHalfEdges(e, f, halfEdges))
            {
                ScanNeighbor(f, fOutgoing, val0, r, v, origin, arr, queue, seen, vertices, triangles, halfEdges, transform);
            }

            //if we hit a boundary edge while rotating CW, so need to go back to start and scan CCW
            if (!EqualOrHalfEdges(e, f, halfEdges) || (!outgoing && halfEdges[e] < 0))
            {
                f = e;
                fOutgoing = outgoing;
                while (TryGetNextEdgeCCW(f, fOutgoing, halfEdges, out f, out fOutgoing) && !EqualOrHalfEdges(e, f, halfEdges))
                {
                    ScanNeighbor(f, fOutgoing, val0, r, v, origin, arr, queue, seen, vertices, triangles, halfEdges, transform);
                }
            }
        }

        static void ScanNeighbor(int f, bool fOutgoing, Vector2 val0, Vector2 spreadDist, int originVertex, Vector2 originPos,
            NativeArray<Vector2> arr, NativeQueue<(int, Vector2, bool)> queue, NativeArray<bool> seen, ReadOnlySpan<Vector2> vertices,
            ReadOnlySpan<int> triangles, ReadOnlySpan<int> halfEdges, Transform transform)
        {
            var w = Vertex(f, !fOutgoing, triangles);
            var q = vertices[w];
            if (!seen[w] && ShouldSpread(originPos, spreadDist, q, out var distToOrigin))
            {
                var tX = math.max(1 - distToOrigin / spreadDist.x, 0);
                var tY = math.max(1 - distToOrigin / spreadDist.y, 0);
                var val = new Vector2(tX * val0.x, tY * val0.y);
                BlendSet(w, val, arr);
                queue.Enqueue((f, spreadDist, !fOutgoing));
                seen[w] = true;
                Debug.DrawLine(transform.TransformPoint(vertices[originVertex]), transform.TransformPoint(q), Color.green, 5);
            }
        }

        static bool ShouldSpread(Vector2 origin, Vector2 spreadDist, Vector2 q, out float distToOrigin)
        {
            distToOrigin = math.distance(origin, q);
            return distToOrigin < spreadDist.x || distToOrigin < spreadDist.y;
        }

        static bool EqualOrHalfEdges(int edge1, int edge2, ReadOnlySpan<int> halfEdges)
        {
            return edge1 == edge2 || halfEdges[edge1] == edge2;
        }

        static int Vertex(int edge, bool outgoing, ReadOnlySpan<int> triangles)
        {
            return math.select(triangles[NextIndexInTriangle(edge)], triangles[edge], outgoing);
        }

        static bool TryGetNextEdgeCW(int edge, bool outgoing, ReadOnlySpan<int> halfEdges, out int nextEdge, out bool nextOutgoing)
        {
            var eOutgoing = math.select(halfEdges[edge], edge, outgoing);
            if (eOutgoing < 0)
            {
                nextEdge = edge;
                nextOutgoing = outgoing;
                return false;
            }

            nextEdge = PrevIndexInTriangle(eOutgoing);
            nextOutgoing = false;
            return true;
        }

        static bool TryGetNextEdgeCCW(int edge, bool outgoing, ReadOnlySpan<int> halfEdges, out int nextEdge, out bool nextOutgoing)
        {
            var eIncoming = math.select(edge, halfEdges[edge], outgoing);
            if (eIncoming < 0)
            {
                nextEdge = edge;
                nextOutgoing = outgoing;
                return false;
            }

            nextEdge = NextIndexInTriangle(eIncoming);
            nextOutgoing = true;
            return true;
        }
    }

    private void OnDrawGizmos()
    {
        if (drawPerimeterGizmo && perimeter != null && perimeter.Length > 0)
        {
            Gizmos.color = Color.blue;
            for (int i = 0; i < perimeter.Length - 1; i++)
            {
                Gizmos.DrawLine(transform.TransformPoint(perimeter[i]), transform.TransformPoint(perimeter[i + 1]));
            }
            Gizmos.DrawLine(transform.TransformPoint(perimeter[^1]), transform.TransformPoint(perimeter[0]));
        }
    }
}