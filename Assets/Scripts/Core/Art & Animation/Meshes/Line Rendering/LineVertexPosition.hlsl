#ifndef LINE_VERTEX_POS//if we use this file twice in one shader, this code will only get included once (like #pragma once)
#define LINE_VERTEX_POS

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

int _NumNodes;
int _EndcapTriangles;
float _HalfWidth;
float _Orientation;

StructuredBuffer<float4> _NodePosition;

void LineBodyPosition_float(float vertID, float2 uv, out float3 objectPos)
{
    float4 nodeData = _NodePosition[((int)vertID) / 2];
    float displacementDir = (2 * uv.y - 1) * _Orientation;//when ori < 0, need to flip the rope upside down
    float2 p = nodeData.xy + displacementDir * _HalfWidth * nodeData.zw;
    objectPos = TransformWorldToObject(float3(p, 0));
}

void LineBodyPosition_half(half vertID, half2 uv, out half3 objectPos)
{
    LineBodyPosition_float(vertID, uv, objectPos);
}

void LineEndcapPosition_float(float2 endCapPos, out float3 objectPos)
{
    float4 lastNode = _NodePosition[_NumNodes - 1];
    float2 center = lastNode.xy;
    float2 up = normalize(lastNode.zw);
    float2 right = float2(up.y, -up.x);
    float2 p = center + _HalfWidth * (endCapPos.x * right + _Orientation * endCapPos.y * up);
    objectPos = TransformWorldToObject(float3(p, 0));
}

void LineEndcapPosition_half(half2 endCapPos, out half3 objectPos)
{
    LineEndcapPosition_float(endCapPos, objectPos);
}

#endif