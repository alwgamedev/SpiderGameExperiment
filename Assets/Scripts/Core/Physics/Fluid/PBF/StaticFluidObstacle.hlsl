//convex polygon with at most 8 vertices (oriented ccw)
struct PolygonGeometry
{
    float2 aabbMin;
    float2 aabbMax;
    float4 vertex[8];
    uint vertexCount;
    uint pad1, pad2, pad3;
};

bool OverlapPoint(float2 boxMin, float2 boxMax, float2 p)
{
    return all(p > boxMin && p < boxMax);
}

bool OverlapPoint(PolygonGeometry poly, float2 p, float buffer, out float2 escapeNormal, out float escapeDistance)
{
    escapeNormal = 0;
    escapeDistance = -1;
    //^initializing to large constant (or 100, even) for some reason makes dist < escapeDistance check fail and you will lose your mind
    //trying to figure out what the HHHH is goin' on
    
    if (!OverlapPoint(poly.aabbMin, poly.aabbMax, p))
    {
        return false;
    }
    
    for (uint i = 0; i < poly.vertexCount; i++)
    {
        float2 vertex = poly.vertex[i].xy;
        float2 normal = poly.vertex[i].zw;
        
        float dist = dot(vertex - p, normal) + buffer;
        if (dist < 0)
        {
            return false;
        }

        if (escapeDistance < 0 || abs(dist) < escapeDistance)
        {
            escapeDistance = max(dist, 10E-05);
            escapeNormal = normal;
        }
    }
    
    return true;
}

bool OverlapPoint(StructuredBuffer<PolygonGeometry> obstacle, uint numObstacles, float2 p, float buffer, out int overlapIndex, out float2 escapeNormal, out float escapeDistance)
{
    for (uint i = 0; i < numObstacles; i++)
    {
        if (OverlapPoint(obstacle[i], p, buffer, escapeNormal, escapeDistance))
        {
            overlapIndex = i;
            return true;
        }
    }

    overlapIndex = -1;
    return false;
}