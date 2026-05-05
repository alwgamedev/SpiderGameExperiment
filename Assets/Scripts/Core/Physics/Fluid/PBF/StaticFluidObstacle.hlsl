const float o41 = 1E-05;
const float bigly = 1E30;

struct AABB
{
    float2 min;
    float2 max;
};

//convex polygon with at most 8 vertices (oriented ccw)
struct PolygonGeometry
{
    AABB aabb;
    int vertexCount;
    int pad;
    float4 vertex[8];//xy = vertex, zw = outward normal of edge (i, i + 1)
};

bool OverlapPoint(AABB box, float2 p)
{
    return all(p < box.max && p > box.min);
}

bool OverlapPoint(PolygonGeometry poly, float2 p, out float2 escapeNormal, out float escapeDistance)
{
    escapeNormal = 0;
    escapeDistance = 0;
    
    if (!OverlapPoint(poly.aabb, p))
    {
        return false;
    }
    
    escapeDistance = bigly;
    int escapeIndex = 0;
    for (int i = 0; i < poly.vertexCount; i++)
    {
        float dist = dot(poly.vertex[i].xy - p, poly.vertex[i].zw);
        if (dist < 0)
        {
            return false;
        }

        if (dist < escapeDistance)
        {
            escapeDistance = dist;
            escapeIndex = i;
        }
    }
    
    escapeNormal = poly.vertex[escapeIndex].zw;
    return true;
}