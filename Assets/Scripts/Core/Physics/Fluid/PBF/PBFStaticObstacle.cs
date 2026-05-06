using System.Runtime.InteropServices;
using Unity.U2D.Physics;
using UnityEngine;

//convex polygon with <= 8 vertices, oriented ccw
[StructLayout(LayoutKind.Sequential)]
public unsafe struct PBFStaticPolygonObstacle
{
    Vector2 aabbMin;
    Vector2 aabbMax;
    fixed float vertex[32];//viewed as 8 x Vector4, where xy = vertex position, zw = outward normal of edge (i, i + 1)
    int vertexCount;
    int pad1, pad2, pad3;

    /// <summary> Input geometry is assumed to be in world space. </summary>
    public PBFStaticPolygonObstacle(PolygonGeometry polygonGeometry)
    {
        this = default;
        var aabb = polygonGeometry.CalculateAABB(PhysicsTransform.identity);
        aabbMin = aabb.lowerBound;
        aabbMax = aabb.upperBound;
        vertexCount = polygonGeometry.count;
        pad1 = 0;
        pad2 = 0;
        pad3 = 0;

        fixed (float* vertexPtr = vertex)
        {
            for (int i = 0; i < vertexCount; i++)
            {
                var v0 = polygonGeometry.vertices[i];
                var v1 = polygonGeometry.vertices[(i + 1) % vertexCount];
                var n = (v1 - v0).normalized.CWPerp();

                var j = 4 * i;
                vertexPtr[j++] = v0.x;
                vertexPtr[j++] = v0.y;
                vertexPtr[j++] = n.x;
                vertexPtr[j++] = n.y;

                //switch (i)
                //{
                //    case 0:
                //        vertex0 = v0;
                //        normal0 = n;
                //        break;
                //    case 1:
                //        vertex1 = v0;
                //        normal1 = n;
                //        break;
                //    case 2:
                //        vertex2 = v0;
                //        normal2 = n;
                //        break;
                //    case 3:
                //        vertex3 = v0;
                //        normal3 = n;
                //        break;
                //    case 4:
                //        vertex4 = v0;
                //        normal4 = n;
                //        break;
                //    case 5:
                //        vertex5 = v0;
                //        normal5 = n;
                //        break;
                //    case 6:
                //        vertex6 = v0;
                //        normal6 = n;
                //        break;
                //    case 7:
                //        vertex7 = v0;
                //        normal7 = n;
                //        break;
                //}
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

                var color = (i % 4) switch
                {
                    0 => Color.red,
                    1 => Color.green,
                    2 => Color.blue,
                    _ => Color.purple
                };

                Debug.DrawLine(v0, v1, color, 30);
                var midpt = 0.5f * (v0 + v1);
                Debug.DrawLine(midpt, midpt + n, color, 30);
            }
        }
    }
}