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
        CalculateUV2X(uv2, positions, triangles);//uv2[i].x = area of polygon centered at vert i (i.e. sum of triangle areas) 
        CalculateUV2Y(uv1, uv2, positions, triangles, boundaryEdges);//uv1.y = distance to border

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
        var edgeNeighbor = halfEdges[nextEdge];
        while (!(edgeNeighbor < 0))
        {
            nextEdge = NextIndexInTriangle(edgeNeighbor);
            edgeNeighbor = halfEdges[nextEdge];
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
    private static void CalculateInteriorUV1(NativeArray<Vector2> uv1,  ReadOnlySpan<Vector2> vertices, ReadOnlySpan<int> triangles, 
        ReadOnlySpan<int> boundaryEdges, float radiusX, float radiusY, float fadeRateX, float fadeRateY)
    {
        NativeArray<bool> isBdryVertex = new(vertices.Length, Allocator.Temp);
        NativeArray<Vector2> temp = new (uv1.Length, Allocator.Temp);
        temp.CopyFrom(uv1);

        for (int i = 0; i < boundaryEdges.Length; i++)
        {
            isBdryVertex[triangles[boundaryEdges[i]]] = true;
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
                countY = math.select(countY, countY + 1, tY  > 0);
            }

            uv1[i] = new Vector2(sum.x / math.max(countX, 1), sum.y / math.max(countY, 1));
        }
    }

    //uv2.x = sum of areas of triangles meeting at that vertex
    [BurstCompile]
    private static void CalculateUV2X(NativeArray<Vector2> uv2, ReadOnlySpan<Vector2> vertices, ReadOnlySpan<int> triangles)
    {
        int i = 0;
        while (i < triangles.Length - 2)
        {
            var t0 = i++;
            var t1 = i++;
            var t2 = i++;

            var v0 = triangles[t0];
            var v1 = triangles[t1];
            var v2 = triangles[t2];

            AddArea(v0, v1, v2, uv2, vertices);
            AddArea(v1, v2, v0, uv2, vertices);
            AddArea(v2, v0, v1, uv2, vertices);
        }

        static void AddArea(int v0, int v1, int v2, NativeArray<Vector2> uv1, ReadOnlySpan<Vector2> vertices)
        {
            var cur = uv1[v0];
            cur.x += Area(v0, v1, v2, vertices);
            uv1[v0] = cur;
        }

        static float Area(int v0, int v1, int v2, ReadOnlySpan<Vector2> vertices)
        {
            var p0 = vertices[v0];
            var p1 = vertices[v1];
            var p2 = vertices[v2];
            return 0.5f * MathTools.Cross2D(p2 - p0, p1 - p0);
        }
    }

    //uv2.y = distance to border (in local space)
    [BurstCompile]
    private static void CalculateUV2Y(NativeArray<Vector2> uv1, NativeArray<Vector2> uv2,
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

                var v = p0 - p;
                var w = p1 - p0;
                var t = math.dot(p - p0, w) / math.lengthsq(w);
                t = math.select(0, t, t > 0 && !(t > 1));//if t not in (0, 1] just check left endpt of edge

                var thisDist2 = math.distancesq(p, p0 + t * w);
                minDist2 = math.min(thisDist2, minDist2);
            }

            uv2[i] = new Vector2(uv2[i].x, math.sqrt(minDist2));
        }
    }

    [BurstCompile]
    public static void SpreadFromBoundary(NativeArray<Vector2> data, ReadOnlySpan<Vector2> positions,
        ReadOnlySpan<int> triangles, ReadOnlySpan<int> halfEdges, ReadOnlySpan<int> boundaryEdges,
        float spreadRadiusMin, float spreadRadiusMax, Vector2 dataMax, float spreadRate, int iterations)
    {
        var queue = new NativeQueue<int>(Allocator.Temp);
        var seen = new NativeArray<bool>(positions.Length, Allocator.Temp);

        for (int iter = 0; iter < iterations; iter++)
        {
            for (int i = 0; i < boundaryEdges.Length; i++)
            {
                var e0 = boundaryEdges[i];
                var v0 = triangles[NextIndexInTriangle(e0)];//right endpt of edge e0
                var p0 = positions[v0];
                var d0 = data[v0];
                var radX = math.lerp(spreadRadiusMin, spreadRadiusMax, math.abs(d0.x) / dataMax.x);
                var radY = math.lerp(spreadRadiusMin, spreadRadiusMax, math.abs(d0.y) / dataMax.y);
                var radX2 = radX * radX;
                var radY2 = radY * radY;

                queue.Clear();
                seen.FillArray(false, 0, seen.Length);

                queue.Enqueue(e0);

                while (queue.Count != 0)
                {
                    var e = queue.Dequeue();//edge incoming to the vertex we want to scan
                    var nextEdge = NextIndexInTriangle(e);
                    var v = triangles[nextEdge];
                    var d = data[v];

                    seen[v] = true;
                    var nextV = triangles[NextIndexInTriangle(nextEdge)];

                    while (!seen[nextV])//scan all neighbors of vertex v
                    {
                        seen[nextV] = true;
                        var dist2 = math.distancesq(p0, positions[nextV]);

                        var inRangeX = dist2 < radX2;
                        var inRangeY = dist2 < radY2;

                        if (inRangeX || inRangeY)
                        {
                            var dist = math.sqrt(dist2);
                            var cur = data[nextV];

                            var tX = math.select(0, 1 - dist / radX, inRangeX) * spreadRate;
                            var x = math.lerp(cur.x, d.x, tX);
                            var tY = math.select(0, 1 - dist / radY, inRangeY) * spreadRate;
                            var y = math.lerp(cur.y, d.y, tY);
                            data[nextV] = new(x, y);

                            if (!(halfEdges[nextEdge] < 0))
                            {
                                nextEdge = halfEdges[nextEdge];
                                nextV = triangles[NextIndexInTriangle(nextEdge)];
                                queue.Enqueue(nextEdge);
                            }
                        }

                        //keeping the big picture in mind:
                        //i think we'll get nice results for uv1 (convexity & top/under side values)
                        //this gives us nice shading around the edges
                        //we need a way to give some geometry to the interior -- which i guess will come from smoothing out the area value, which we can maybe then use for highlight
                        //we could also try planting some random dots in interior and spreading (in non-circular, possibly random way to create little nooks and crannies)
                        //can also add "cracks" by just darkening certain edges (and following a random path with some random branching thrown in)
                        //these ideas i think would be a lot better than the crappy results from area
                        //(note uvs can be V2, V3, or V4 to accomodate the data you want)
                    }
                }
            }
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