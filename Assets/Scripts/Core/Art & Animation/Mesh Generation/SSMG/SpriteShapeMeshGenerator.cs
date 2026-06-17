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
    [SerializeField] MeshRenderer meshRenderer;
    [SerializeField] int arcLengthSamples;//samples per spline segment used to calculate arc length
    [SerializeField] float splineSampleRate;//number of vertices per unit arc length
    [SerializeField] float maxTriangleArea;
    [SerializeField] float minAngleDeg;
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
    [SerializeField] float crackSpread;
    [SerializeField] int barySeedSpacing;
    [SerializeField] int bdryDistSmoothingIterations;
    [SerializeField] Vector2[] perimeter;

    Material material;

    public ReadOnlySpan<Vector2> GetPerimeter() => perimeter;

    public void GenerateMesh()
    {
        if (!spriteShapeController)
        {
            Debug.LogWarning($"No Sprite Shape Controller.");
            return;
        }

        if (mesh)
        {
            DestroyImmediate(mesh);
        }

        var seed = (uint)MathTools.RNG.Next(1, int.MaxValue);
        // var rng = new Unity.Mathematics.Random(seed);

        var vertices = new NativeList<Vector2>(Allocator.TempJob);
        SplineSampler.SampleSpline(spriteShapeController.spline, arcLengthSamples, splineSampleRate, vertices);
        var (bbMin, bbMax) = BoundingBox(vertices.AsArray());

        var triangulator = Triangulate(vertices.AsArray(), maxTriangleArea, minAngleDeg);//TempJob allocated
        vertices.Dispose();

        var positions = triangulator.Output.Positions;
        var triangles = triangulator.Output.Triangles;
        var halfEdges = triangulator.Output.Halfedges;

        if (triangles.Length == 0)
        {
            //can happen if your polygon is invalid. this makes sure things get properly disposed.
            triangulator.Dispose();
            Debug.LogError("Triangulation failed.");
            return;
        }

        var boundaryEdges = new NativeList<int>(Allocator.Temp);
        MeshTools.GetBoundaryEdges(boundaryEdges, halfEdges);

        // var vertexGrid = BuildPointGrid(positions.AsArray(), bbMin, bbMax, 1);

        var uv = new NativeArray<Vector2>(positions.Length, Allocator.Temp);//uv
        // var uv1 = new NativeArray<Vector4>(positions.Length, Allocator.Temp);//border geometry
        var uv2 = new NativeArray<Vector4>(positions.Length, Allocator.Temp);//dist to border + crack spread
        // var uv2Float = uv2.Reinterpret<float>(16);
        // var uv3 = new NativeArray<Vector4>(positions.Length, Allocator.Temp);//bary coords
        // var uv4 = new NativeArray<Vector4>(positions.Length, Allocator.Temp);//cracks

        // var seed = (uint)MathTools.RNG.Next(1, int.MaxValue);
        // var rng = new Unity.Mathematics.Random(seed);

        MeshTools.CalculateBoundingBoxUV(uv, positions, bbMin, bbMax);
        // FillBarycentricCoords(uv3, triangles, halfEdges, barySeedSpacing, out var baryMask);
        // FillBorderGeometry(uv1, positions, triangles, boundaryEdges, vertexGrid,
        //     convexitySpread, concavitySpread, topsideSpread, undersideSpread,
        //     convexityMax, concavityMax, topsideMax, undersideMax);
        FillDistToBorder(uv2, positions, triangles, boundaryEdges, bdryDistSmoothingIterations);
        // SmoothDistToBorder(uv2Float, 4, 0, smoothingRadius, positions);
        // FillCracks(uv4, positions, triangles, halfEdges, vertexGrid, baryMask, numCracksMin, numCracksMax, ref rng, out var cracks);
        // FillCrackSpread(uv2Float, 4, 1, cracks, crackSpread, positions, triangles);

        Array.Resize(ref perimeter, boundaryEdges.Length);
        for (int i = 0; i < perimeter.Length; i++)
        {
            perimeter[i] = positions[triangles[boundaryEdges[i]]];
        }

        mesh = new();

        NativeArray<Vector3> positionsV3 = new(positions.Length, Allocator.Temp);
        for (int i = 0; i < positionsV3.Length; i++)
        {
            positionsV3[i] = positions[i];
        }

        mesh.SetVertices(positionsV3);
        mesh.SetIndices(triangles.AsArray(), MeshTopology.Triangles, 0);
        mesh.SetUVs(0, uv);//uv
        // mesh.SetUVs(1, uv1);//bord geometry
        mesh.SetUVs(2, uv2);//dist to border
        // mesh.SetUVs(3, uv3);//bary coords
        // mesh.SetUVs(4, uv4);//crack
        mesh.RecalculateNormals();

        triangulator.Dispose();
    }


    public void ApplyMesh()
    {
        meshFilter.sharedMesh = mesh;
    }

    readonly int offsetProperty = Shader.PropertyToID("_RandomOffset");

    private void Start()
    {
        if (Application.isPlaying)
        {
            material = new Material(meshRenderer.sharedMaterial);
            var offset = new Vector2(MathTools.RandomFloat(-10000, 10000), MathTools.RandomFloat(-10000, 10000));
            material.SetVector(offsetProperty, offset);
            meshRenderer.sharedMaterial = material;
        }
    }

    private void OnDestroy()
    {
        if (mesh)
        {
            if (Application.isPlaying)
            {
                Destroy(material);
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
    private static Triangulator<Vector2> Triangulate(NativeArray<Vector2> vertices, float maxArea, float minAngleDeg)
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
            RefinementThresholds = { Angle = math.radians(minAngleDeg), Area = maxArea },
            RestoreBoundary = true
        };

        var triangulator = new Triangulator<Vector2>(Allocator.TempJob)
        {
            Input = { Positions = vertices, ConstraintEdges = constraintEdges.AsArray() },
            Settings = settings
        };

        triangulator.Run();
        return triangulator;
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
    private static void FillBorderGeometry(NativeArray<Vector4> uv1, ReadOnlySpan<Vector2> vertices, ReadOnlySpan<int> triangles,
        ReadOnlySpan<int> boundaryEdges, PointGrid vertexGrid,
        float convexitySpread, float concavitySpread, float topsideSpread, float undersideSpread,
        float convexityMax, float concavityMax, float topsideMax, float undersideMax)
    {
        var e0 = boundaryEdges[^1];
        var e0Verts = MeshTools.EdgeVertices(e0, triangles);
        var u0 = (vertices[e0Verts.Item1] - vertices[e0Verts.Item2]).normalized;

        var uv1Float = uv1.Reinterpret<float>(16);

        for (int i = 0; i < boundaryEdges.Length; i++)
        {
            var e1 = boundaryEdges[i];
            var e1Verts = MeshTools.EdgeVertices(e1, triangles);
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

    //x-coord = dist to boundary
    //y-coord = largest distance to boundary "nearby"
    //zw = unit vector pointing towards closest boundary pt
    [BurstCompile]
    private static void FillDistToBorder(NativeArray<Vector4> arr, ReadOnlySpan<Vector2> positions,
        ReadOnlySpan<int> triangles, ReadOnlySpan<int> boundaryEdges, int smoothingIterations)
    {
        NativeArray<float> boundaryWidth = new(boundaryEdges.Length, Allocator.Temp);
        for (int i = 0; i < boundaryEdges.Length; i++)
        {
            var v0 = triangles[boundaryEdges[i]];
            var v1 = triangles[boundaryEdges[(i + 1) % boundaryEdges.Length]];
            var p0 = positions[v0];
            var p1 = positions[v1];
            var m = (p1 - p0).normalized.CCWPerp();//outward normal

            //try to find a point on the "opposite" side of the boundary to get an idea of how wide the region is
            //this method is good enough, especially after a bit of smoothing out
            var minDist = float.MaxValue;
            var minJ = i;
            for (int j = 0; j < boundaryEdges.Length; j++)
            {
                var w0 = triangles[boundaryEdges[j]];
                var q0 = positions[w0];
                var d = q0 - p0;
                var l = math.length(d);
                if (math.dot(d, m) < -0.5f * l)
                {
                    minJ = math.select(minJ, j, l < minDist);
                    minDist = math.min(minDist, l);
                }
            }

            boundaryWidth[i] = minDist;
        }

        for (int iter = 0; iter < smoothingIterations; iter++)
        {
            var iPrev = boundaryWidth.Length - 1;
            for (int i = 0; i < boundaryWidth.Length; i++)
            {
                var iNext = (i + 1) % boundaryWidth.Length;
                boundaryWidth[i] = 0.25f * boundaryWidth[i] + 0.375f * (boundaryWidth[iPrev] + boundaryWidth[iNext]);
                iPrev = i;
            }
        }

        for (int i = 0; i < positions.Length; i++)
        {
            var p = positions[i];
            var minDist2 = Mathf.Infinity;
            var closestBdryPt = Vector2.zero;
            int minJ = 0;

            for (int j = 0; j < boundaryEdges.Length; j++)
            {
                var edge = MeshTools.EdgeVertices(boundaryEdges[j], triangles);
                var p0 = positions[edge.Item1];
                var p1 = positions[edge.Item2];

                var w = p1 - p0;
                var t = math.dot(p - p0, w) / math.lengthsq(w);
                t = math.select(0, t, t > 0 && !(t > 1));//if t not in (0, 1] just check left endpt of edge

                var q = p0 + t * w;
                var thisDist2 = math.distancesq(p, q);
                closestBdryPt = math.select(closestBdryPt, q, thisDist2 < minDist2);
                minJ = math.select(minJ, j, thisDist2 < minDist2);
                minDist2 = math.min(thisDist2, minDist2);
            }

            var minDist = math.sqrt(minDist2);
            var u = (closestBdryPt - p).normalized;
            arr[i] = new(minDist, boundaryWidth[minJ], u.x, u.y);
        }

        for (int i = 0; i < boundaryEdges.Length; i++)
        {
            var v0 = triangles[boundaryEdges[i]];
            var v1 = triangles[boundaryEdges[(i + 1) % boundaryEdges.Length]];
            var p0 = positions[v0];
            var p1 = positions[v1];
            var u = (p1 - p0).normalized.CCWPerp();
            var a = arr[v0];
            a.z = u.x;
            a.w = u.y;
            arr[v0] = a;
        }
    }

    //uv2.y = cracks
    [BurstCompile]
    private static void FillCracks(NativeArray<Vector4> arr, ReadOnlySpan<Vector2> vertices,
        ReadOnlySpan<int> triangles, ReadOnlySpan<int> halfEdges, PointGrid vertexGrid, ReadOnlySpan<int> baryMask,
        float numCracksMin, float numCracksMax, ref Unity.Mathematics.Random rng, out NativeList<Crack> cracks,
        Transform transform = null, float drawTime = 0)
    {
        NativeArray<bool> seen = new(arr.Length, Allocator.Temp);
        NativeList<int> possibleChildren = new(Allocator.Temp);
        NativeArray<float> arrFloat = arr.Reinterpret<float>(16);
        cracks = new(Allocator.Temp);

        for (int i = 0; i < seen.Length; i++)
        {
            seen[i] = baryMask[i] == 0;
        }

        var bbArea = vertexGrid.cellSize * vertexGrid.cellSize * vertexGrid.NumCells;
        var numCracks = (int)(MathTools.RandomFloat(numCracksMin, numCracksMax) * bbArea);
        for (int i = 0; i < numCracks; i++)
        {
            var crack = GenerateCrack(seen, possibleChildren, vertices, triangles, halfEdges, vertexGrid, ref rng);
            if (!crack.nodes.IsCreated)
            {
                continue;
            }

            cracks.Add(crack);
            for (int j = 0; j < crack.nodes.Length; j++)
            {
                var node = crack.nodes[j];
                var vert = MeshTools.Vertex(node.edge, node.outgoing != 0, triangles);
                var baryColor = baryMask[vert];
                var offset = baryColor switch
                {
                    1 => 0,
                    2 => 1,
                    4 => 2,
                    8 => 3,
                    _ => -1
                };
                if (!(offset < 0))
                {
                    // BlendIn(vert, 4, offset, rng.NextFloat(crackMinValue, crackMaxValue), arrFloat);
                    arrFloat[4 * vert + offset] = 1;
                }
            }

            if (transform)
            {
                crack.DebugDraw(vertices, triangles, transform, drawTime);
            }
        }
    }

    [BurstCompile]
    private static void FillCrackSpread(NativeArray<float> arr, int stride, int offset,
        ReadOnlySpan<Crack> cracks, float radius, ReadOnlySpan<Vector2> positions, ReadOnlySpan<int> triangles)
    {
        var r2 = radius * radius;
        for (int i = 0; i < positions.Length; i++)
        {
            var p = positions[i];
            var dist2 = cracks[0].DistanceSqrd(p, positions, triangles);
            for (int j = 1; j < cracks.Length; j++)
            {
                var crack = cracks[j];
                dist2 = math.min(dist2, crack.DistanceSqrd(p, positions, triangles));
            }
            if (dist2 < r2)
            {
                var t = math.clamp(1 - math.sqrt(dist2) / radius, 0, 1);
                arr[stride * i + offset] = t;
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

        public readonly (int colMin, int colMax, int rowMin, int rowMax) GetIterBounds(Vector2 minPt, Vector2 maxPt)
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
    private static PointGrid BuildPointGrid(ReadOnlySpan<Vector2> positions, Vector2 bbMin, Vector2 bbMax, float cellSize)
    {
        var bbSpan = bbMax - bbMin;
        var gridWidth = (int)Mathf.Ceil(bbSpan.x / cellSize);
        var gridHeight = (int)Mathf.Ceil(bbSpan.y / cellSize);
        var numCells = gridWidth * gridHeight;

        //will store triangle edges, ordered in grid by position of their start pt
        var grid = new NativeArray<int>(positions.Length, Allocator.TempJob);
        var cellStart = new NativeArray<int>(numCells + 1, Allocator.TempJob);

        NativeArray<int> cellCount = new(numCells, Allocator.Temp);

        //first count the number of points in each cell
        for (int i = 0; i < positions.Length; i++)
        {
            var p = positions[i] - bbMin;
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
        for (int i = 0; i < positions.Length; i++)
        {
            var p = positions[i] - bbMin;
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
        public int outgoing;//0 for false
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

        public float DistanceSqrd(Vector2 p, ReadOnlySpan<Vector2> vertices, ReadOnlySpan<int> triangles)
        {
            var node0 = nodes[0];
            var p0 = vertices[MeshTools.Vertex(node0.edge, node0.outgoing != 0, triangles)];
            float minDist2 = math.distancesq(p, p0);

            for (int i = 1; i < nodes.Length; i++)
            {
                var node = nodes[i];
                var p2 = vertices[MeshTools.Vertex(node.edge, node.outgoing != 0, triangles)];
                var p1 = vertices[MeshTools.Vertex(node.edge, node.outgoing == 0, triangles)];
                minDist2 = math.min(minDist2, math.distancesq(p, p2));

                var edge = p2 - p1;
                var v = p - p1;
                var edgeLength2Inv = 1 / math.lengthsq(edge);
                var t = math.dot(v, edge) * edgeLength2Inv;
                var y = math.dot(v, edge.CCWPerp());
                var dist2 = y * y * edgeLength2Inv;

                minDist2 = math.select(minDist2, dist2, t > 0 && t < 1 && dist2 < minDist2);
            }

            return minDist2;
        }

        public void DebugDraw(ReadOnlySpan<Vector2> vertices, ReadOnlySpan<int> triangles, Transform transform, float drawTime)
        {
            for (int i = 1; i < nodes.Length; i++)
            {
                var m = nodes[i];
                var v = MeshTools.Vertex(m.edge, m.outgoing != 0, triangles);
                var w = MeshTools.Vertex(m.edge, m.outgoing == 0, triangles);
                var p = transform.TransformPoint(vertices[v]);
                var q = transform.TransformPoint(vertices[w]);
                Debug.DrawLine(p, q, Color.green, drawTime);
            }
        }
    }

    static Crack GenerateCrack(NativeArray<bool> seen, NativeList<int> possibleChildren,
        ReadOnlySpan<Vector2> vertices, ReadOnlySpan<int> triangles, ReadOnlySpan<int> halfEdges, PointGrid vertexGrid,
        /*int depthMax, float continueChance, float branchChance,*/ ref Unity.Mathematics.Random rng)
    {
        static bool Valid(int edge, bool outgoing, ReadOnlySpan<bool> seen, ReadOnlySpan<int> triangles, ReadOnlySpan<int> halfEdges)
        {
            var v = MeshTools.Vertex(edge, outgoing, triangles);
            return !seen[v] && !HasSeenNeighbor(edge, outgoing, seen, triangles, halfEdges);
        }

        //vertex cannot have any seen neighbors (except possibly the edge passed in)
        //or else shader will think there's an edge between them
        static bool HasSeenNeighbor(int edge, bool outgoing, ReadOnlySpan<bool> seen,
            ReadOnlySpan<int> triangles, ReadOnlySpan<int> halfEdges)
        {
            var f = edge;
            var fOutgoing = outgoing;

            while (MeshTools.TryGetNextEdgeCCW(f, fOutgoing, halfEdges, out f, out fOutgoing) 
                && !MeshTools.EqualOrHalfEdges(f, edge, halfEdges))
            {
                if (seen[MeshTools.Vertex(f, !fOutgoing, triangles)])
                {
                    return true;
                }
            }

            //if we hit a boundary edge while scanning CCW, go back to start and scan CW to make sure we get all neighbors
            if (!MeshTools.EqualOrHalfEdges(f, edge, halfEdges) || (outgoing && halfEdges[edge] < 0))
            {
                f = edge;
                fOutgoing = outgoing;
                while (MeshTools.TryGetNextEdgeCW(f, fOutgoing, halfEdges, out f, out fOutgoing) 
                    && !MeshTools.EqualOrHalfEdges(f, edge, halfEdges))
                {
                    if (seen[MeshTools.Vertex(f, !fOutgoing, triangles)])
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        //randomize by cell to get cracks more evenly distributed across mesh.
        //(there are a lot more vertices near the boundary, so if we just choose a random vertex
        //we end up with most of the cracks near the boundary)
        int attempts = 0;
        int edge = -1;
        bool outgoing = false;
        while (edge < 0 && attempts < 10)
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

            //find an edge containing that vertex, and make sure there is at least one valid neighboring vertex
            for (int i = 0; i < triangles.Length; i++)
            {
                var edgeVerts = MeshTools.EdgeVertices(i, triangles);
                if (edgeVerts.Item1 == vert || edgeVerts.Item2 == vert)
                {
                    var og = edgeVerts.Item1 == vert;
                    if (!Valid(i, og, seen, triangles, halfEdges))
                    {
                        break;
                    }
                    //if opposite vert is valid, then we've confirmed there's at least one valid neighbor and we can proceed
                    if (Valid(i, !og, seen, triangles, halfEdges))
                    {
                        edge = i;
                        outgoing = og;
                        break;
                    }
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

        //add first node and give it 1-4 children
        int numChildren0 = int.MaxValue;//rng.NextInt(1, 4);
        QueueCrackNode(edge, outgoing, nodes, seen, vertices, triangles, ref bbMin, ref bbMax);
        AddNodeChildren(0, numChildren0, nodes, seen, possibleChildren, vertices, triangles, halfEdges,
            ref bbMin, ref bbMax, ref rng);

        int j = 1;
        while (j < nodes.Length)
        {
            //give node 0-2 children (if depth == depthMax, no children)
            // var depth = nodes[j].depth;
            // var childRoll = rng.NextFloat();
            var numChildren = int.MaxValue;//math.select(0, math.select(1, 2, childRoll < branchChance), depth < depthMax && childRoll < continueChance);
            AddNodeChildren(j, numChildren, nodes, seen, possibleChildren, vertices, triangles, halfEdges,
                ref bbMin, ref bbMax, ref rng);
            j++;
        }

        return new Crack() { nodes = nodes.AsArray(), bbMin = bbMin, bbMax = bbMax };

        //when node is first queued, it'll store (edge, outgoing, depth)
        //correct data will be entered when the node is processed by AddNodeChildren
        static void QueueCrackNode(int edge, bool outgoing, NativeList<CrackNode> nodes, NativeArray<bool> seen,
            ReadOnlySpan<Vector2> vertices, ReadOnlySpan<int> triangles, ref Vector2 bbMin, ref Vector2 bbMax)
        {
            nodes.Add(new() { edge = edge, outgoing = (ushort)math.select(0, 1, outgoing) });
            var v = MeshTools.Vertex(edge, outgoing, triangles);
            var p = vertices[v];
            bbMin = math.min(p, bbMin);
            bbMax = math.max(p, bbMax);
            seen[v] = true;
        }

        static void AddNodeChildren(int i, int numChildren, NativeList<CrackNode> nodes, NativeArray<bool> seen, NativeList<int> possibleChildren,
            ReadOnlySpan<Vector2> vertices, ReadOnlySpan<int> triangles, ReadOnlySpan<int> halfEdges,
            ref Vector2 bbMin, ref Vector2 bbMax, ref Unity.Mathematics.Random rng)
        {
            var node = nodes[i];
            var edge = node.edge;
            var outgoing = node.outgoing != 0;
            var vert = MeshTools.Vertex(edge, outgoing, triangles);

            if (numChildren != 0)
            {
                possibleChildren.Clear();

                var f = edge;
                var fOutgoing = outgoing;

                //usually f points back to the parent node, but for i = 0 we can include it
                if (i == 0 && Valid(f, !fOutgoing, seen, triangles, halfEdges))
                {
                    possibleChildren.Add(f);
                }

                //first find all valid children (vertex not seen, has valid bary color, and edge direction has dot > 0 with previous edge)
                while (MeshTools.TryGetNextEdgeCCW(f, fOutgoing, halfEdges, out f, out fOutgoing)  
                    && !MeshTools.EqualOrHalfEdges(f, edge, halfEdges))
                {
                    possibleChildren.Add(f);
                }

                //if we hit a boundary edge while scanning CCW, go back to start and scan CW to make sure we get all neighbors
                if (!MeshTools.EqualOrHalfEdges(f, edge, halfEdges) || (outgoing && halfEdges[edge] < 0))
                {
                    f = edge;
                    fOutgoing = outgoing;
                    while (MeshTools.TryGetNextEdgeCW(f, fOutgoing, halfEdges, out f, out fOutgoing) 
                        && !MeshTools.EqualOrHalfEdges(f, edge, halfEdges))
                    {
                        possibleChildren.Add(f);
                    }
                }

                while (numChildren > 0 && possibleChildren.Length > 0)
                {
                    var j = rng.NextInt(possibleChildren.Length);
                    var child = possibleChildren[j];
                    var childOutgoing = triangles[child] != vert;
                    if (Valid(child, childOutgoing, seen, triangles, halfEdges))
                    {
                        QueueCrackNode(child, childOutgoing, nodes, seen, vertices, triangles, ref bbMin, ref bbMax);
                        numChildren--;
                    }

                    possibleChildren.RemoveAt(j);
                }
            }
        }
    }
}