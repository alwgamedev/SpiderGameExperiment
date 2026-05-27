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
    [SerializeField] float convexitySpread;
    [SerializeField] float convexityMax;
    [SerializeField] float concavitySpread;
    [SerializeField] float concavityMax;
    [SerializeField] float topsideSpread;
    [SerializeField] float topsideMax;
    [SerializeField] float undersideSpread;
    [SerializeField] float undersideMax;
    [SerializeField] Vector2 crannyInitialVal;
    [SerializeField] Vector2 crannySpread;
    [SerializeField] Vector2 numCranny;
    [SerializeField] float crannyBorder;
    [SerializeField] float crannyBorderIterations;
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
        var uv1 = new NativeArray<Vector4>(positions.Length, Allocator.Temp);
        var uv2 = new NativeArray<Vector2>(positions.Length, Allocator.Temp);

        CalculateUV(uv, positions, bbMin, bbMax);
        CalculateUV1(uv1, positions, triangles, boundaryEdges, halfEdges, convexitySpread, concavitySpread, topsideSpread, undersideSpread, 
            convexityMax, concavityMax, topsideMax, undersideMax);
        // CalculateInteriorUV1(uv1, positions, triangles, boundaryEdges, uv1ExtrapolationRadius.x, uv1ExtrapolationRadius.y, uv1FadeRate.x, uv1FadeRate.y);
        CalculateUV2X(uv2, positions, triangles, boundaryEdges);
        // var spreadMin = new Vector2(crannySpread.x, crannySpread.x);
        // var spreadMax = new Vector2(crannySpread.y, crannySpread.y);
        // var numCrannies = (int)math.ceil(MathTools.RandomFloat(numCranny.x, numCranny.y) * (bbMax.x - bbMin.x) * (bbMax.y - bbMin.y));
        // CalculateUV2Y(uv2, positions, triangles, halfEdges, numCrannies, crannyInitialVal, spreadMin, spreadMax,
        //     crannyBorder, crannyBorderIterations, transform);

        mesh = new();

        NativeArray<Vector3> positionsV3 = new(positions.Length, Allocator.Temp);
        for (int i = 0; i < positionsV3.Length; i++)
        {
            positionsV3[i] = positions[i];
        }

        mesh.SetVertices(positionsV3);
        mesh.SetIndices(triangles.AsArray(), MeshTopology.Triangles, 0);
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

    //uv1.x = convexity
    //uv1.y = concavity 
    //uv1.z = top side highlight
    //uv1.w = underside shadow
    //keep them separate so that we can decide how they are weighted and how they blend in the shader
    //maxes are enforced so that
        //a) "BlendSet" doesn't stack too quickly -- we should probably use a better blend function...
        //b) reduce the extremes (e.g. if i have one very concave region and another not so concave that i want to stand out more, i can increase
            //the concavity strength without making region A become over-saturated)
    [BurstCompile]
    private static void CalculateUV1(NativeArray<Vector4> uv1, ReadOnlySpan<Vector2> vertices, ReadOnlySpan<int> triangles,
        ReadOnlySpan<int> boundaryEdges, ReadOnlySpan<int> halfEdges,
        float convexitySpread, float concavitySpread, float topsideSpread, float undersideSpread,
        float convexityMax, float concavityMax, float topsideMax, float undersideMax)
    {
        var e0 = boundaryEdges[^1];
        var e0Verts = EdgeVertices(e0, triangles);
        var u0 = (vertices[e0Verts.Item1] - vertices[e0Verts.Item2]).normalized;

        NativeQueue<(int, bool)> spreadQueue = new(Allocator.Temp);
        NativeArray<bool> seen = new(vertices.Length, Allocator.Temp);

        for (int i = 0; i < boundaryEdges.Length; i++)
        {
            var e1 = boundaryEdges[i];
            var e1Verts = EdgeVertices(e1, triangles);
            var u1 = (vertices[e1Verts.Item2] - vertices[e1Verts.Item1]).normalized;

            var convexity = MathTools.Cross2D(u0, u1);
            var outwardNormal = math.select(-math.sign(convexity) * (0.5f * (u0 + u1)).normalized, u1.CCWPerp(), convexity == 0);
            outwardNormal = math.select(outwardNormal, u1.CCWPerp(), outwardNormal.Equals(0));
            var topside = outwardNormal.y;

            if (convexity != 0)
            {
                var offset = math.select(1, 0, convexity > 0);//which coordinate of the vector4 we're writing to
                var spread = math.select(concavitySpread, convexitySpread, convexity > 0);
                convexity = math.select(math.min(-convexity, concavityMax), math.min(convexity, convexityMax), convexity > 0);
                Spread(e1, true, convexity, spread, uv1.Reinterpret<float>(16), 4, offset, spreadQueue, seen, vertices, triangles, halfEdges);
            }
            if (topside != 0)
            {
                var offset = math.select(3, 2, topside > 0);
                var spread = math.select(undersideSpread, topsideSpread, topside > 0);
                topside = math.select(math.min(-topside, undersideMax), math.min(topside, topsideMax), topside > 0);
                Spread(e1, true, topside, spread, uv1.Reinterpret<float>(16), 4, offset, spreadQueue, seen, vertices, triangles, halfEdges);
            }

            u0 = -u1;
        }

        //output in [-1, 1]
        // static float Remap(float a, Vector2 range)
        // {
        //     var midpt = 0.5f * (range.x + range.y);
        //     var halfWidth = range.y - midpt;
        //     return math.clamp((a - midpt) / halfWidth, -1, 1);
        // }
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
    // [BurstCompile]
    // private static void CalculateUV2Y(NativeArray<Vector2> uv2, ReadOnlySpan<Vector2> vertices, ReadOnlySpan<int> triangles, ReadOnlySpan<int> halfEdges,
    //     int numCrannies, Vector2 initialVal, Vector2 spreadMin, Vector2 spreadMax,
    //     float crannyBorder, float crannyBorderIterations, Transform transform)
    // {
    //     var queue = new NativeQueue<(int, Vector2, bool)>(Allocator.TempJob);
    //     var seen = new NativeArray<bool>(vertices.Length, Allocator.TempJob);

    //     for (int i = 0; i < numCrannies; i++)
    //     {
    //         var edge0 = MathTools.RNG.Next(triangles.Length - 1);
    //         var v0 = triangles[edge0];
    //         int j = 0;
    //         while (uv2[v0].x < crannyBorder && edge0 < triangles.Length - 1 && j < crannyBorderIterations * triangles.Length)
    //         {
    //             edge0++;
    //             j++;
    //             v0 = triangles[edge0];
    //         }
    //         var val0 = MathTools.RandomFloat(initialVal.x, initialVal.y);
    //         var spreadDist = new Vector2(MathTools.RandomFloat(spreadMin.x, spreadMax.x), MathTools.RandomFloat(spreadMin.y, spreadMax.y));
    //         Spread(edge0, true, new(0, val0), spreadDist, uv2, queue, seen, vertices, triangles, halfEdges, transform);
    //     }
    //     queue.Dispose();
    //     seen.Dispose();
    // }

    // static void BlendSet(int i, Vector2 val, NativeArray<Vector2> arr)
    // {
    //     var cur = arr[i];
    //     arr[i] = new(BlendSet(cur.x, val.x), BlendSet(cur.y, val.y));
    // }

    private static void BlendSet(int i, int stride, int offset, float val, NativeArray<float> arr)
    {
        int j = stride * i + offset;
        var cur = arr[j];
        arr[j] = BlendSet(cur, val);
    }

    private static float BlendSet(float cur, float blend)
    {
        return cur + (1 - math.abs(cur)) * blend;// = lerp(cur, sign(cur), sign(cur) * blend);
    }

    /// <summary> edg0 is taken as an outgoing edge from the vertex where we want to spread</summary>
    static void Spread(int edge0, bool outgoing0, float val0, float spreadRadius, 
        NativeArray<float> arr, int stride, int offset, NativeQueue<(int e, bool outgoing)> queue, NativeArray<bool> seen,
        ReadOnlySpan<Vector2> vertices, ReadOnlySpan<int> triangles, ReadOnlySpan<int> halfEdges)
    {
        var v0 = Vertex(edge0, outgoing0, triangles);
        // BlendSet(v0, val0, arr);
        BlendSet(v0, stride, offset, val0, arr);

        seen.FillArray(false, 0, seen.Length);
        queue.Clear();
        queue.Enqueue((edge0, outgoing0));
        seen[v0] = true;

        var origin = vertices[v0];
        while (queue.Count != 0)
        {
            //we need the flexibility of outgoing/incoming edges to be able to queue boundary edges (which only have one edge side, so we don't get to decide)
            var (e, outgoing) = queue.Dequeue();
            var v = Vertex(e, outgoing, triangles);

            //search CW first
            var f = e;
            var fOutgoing = outgoing;
            while (TryGetNextEdgeCW(f, fOutgoing, halfEdges, out f, out fOutgoing) && !EqualOrHalfEdges(e, f, halfEdges))
            {
                ScanNeighbor(f, fOutgoing, val0, spreadRadius, origin, arr, stride, offset, queue, seen, vertices, triangles, halfEdges);
            }

            //if we hit a boundary edge while rotating CW, so need to go back to start and scan CCW
            if (!EqualOrHalfEdges(e, f, halfEdges) || (!outgoing && halfEdges[e] < 0))
            {
                f = e;
                fOutgoing = outgoing;
                while (TryGetNextEdgeCCW(f, fOutgoing, halfEdges, out f, out fOutgoing) && !EqualOrHalfEdges(e, f, halfEdges))
                {
                    ScanNeighbor(f, fOutgoing, val0, spreadRadius, origin, arr, stride, offset, queue, seen, vertices, triangles, halfEdges);
                }
            }
        }

        static void ScanNeighbor(int f, bool fOutgoing, float val0, float spreadDist, Vector2 originPos,
            NativeArray<float> arr, int stride, int offset, NativeQueue<(int, bool)> queue, NativeArray<bool> seen, 
            ReadOnlySpan<Vector2> vertices, ReadOnlySpan<int> triangles, ReadOnlySpan<int> halfEdges)
        {
            var w = Vertex(f, !fOutgoing, triangles);
            var q = vertices[w];
            if (!seen[w] && ShouldSpread(originPos, spreadDist, q, out var distToOrigin))
            {
                var t = math.max(1 - distToOrigin / spreadDist, 0);
                BlendSet(w, stride, offset, t * val0, arr);
                queue.Enqueue((f, !fOutgoing));
                seen[w] = true;
            }
        }

        static bool ShouldSpread(Vector2 origin, float spreadDist, Vector2 q, out float distToOrigin)
        {
            distToOrigin = math.distance(origin, q);
            return distToOrigin < spreadDist;
        }

        static bool EqualOrHalfEdges(int edge1, int edge2, ReadOnlySpan<int> halfEdges)
        {
            return edge1 == edge2 || halfEdges[edge1] == edge2;
        }

        static int Vertex(int edge, bool outgoing, ReadOnlySpan<int> triangles)
        {
            return math.select(triangles[NextIndexInTriangle(edge)], triangles[edge], outgoing);
        }

        //next edge with same endpt
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

        //next edge with same endpt
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