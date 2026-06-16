using Unity.Collections;
using Unity.Burst;
using System;
using UnityEngine;
using Unity.Mathematics;

public static class MeshTools
{
    public static int PrevIndexInTriangle(int j)
    {
        var k = j % 3;
        return k switch
        {
            0 => j + 2,
            _ => j - 1
        };
    }

    public static int NextIndexInTriangle(int j)
    {
        var k = j % 3;
        return k switch
        {
            2 => j - 2,
            _ => j + 1
        };
    }

    public static (int, int) EdgeVertices(int edge, ReadOnlySpan<int> triangles)
    {
        var v0 = triangles[edge];
        var v1 = triangles[NextIndexInTriangle(edge)];
        return (v0, v1);
    }

    [BurstCompile]
    public static int NextBoundaryEdge(int bdryEdge, ReadOnlySpan<int> halfEdges)
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
    public static void GetBoundaryEdges(NativeList<int> edges, ReadOnlySpan<int> halfEdges)
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
    public static void CalculateBoundingBoxUV(NativeArray<Vector2> uv, ReadOnlySpan<Vector2> vertices, Vector2 bbMin, Vector2 bbMax)
    {
        var bbSpan = bbMax - bbMin;
        for (int i = 0; i < vertices.Length; i++)
        {
            var p = vertices[i];
            uv[i] = new((p.x - bbMin.x) / bbSpan.x, (p.y - bbMin.y) / bbSpan.y);
        }
    }

    public static bool EqualOrHalfEdges(int edge1, int edge2, ReadOnlySpan<int> halfEdges)
    {
        return edge1 == edge2 || halfEdges[edge1] == edge2;
    }

    public static int Vertex(int edge, bool outgoing, ReadOnlySpan<int> triangles)
    {
        return math.select(triangles[NextIndexInTriangle(edge)], triangles[edge], outgoing);
    }

    //next edge with same endpt
    public static bool TryGetNextEdgeCW(int edge, bool outgoing, ReadOnlySpan<int> halfEdges, out int nextEdge, out bool nextOutgoing)
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
    public static bool TryGetNextEdgeCCW(int edge, bool outgoing, ReadOnlySpan<int> halfEdges, out int nextEdge, out bool nextOutgoing)
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

    public static int BaryCoord(int mask, int coordIdx)
    {
        return (mask >> coordIdx) & 1;
    }

    //give the mesh a coloring by vectors -- we use a "greedy" algorithm with 4 colors, so
    //some vertices will end up uncolored (zero vector), but we're able to salvage this in the shader as long 
    //as the triangle has 2 vertices with valid colors. 
    //for the ruppert triangulations i usually get usable coordinates for ~99.5%+ of the triangles.
    [BurstCompile]
    public static void BakeBaryCoords(NativeArray<Vector4> bary, ReadOnlySpan<int> triangles, ReadOnlySpan<int> halfEdges,
        out NativeArray<int> baryMask, int seedSpacing = 2)
    {
        baryMask = new(bary.Length, Allocator.Temp);

        for (int i = 0; i < seedSpacing; i++)
        {
            for (int j = i; j < triangles.Length; j += seedSpacing)
            {
                var v = triangles[j];
                if (baryMask[v] == 0)
                {
                    var (nbrMask, _) = ScanNeighbors(j, true, baryMask, triangles, halfEdges);
                    baryMask[v] = FirstAvailableColor(nbrMask, 4);
                }
            }
        }

        for (int i = 0; i < baryMask.Length; i++)
        {
            var mask = baryMask[i];
            bary[i] = new(BaryCoord(mask, 0), BaryCoord(mask, 1), BaryCoord(mask, 2), BaryCoord(mask, 3));
        }

        //if you want to test results:
        // var badTriangleCount = 0;
        // for (int i = 0; i < triangles.Length; i += 3)
        // {
        //     var v0 = triangles[i];
        //     var v1 = triangles[i + 1];
        //     var v2 = triangles[i + 2];
        //     var badVertCount = math.select(0, 1, baryMask[v0] == 0) + math.select(0, 1, baryMask[v1] == 0)
        //         + math.select(0, 1, baryMask[v2] == 0);
        //     if (badVertCount > 1)
        //     {
        //         badTriangleCount++;
        //     }
        // }

        // Debug.Log($"{badTriangleCount} / {triangles.Length} = {(float)badTriangleCount / triangles.Length}%"
        //     + " triangles with bad bary coords");

        static (int nbrMask, int nbrCount) ScanNeighbors(int edge, bool outgoing, NativeArray<int> baryMask,
            ReadOnlySpan<int> triangles, ReadOnlySpan<int> halfEdges)
        {
            var neighborMask = 0;
            var neighborCount = 0;
            var f = edge;
            var fOutgoing = outgoing;

            ScanNeighbor(f, fOutgoing, baryMask, triangles, ref neighborMask);

            while (TryGetNextEdgeCCW(f, fOutgoing, halfEdges, out f, out fOutgoing) && !EqualOrHalfEdges(f, edge, halfEdges))
            {
                neighborCount++;
                ScanNeighbor(f, fOutgoing, baryMask, triangles, ref neighborMask);
            }

            //if we hit a boundary edge while scanning CCW, go back to start and scan CW to make sure we get all neighbors
            if (!EqualOrHalfEdges(f, edge, halfEdges) || (outgoing && halfEdges[edge] < 0))
            {
                f = edge;
                fOutgoing = outgoing;
                while (TryGetNextEdgeCW(f, fOutgoing, halfEdges, out f, out fOutgoing) && !EqualOrHalfEdges(f, edge, halfEdges))
                {
                    neighborCount++;
                    ScanNeighbor(f, fOutgoing, baryMask, triangles, ref neighborMask);
                }
            }

            return (neighborMask, neighborCount);

            static void ScanNeighbor(int f, bool fOutgoing, ReadOnlySpan<int> baryMask,
                ReadOnlySpan<int> triangles, ref int neighborMask)
            {
                var w = Vertex(f, !fOutgoing, triangles);
                neighborMask |= baryMask[w];
            }
        }

        static int FirstAvailableColor(int taken, int numColors)
        {
            int result = 1;
            while ((taken & result) != 0)
            {
                result <<= 1;
            }

            return result & ((1 << numColors) - 1);
        }
    }
}