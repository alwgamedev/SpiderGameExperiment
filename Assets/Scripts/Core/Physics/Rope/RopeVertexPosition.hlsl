#ifndef ROPE_VERTEX_POS//if we use this file twice in one shader, this code will only get included once (like #pragma once)
#define ROPE_VERTEX_POS

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

int _NumNodes;
int _EndcapTriangles;
float _HalfWidth;
float _Orientation;

StructuredBuffer<float4> _NodePosition;

//output vertex object position, to be used in shader graph
// void RopeVertexPosition_float(float vertID, out float3 objectPos)
// {
//     int vID = (int) vertID;//shader graph function doesn't have option for int parameters...
//     if (vID < 2 * _NumNodes)
//     {
//         int nodeIndex = vID / 2;
//         int displacement = vID % 2 == 0 ? -_Orientation : _Orientation;
//         float4 nodeData = _NodePosition[nodeIndex];
//         float2 segmentDirection = nodeIndex < _NumNodes - 1 ?
//                         _NodePosition[nodeIndex + 1].xy - nodeData.xy : nodeData.xy - _NodePosition[nodeIndex - 1].xy;
//         if (segmentDirection.x == 0 && segmentDirection.y == 0)
//         {
//             int k = nodeIndex + 1;
//             while (++k < _NumNodes && segmentDirection.x == 0 && segmentDirection.y == 0)
//             {
//                 segmentDirection = normalize(_NodePosition[k].xy - nodeData.xy);
//             }
//         }
//         segmentDirection = normalize(segmentDirection);
//         float a = displacement * _HalfWidth * nodeData.z;
//         objectPos = TransformWorldToObject(float3(nodeData.x - a * segmentDirection.y, nodeData.y + a * segmentDirection.x, 0));
//     }
//     else//we're on an endcap vertex
//     {
//         half2 center = _NodePosition[(uint) (_NumNodes - 1)].xy;
//         half2 right = normalize(center - _NodePosition[(uint) (_NumNodes - 2)].xy);
//         half2 down = _Orientation > 0 ? half2(right.y, -right.x) : half2(-right.y, right.x);
//         if (right.x == 0 && right.y == 0)
//         {
//             int k = _NumNodes - 2;
//             while (!(--k < 0) && right.x == 0 && right.y == 0)
//             {
//                 right = normalize(center - _NodePosition[k].xy);
//             }
//         }
//         int i = vID - (2 * _NumNodes) + 1; //which endcap triangle are we on? (not zero indexed)
//         half t = (3.14 * i) / (_EndcapTriangles + 1);
//         half2 p = center + _HalfWidth * (cos(t) * down + sin(t) * right);
//         objectPos = TransformWorldToObject(float3(p, 0));
//     }
// }

void RopeBodyPosition_float(float vertID, out float3 objectPos)
{
    int vID = (int) vertID;//shader graph function doesn't have option for int parameters...
    int nodeIndex = vID / 2;
    int displacement = vID % 2 == 0 ? -_Orientation : _Orientation;
    float4 nodeData = _NodePosition[nodeIndex];
    float2 segmentDirection = nodeIndex < _NumNodes - 1 ?
                    _NodePosition[nodeIndex + 1].xy - nodeData.xy : nodeData.xy - _NodePosition[nodeIndex - 1].xy;
    if (segmentDirection.x == 0 && segmentDirection.y == 0)
    {
        int k = nodeIndex + 1;
        while (++k < _NumNodes && segmentDirection.x == 0 && segmentDirection.y == 0)
        {
            segmentDirection = normalize(_NodePosition[k].xy - nodeData.xy);
        }
    }
    segmentDirection = normalize(segmentDirection);
    float a = displacement * _HalfWidth * nodeData.z;
    objectPos = TransformWorldToObject(float3(nodeData.x - a * segmentDirection.y, nodeData.y + a * segmentDirection.x, 0));
}

void RopeBodyPosition_half(half vertID, out half3 objectPos)
{
    RopeBodyPosition_float(vertID, objectPos);
}

void RopeEndcapPosition_float(float vertID, out float3 objectPos)
{
    int vID = (int) vertID;//shader graph function doesn't have option for int parameters...
    half2 center = _NodePosition[(uint) (_NumNodes - 1)].xy;
    half2 right = normalize(center - _NodePosition[(uint) (_NumNodes - 2)].xy);
    half2 down = _Orientation > 0 ? half2(right.y, -right.x) : half2(-right.y, right.x);
    if (right.x == 0 && right.y == 0)
    {
        int k = _NumNodes - 2;
        while (!(--k < 0) && right.x == 0 && right.y == 0)
        {
            right = normalize(center - _NodePosition[k].xy);
        }
    }
    int i = vID - (2 * _NumNodes) + 1; //which endcap triangle are we on? (not zero indexed)
    half t = (3.14 * i) / (_EndcapTriangles + 1);
    half2 p = center + _HalfWidth * (cos(t) * down + sin(t) * right);
    objectPos = TransformWorldToObject(float3(p, 0));
}

void RopeEndcapPosition_half(half vertID, out half3 objectPos)
{
    RopeEndcapPosition_float(vertID, objectPos);
}

#endif