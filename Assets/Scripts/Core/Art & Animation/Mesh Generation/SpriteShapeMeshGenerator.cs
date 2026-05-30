using andywiecko.BurstTriangulator;
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
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
    [SerializeField] float numCracksMin;//per unit area
    [SerializeField] float numCracksMax;
    [SerializeField] int crackMinDepth;
    [SerializeField] int crackMaxDepth;
    [SerializeField] float crackContinueChance;
    [SerializeField] float crackBranchChance;
    [SerializeField] float crackMinVal;
    [SerializeField] float crackMaxVal;
    // [SerializeField] float crackSpread;
    [SerializeField] Vector2[] perimeter;
    [SerializeField] bool drawPerimeterGizmo;
    [SerializeField] float triangulationDrawTime;
    [SerializeField] float crackDrawTime;

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
        var triangulator = Triangulate(vertices.AsArray(), maxTriangleArea);//TempJob allocated
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

        var vertexGrid = BuildPointGrid(positions.AsArray(), bbMin, bbMax, 1);

        var uv = new NativeArray<Vector2>(positions.Length, Allocator.Temp);
        var uv1 = new NativeArray<Vector4>(positions.Length, Allocator.Temp);
        var uv2 = new NativeArray<Vector4>(positions.Length, Allocator.Temp);
        var uv2Float = uv2.Reinterpret<float>(16);

        var seed = (uint)MathTools.RNG.Next(1, int.MaxValue);
        var rng = new Unity.Mathematics.Random(seed);

        CalculateUV(uv, positions, bbMin, bbMax);
        CalculateUV1(uv1, positions, triangles, boundaryEdges, vertexGrid,
            convexitySpread, concavitySpread, topsideSpread, undersideSpread,
            convexityMax, concavityMax, topsideMax, undersideMax);
        CalculateDistToBorder(uv2Float, 4, 0, positions, triangles, boundaryEdges);
        CalculateCracks(uv2Float, 4, 1, 2, positions, triangles, halfEdges, vertexGrid, numCracksMin, numCracksMax, crackMinDepth, crackMaxDepth,
            crackContinueChance, crackBranchChance, crackMinVal, crackMaxVal, ref rng, transform, crackDrawTime);
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
        vertexGrid.Dispose();
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


    private void DrawTriangulationOutput(Triangulator<Vector2> triangulator)
    {
        if (triangulationDrawTime > 0)
        {
            var output = triangulator.Output;
            var p = output.Positions;
            var t = output.Triangles;
            var he = output.Halfedges;

            for (int i = 0; i < output.Triangles.Length; i += 3)
            {
                var t0 = t[i];
                var t1 = t[i + 1];
                var t2 = t[i + 2];
                var p0 = transform.TransformPoint(p[t0]);
                var p1 = transform.TransformPoint(p[t1]);
                var p2 = transform.TransformPoint(p[t2]);

                Debug.DrawLine(p0, p1, he[i] < 0 ? Color.blue : Color.red, triangulationDrawTime);
                Debug.DrawLine(p1, p2, he[i + 1] < 0 ? Color.blue : Color.red, triangulationDrawTime);
                Debug.DrawLine(p2, p0, he[i + 2] < 0 ? Color.blue : Color.red, triangulationDrawTime);
            }
        }
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
        ReadOnlySpan<int> boundaryEdges, PointGrid vertexGrid,
        float convexitySpread, float concavitySpread, float topsideSpread, float undersideSpread,
        float convexityMax, float concavityMax, float topsideMax, float undersideMax)
    {
        var e0 = boundaryEdges[^1];
        var e0Verts = EdgeVertices(e0, triangles);
        var u0 = (vertices[e0Verts.Item1] - vertices[e0Verts.Item2]).normalized;
        
        var uv1Float = uv1.Reinterpret<float>(16);

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
                var spreadRadius = math.select(concavitySpread, convexitySpread, convexity > 0);
                convexity = math.select(math.min(-convexity, concavityMax), math.min(convexity, convexityMax), convexity > 0);

                var p = vertices[e1Verts.Item1];
                Spread(p, convexity, spreadRadius, uv1Float, 4, offset, vertices, vertexGrid);
            }
            if (topside != 0)
            {
                var offset = math.select(3, 2, topside > 0);
                var spreadRadius = math.select(undersideSpread, topsideSpread, topside > 0);
                topside = math.select(math.min(-topside, undersideMax), math.min(topside, topsideMax), topside > 0);

                var p = vertices[e1Verts.Item1];
                Spread(p, topside, spreadRadius, uv1Float, 4, offset, vertices, vertexGrid);
            }

            u0 = -u1;
        }
    }

    [BurstCompile]
    private static void CalculateDistToBorder(NativeArray<float> arr, int stride, int offset,
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

            arr[stride * i + offset] = math.sqrt(minDist2);
        }
    }

    //uv2.y = cracks
    [BurstCompile]
    private static void CalculateCracks(NativeArray<float> arr, int stride, int offset1, int offset2,
        ReadOnlySpan<Vector2> vertices, ReadOnlySpan<int> triangles, ReadOnlySpan<int> halfEdges, PointGrid vertexGrid, 
        float numCracksMin, float numCracksMax, int minDepth, int maxDepth, float continueChance, float branchChance,
        float crackMinValue, float crackMaxValue, ref Unity.Mathematics.Random rng,
        Transform transform = null, float drawTime = 0)
    {
        NativeArray<bool> seen = new(vertices.Length, Allocator.Temp);
        NativeList<int> validChildren = new(Allocator.Temp);

        var bbArea = vertexGrid.cellSize * vertexGrid.cellSize * vertexGrid.NumCells;
        var numCracks = (int)(MathTools.RandomFloat(numCracksMin, numCracksMax) * bbArea);
        for (int i = 0; i < numCracks; i++)
        {
            var depthMax = rng.NextInt(minDepth, maxDepth + 1);
            var crack = GenerateCrack(seen, validChildren, vertices, triangles, halfEdges, vertexGrid, depthMax, continueChance, branchChance, ref rng);
            if (!crack.nodes.IsCreated)
            {
                continue;
            }

            for (int j = 0; j < crack.nodes.Length; j++)
            {
                var node = crack.nodes[j];
                var vert = Vertex(node.edge, node.outgoing != 0, triangles);

                BlendIn(vert, stride, offset1, rng.NextFloat(crackMinValue, crackMaxValue), arr);

                // var offset = offset1;
                // if (j > 0)
                // {
                //     var parentVert = Vertex(node.edge, node.outgoing == 0, triangles);
                //     bool useOffset1 = arr[stride * parentVert + offset1] != 0;
                //     offset = math.select(offset2, offset1, useOffset1);

                //     var oppVert = Vertex(PrevIndexInTriangle(node.edge), true, triangles);
                //     if (arr[stride * oppVert + offset] == 0 && !(halfEdges[node.edge] < 0))
                //     {
                //         oppVert = Vertex(PrevIndexInTriangle(halfEdges[node.edge]), true, triangles);
                //     }

                //     if (arr[stride * oppVert + offset] != 0)
                //     {
                //         useOffset1 = !useOffset1;
                //         offset = math.select(offset2, offset1, useOffset1);
                //         arr[stride * parentVert + offset] = 1;
                //     }
                // }

                // arr[stride * vert + offset] = 1;
            }

            if (transform)
            {
                crack.DebugDraw(vertices, triangles, transform, drawTime);
            }
        }
    }

    struct PointGrid
    {
        public NativeArray<int> grid;
        public NativeArray<int> cellStart;
        public Vector2 origin;//position of the lower left corner of the grid
        public float cellSize;
        public int gridWidth;
        public int gridHeight;

        public readonly int NumCells => cellStart.Length - 1;

        public readonly int Cell(int row, int col) => row * gridWidth + col;

        public readonly int CellOccupancy(int cell) => cellStart[cell + 1] - cellStart[cell];

        public readonly (int row, int col) Cell(Vector2 p)
        {
            p -= origin;
            var col = (int)math.clamp(p.x / cellSize, 0, gridWidth - 1);
            var row = (int)math.clamp(p.y / cellSize, 0, gridHeight - 1);
            return (row, col);
        }

        public (int colMin, int colMax, int rowMin, int rowMax) GetIterBounds(Vector2 minPt, Vector2 maxPt)
        {
            var (rowMin, colMin) = Cell(minPt);
            var (rowMax, colMax) = Cell(maxPt);
            return (colMin, colMax + 1, rowMin, rowMax + 1);
        }

        public void Dispose()
        {
            if (grid.IsCreated)
            {
                grid.Dispose();
            }

            if (cellStart.IsCreated)
            {
                cellStart.Dispose();
            }
        }
    }

    /// <summary> Natives in the PointGrid are TempJob allocated. </summary>
    [BurstCompile]
    private static PointGrid BuildPointGrid(NativeArray<Vector2> points, Vector2 bbMin, Vector2 bbMax, float cellSize)
    {
        var bbSpan = bbMax - bbMin;
        var gridWidth = (int)Mathf.Ceil(bbSpan.x / cellSize);
        var gridHeight = (int)Mathf.Ceil(bbSpan.y / cellSize);
        var numCells = gridWidth * gridHeight;

        var grid = new NativeArray<int>(points.Length, Allocator.TempJob);
        var cellStart = new NativeArray<int>(numCells + 1, Allocator.TempJob);

        NativeArray<int> cellCount = new(numCells, Allocator.Temp);

        //first count the number of points in each cell
        for (int i = 0; i < points.Length; i++)
        {
            var p = points[i] - bbMin;
            var row = (int)(p.y / cellSize);
            var col = (int)(p.x / cellSize);
            var cell = row * gridWidth + col;
            cellCount[cell]++;
        }

        //set the cell start indices
        var sum = 0;
        for (int i = 0; i < cellCount.Length; i++)
        {
            cellStart[i] = sum;
            sum += cellCount[i];
        }
        cellStart[numCells] = grid.Length;

        //fill the cells
        for (int i = 0; i < points.Length; i++)
        {
            var p = points[i] - bbMin;
            var row = (int)(p.y / cellSize);
            var col = (int)(p.x / cellSize);
            var cell = row * gridWidth + col;
            var j = cellStart[cell] + --cellCount[cell];
            grid[j] = i;
        }

        return new()
        {
            grid = grid,
            cellStart = cellStart,
            origin = bbMin,
            cellSize = cellSize,
            gridWidth = gridWidth,
            gridHeight = gridHeight
        };
    }

    private static void BlendIn(int i, int stride, int offset, float val, NativeArray<float> arr)
    {
        int j = stride * i + offset;
        var cur = arr[j];
        arr[j] = BlendIn(cur, val);
    }

    private static float BlendIn(float cur, float val)
    {
        return cur + 0.5f * (1 - math.abs(cur)) * val;
    }

    static void Spread(Vector2 pos, float val, float radius, NativeArray<float> arr, int stride, int offset,
        ReadOnlySpan<Vector2> positions, PointGrid pointGrid)
    {
        var minPt = new Vector2(pos.x - radius, pos.y - radius);
        var maxPt = new Vector2(pos.x + radius, pos.y + radius);
        var (colMin, colMax, rowMin, rowMax) = pointGrid.GetIterBounds(minPt, maxPt);

        var grid = pointGrid.grid;
        var cellStart = pointGrid.cellStart;
        var r2 = radius * radius;

        for (int col = colMin; col < colMax; col++)
        {
            for (int row = rowMin; row < rowMax; row++)
            {
                var cell = pointGrid.Cell(row, col);
                for (int i = cellStart[cell]; i < cellStart[cell + 1]; i++)
                {
                    var v = grid[i];
                    var p = positions[v];
                    var dist2 = math.distancesq(pos, p);
                    if (dist2 < r2)
                    {
                        var t = math.clamp(1 - math.sqrt(dist2) / radius, 0, 1);
                        BlendIn(v, stride, offset, t * val, arr);
                    }
                }
            }
        }
    }

    struct CrackNode
    {
        public int edge;
        public ushort outgoing;//0 for false
        public ushort depth;
        // public ushort childStart;
        // public ushort childEnd;
    }

    struct Crack
    {
        public NativeArray<CrackNode> nodes;
        public Vector2 bbMin;
        public Vector2 bbMax;
        //^for finding grid bounding box when spreading

        public void Dispose()
        {
            if (nodes.IsCreated)
            {
                nodes.Dispose();
            }
        }

        // public float DistanceSqrd(Vector2 p, ReadOnlySpan<Vector2> vertices, ReadOnlySpan<int> triangles)
        // {
        //     var 
        //     float minDist2 = math.INFINITY;

        //     //first check distances to 
        //     for (int i = 0; i < nodes.Length; i++)
        //     {
        //         // var node = nodes[i];
        //         // var pt = vertices[node.vertex];
        //         // minDist2 = math.min(minDist2, math.distancesq(p, pt));

        //         // for (int j = node.childStart; j < node.childEnd; j++)
        //         // {
        //         //     var child = nodes[j];
        //         //     var childPt = vertices[child.vertex];
        //         //     var edge = childPt - pt;
        //         //     var v = p - pt;
        //         //     var x = math.dot(v, edge);
        //         //     var y = math.dot(v, edge.CCWPerp());
        //         //     var dist2 = y * y / math.lengthsq(edge);

        //         //     minDist2 = math.select(minDist2, dist2, x > 0 && x < 1 && dist2 < minDist2);

        //         //     //we'll add bending once we get the basics set up
        //         // }
        //     }

        //     return minDist2;
        // }

        public void DebugDraw(ReadOnlySpan<Vector2> vertices, ReadOnlySpan<int> triangles, Transform transform, float drawTime)
        {
            for (int i = 1; i < nodes.Length; i++)
            {
                var m = nodes[i];
                var v = Vertex(m.edge, m.outgoing != 0, triangles);
                var w = Vertex(m.edge, m.outgoing == 0, triangles);
                var p = transform.TransformPoint(vertices[v]);
                var q = transform.TransformPoint(vertices[w]);
                Debug.DrawLine(p, q, Color.green, drawTime);
                // var p = transform.TransformPoint(vertices[m.vertex]);

                // for (int j = m.childStart; j < m.childEnd; j++)
                // {
                //     var n = nodes[j];
                //     var q = transform.TransformPoint(vertices[n.vertex]);
                //     Debug.DrawLine(p, q, Color.green, drawTime);
                // }
            }
        }
    }

    static Crack GenerateCrack(NativeArray<bool> seen, NativeList<int> validChildren,
        ReadOnlySpan<Vector2> vertices, ReadOnlySpan<int> triangles, ReadOnlySpan<int> halfEdges, PointGrid vertexGrid,
        int depthMax, float continueChance, float branchChance, ref Unity.Mathematics.Random rng)
    {
        //randomize by cell to get cracks more evenly distributed across mesh.
        //(there are a lot more vertices near the boundary, so if we just choose a random vertex
        //we end up with most of the cracks near the boundary)
        int attempts = 0;
        int edge = -1;
        bool outgoing = false;
        while (edge < 0 && attempts < 5)
        {
            attempts++;

            //pick a random cell
            var randomCell = rng.NextInt(vertexGrid.NumCells);
            var cellOccupancy = vertexGrid.CellOccupancy(randomCell);
            if (cellOccupancy == 0)
            {
                continue;
            }

            //pick a random vertex in that cell
            var vertInCell = rng.NextInt(cellOccupancy);
            var vert = vertexGrid.grid[vertexGrid.cellStart[randomCell] + vertInCell];
            var vertShifts = 0;
            while (seen[vert] && vertShifts < cellOccupancy - 1)
            {
                vertInCell = (vertInCell + 1) % cellOccupancy;
                vert = vertexGrid.grid[vertexGrid.cellStart[randomCell] + vertInCell];
                vertShifts++;
            }

            if (seen[vert])
            {
                continue;
            }

            //find an edge containing that vertex, and make sure there is at least one neighboring vertex that hasn't been seen
            for (int i = 0; i < triangles.Length; i++)
            {
                var edgeVerts = EdgeVertices(i, triangles);
                if ((edgeVerts.Item1 == vert && !seen[edgeVerts.Item2]) || (edgeVerts.Item2 == vert && !seen[edgeVerts.Item1]))
                {
                    edge = i;
                    outgoing = edgeVerts.Item1 == vert;
                    break;
                }
            }
        }

        if (edge < 0)
        {
            return default;
        }

        NativeList<CrackNode> nodes = new(Allocator.Temp);
        Vector2 bbMin = new(float.MinValue, float.MinValue);
        Vector2 bbMax = new(float.MaxValue, float.MaxValue);

        int numChildren0 = rng.NextInt(1, 4);
        QueueCrackNode(edge, outgoing, 0, nodes, seen, vertices, triangles, ref bbMin, ref bbMax);
        AddNodeChildren(0, numChildren0, nodes, seen, validChildren, vertices, triangles, halfEdges, ref bbMin, ref bbMax, ref rng);

        int j = 1;
        while (j < nodes.Length)
        {
            //choose btwn 0-2 children (if depth == depthMax, no children)
            var depth = nodes[j].depth;//nodes[j].childEnd;
            var childRoll = rng.NextFloat();
            var numChildren = math.select(0, math.select(1, 2, childRoll < branchChance), depth < depthMax && childRoll < continueChance);
            AddNodeChildren(j, numChildren, nodes, seen, validChildren, vertices, triangles, halfEdges, ref bbMin, ref bbMax, ref rng);
            j++;
        }

        return new Crack() { nodes = nodes.AsArray(), bbMin = bbMin, bbMax = bbMax };

        //when node is first queued, it'll store (edge, outgoing, depth)
        //correct data will be entered when the node is processed by AddNodeChildren
        static void QueueCrackNode(int edge, bool outgoing, int depth, NativeList<CrackNode> nodes, NativeArray<bool> seen,
            ReadOnlySpan<Vector2> vertices, ReadOnlySpan<int> triangles, ref Vector2 bbMin, ref Vector2 bbMax)
        {
            nodes.Add(new() { depth = (ushort)depth, edge = edge, outgoing = (ushort)math.select(0, 1, outgoing) });
                //{ vertex = edge, childStart = (ushort)math.select(0, 1, outgoing), childEnd = (ushort)depth });
            var v = Vertex(edge, outgoing, triangles);
            var p = vertices[v];
            bbMin = math.min(p, bbMin);
            bbMax = math.max(p, bbMax);
            seen[v] = true;
        }

        static void AddNodeChildren(int i, int numChildren, NativeList<CrackNode> nodes, NativeArray<bool> seen, NativeList<int> validChildren,
            ReadOnlySpan<Vector2> vertices, ReadOnlySpan<int> triangles, ReadOnlySpan<int> halfEdges, ref Vector2 bbMin, ref Vector2 bbMax,
            ref Unity.Mathematics.Random rng)
        {
            var node = nodes[i];
            var edge = node.edge;
            var outgoing = node.outgoing != 0;
            var depth = node.depth;
            var vert = Vertex(edge, outgoing, triangles);
            var p = vertices[vert];

            // node.vertex = Vertex(edge, outgoing, triangles);
            // node.childStart = (ushort)nodes.Length;

            if (numChildren != 0)
            {
                validChildren.Clear();

                var f = edge;
                var fOutgoing = outgoing;
                if (i == 0)
                {
                    validChildren.Add(f);
                }

                var parentVert = Vertex(edge, !outgoing, triangles);
                var p0 = vertices[parentVert];
                var u = math.select(0, (p - p0).normalized, i > 0);

                //first find all valid children (vertex not seen and edge direction has dot > 0 with previous edge)
                while (TryGetNextEdgeCCW(f, fOutgoing, halfEdges, out f, out fOutgoing) && !EqualOrHalfEdges(f, edge, halfEdges))
                {
                    var w = Vertex(f, !fOutgoing, triangles);
                    var opp = Vertex(PrevIndexInTriangle(f), true, triangles);
                    if (!seen[opp] && !(halfEdges[f] < 0))
                    {
                        opp = Vertex(PrevIndexInTriangle(halfEdges[f]), true, triangles);
                    }
                    if (!seen[w] && !seen[opp] && math.dot(vertices[w] - p, u) > 0)
                    {
                        validChildren.Add(f);
                    }
                }

                //if we hit a boundary edge while scanning CCW, go back to start and scan CW to make sure we get all neighbors
                if (!EqualOrHalfEdges(f, edge, halfEdges) || (outgoing && halfEdges[edge] < 0))
                {
                    f = edge;
                    fOutgoing = outgoing;
                    while (TryGetNextEdgeCW(f, fOutgoing, halfEdges, out f, out fOutgoing) && !EqualOrHalfEdges(f, edge, halfEdges))
                    {
                        var w = Vertex(f, !fOutgoing, triangles);
                        if (!seen[w] && math.dot(vertices[w] - p, u) > 0)
                        {
                            validChildren.Add(f);
                        }
                    }
                }

                int numValid = validChildren.Length;
                while (numChildren > 0 && numValid > 0)
                {
                    var j = rng.NextInt(numValid);
                    int numValidSeen = 0;
                    int lastScanned = -1;
                    while (numValidSeen < j + 1)
                    {
                        lastScanned++;
                        if (!(validChildren[lastScanned] < 0))
                        {
                            numValidSeen++;
                        }
                    }

                    var child = validChildren[lastScanned];
                    var childOutgoing = triangles[child] != vert;
                    QueueCrackNode(child, childOutgoing, depth + 1, nodes, seen, vertices, triangles, ref bbMin, ref bbMax);
                    validChildren[lastScanned] = -69;
                    numValid--;
                    numChildren--;
                }
            }

            // node.childEnd = (ushort)nodes.Length;
            // nodes[i] = node;
        }
    }

    // static void SpreadCrack(Crack crack, float val, float radius, NativeArray<float> arr, int stride, int offset,
    //     ReadOnlySpan<Vector2> positions, PointGrid pointGrid)
    // {
    //     var (colMin, colMax, rowMin, rowMax) = pointGrid.GetIterBounds(crack.bbMin, crack.bbMax);

    //     var grid = pointGrid.grid;
    //     var cellStart = pointGrid.cellStart;
    //     var r2 = radius * radius;

    //     for (int col = colMin; col < colMax; col++)
    //     {
    //         for (int row = rowMin; row < rowMax; row++)
    //         {
    //             var cell = pointGrid.Cell(row, col);
    //             for (int i = cellStart[cell]; i < cellStart[cell + 1]; i++)
    //             {
    //                 var v = grid[i];
    //                 var p = positions[v];
    //                 var dist2 = crack.DistanceSqrd(p, positions);
    //                 if (dist2 < r2)
    //                 {
    //                     var t = math.clamp(1 - math.sqrt(dist2) / radius, 0, 1);
    //                     BlendIn(v, stride, offset, t * val, arr);
    //                 }
    //             }
    //         }
    //     }
    // }

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

    // static void Spread(int edge0, bool outgoing0, float val0, float spreadRadius, 
    //     NativeArray<float> arr, int stride, int offset, NativeQueue<(int e, bool outgoing)> queue, NativeArray<bool> seen,
    //     ReadOnlySpan<Vector2> vertices, ReadOnlySpan<int> triangles, ReadOnlySpan<int> halfEdges)
    // {
    //     var v0 = Vertex(edge0, outgoing0, triangles);
    //     // BlendSet(v0, val0, arr);
    //     BlendIn(v0, stride, offset, val0, arr);

    //     seen.FillArray(false, 0, seen.Length);
    //     queue.Clear();
    //     queue.Enqueue((edge0, outgoing0));
    //     seen[v0] = true;

    //     var origin = vertices[v0];
    //     while (queue.Count != 0)
    //     {
    //         //we need the flexibility of outgoing/incoming edges to be able to queue boundary edges (which only have one edge side, so we don't get to decide)
    //         var (e, outgoing) = queue.Dequeue();
    //         var v = Vertex(e, outgoing, triangles);

    //         //search CW first
    //         var f = e;
    //         var fOutgoing = outgoing;
    //         while (TryGetNextEdgeCW(f, fOutgoing, halfEdges, out f, out fOutgoing) && !EqualOrHalfEdges(e, f, halfEdges))
    //         {
    //             ScanNeighbor(f, fOutgoing, val0, spreadRadius, origin, arr, stride, offset, queue, seen, vertices, triangles, halfEdges);
    //         }

    //         //if we hit a boundary edge while rotating CW, so need to go back to start and scan CCW
    //         if (!EqualOrHalfEdges(e, f, halfEdges) || (!outgoing && halfEdges[e] < 0))
    //         {
    //             f = e;
    //             fOutgoing = outgoing;
    //             while (TryGetNextEdgeCCW(f, fOutgoing, halfEdges, out f, out fOutgoing) && !EqualOrHalfEdges(e, f, halfEdges))
    //             {
    //                 ScanNeighbor(f, fOutgoing, val0, spreadRadius, origin, arr, stride, offset, queue, seen, vertices, triangles, halfEdges);
    //             }
    //         }
    //     }

    //     static void ScanNeighbor(int f, bool fOutgoing, float val0, float spreadDist, Vector2 originPos,
    //         NativeArray<float> arr, int stride, int offset, NativeQueue<(int, bool)> queue, NativeArray<bool> seen, 
    //         ReadOnlySpan<Vector2> vertices, ReadOnlySpan<int> triangles, ReadOnlySpan<int> halfEdges)
    //     {
    //         var w = Vertex(f, !fOutgoing, triangles);
    //         var q = vertices[w];
    //         if (!seen[w] && ShouldSpread(originPos, spreadDist, q, out var distToOrigin))
    //         {
    //             var t = math.max(1 - distToOrigin / spreadDist, 0);
    //             BlendIn(w, stride, offset, t * val0,  arr);
    //             queue.Enqueue((f, !fOutgoing));
    //             seen[w] = true;
    //         }
    //     }

    //     static bool ShouldSpread(Vector2 origin, float spreadDist, Vector2 q, out float distToOrigin)
    //     {
    //         distToOrigin = math.distance(origin, q);
    //         return distToOrigin < spreadDist;
    //     }

    //     static bool EqualOrHalfEdges(int edge1, int edge2, ReadOnlySpan<int> halfEdges)
    //     {
    //         return edge1 == edge2 || halfEdges[edge1] == edge2;
    //     }

    //     static int Vertex(int edge, bool outgoing, ReadOnlySpan<int> triangles)
    //     {
    //         return math.select(triangles[NextIndexInTriangle(edge)], triangles[edge], outgoing);
    //     }

    //     //next edge with same endpt
    //     static bool TryGetNextEdgeCW(int edge, bool outgoing, ReadOnlySpan<int> halfEdges, out int nextEdge, out bool nextOutgoing)
    //     {
    //         var eOutgoing = math.select(halfEdges[edge], edge, outgoing);
    //         if (eOutgoing < 0)
    //         {
    //             nextEdge = edge;
    //             nextOutgoing = outgoing;
    //             return false;
    //         }

    //         nextEdge = PrevIndexInTriangle(eOutgoing);
    //         nextOutgoing = false;
    //         return true;
    //     }

    //     //next edge with same endpt
    //     static bool TryGetNextEdgeCCW(int edge, bool outgoing, ReadOnlySpan<int> halfEdges, out int nextEdge, out bool nextOutgoing)
    //     {
    //         var eIncoming = math.select(edge, halfEdges[edge], outgoing);
    //         if (eIncoming < 0)
    //         {
    //             nextEdge = edge;
    //             nextOutgoing = outgoing;
    //             return false;
    //         }

    //         nextEdge = NextIndexInTriangle(eIncoming);
    //         nextOutgoing = true;
    //         return true;
    //     }
    // }

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