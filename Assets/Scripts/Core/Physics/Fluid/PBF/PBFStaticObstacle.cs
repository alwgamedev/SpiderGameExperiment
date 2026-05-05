using UnityEngine;
using System.Runtime.InteropServices;
using Unity.U2D.Physics;

//convex polygon with <= 8 vertices (oriented ccw)
[StructLayout(LayoutKind.Sequential)]
public unsafe struct PBFStaticPolygonObstacle
{
    Vector2 aabbMin;
    Vector2 aabbMax;
    int vertexCount;
    int pad;
    fixed float vertex[32];//viewed as 8 x Vector4, where xy = vertex position, zw = outward normal of edge (i, i + 1)

    /// <summary> Input geometry is assumed to be in world space. </summary>
    public PBFStaticPolygonObstacle(PolygonGeometry polygonGeometry)
    {
        var aabb = polygonGeometry.CalculateAABB(PhysicsTransform.identity);
        aabbMin = aabb.lowerBound;
        aabbMax = aabb.upperBound;
        vertexCount = polygonGeometry.count;
        pad = 0;

        fixed (float* vertexPtr = vertex)
        {
            for (int i = 0; i < vertexCount; i++)
            {
                var v0 = polygonGeometry.vertices[i];
                var v1 = polygonGeometry.vertices[(i + 1) % vertexCount];
                var n = (v1 - v0).normalized.CWPerp();
                int j = 4 * i;
                vertexPtr[j++] = v0.x;
                vertexPtr[j++] = v0.y;
                vertexPtr[j++] = n.x;
                vertexPtr[j++] = n.y;
            }
        }
    }

    public void DebugLog(Vector2 pos)
    {
        var min = aabbMin + pos;
        var max = aabbMax + pos;  
        Debug.Log($"aabbMin: {min}, aabbMax: {max}");
        Debug.DrawLine(min, new Vector2(max.x, min.y), Color.green, 30);
        Debug.DrawLine(new Vector2(max.x, min.y), max, Color.green, 30);
        Debug.DrawLine(max, new Vector2(min.x, max.y), Color.green, 30);
        Debug.DrawLine(new Vector2(min.x, max.y), min, Color.green, 30);

        Debug.Log($"vertexCount: {vertexCount}");
        fixed (float* vertexPtr = vertex)
        {
            for (int i = 0; i < vertexCount; i++)
            {
                var j = 4 * i;
                var j1 = 4 * ((i + 1) % vertexCount);
                var v0 = new Vector2(vertex[j], vertex[j + 1]) + pos;
                var v1 = new Vector2(vertex[j1], vertex[j1 + 1]) + pos;
                var n = new Vector2(vertex[j + 2], vertex[j + 3]);

                Debug.DrawLine(v0, v1, Color.red, 30);
                Debug.DrawLine(v0, v0 + n, Color.yellow, 30);
            }
        }
    }
}