#ifndef SSMG_VERTEX_DATA//if we use this function twice in one shader, this code will only get included once (like #pragma once)
#define SSMG_VERTEX_DATA

//process uv1, uv2 data stored in ssmg mesh
void SSMGVertexData_float(float2 uv1, float2 uv2, float2 objectScale, float convexityRadius, float visibilityRadius, float areaMin, float areaMax, float borderWorldWidth,
    out float convexity, out float visibility, out float area, out float border)
{
   float scale = max(objectScale.x, objectScale.y);
   float localDistToBorder = uv2.y;
   float worldDistToBorder = scale * localDistToBorder;
   border = clamp(1 - worldDistToBorder / borderWorldWidth, 0, 1);

   convexity = uv1.x;
//    convexityRadius *= abs(convexity);
   convexity *= clamp(1 - worldDistToBorder / convexityRadius, 0, 1);

   visibility = uv1.y;
//    visibilityRadius *= abs(visibility);
   visibility *= clamp(1 - worldDistToBorder / visibilityRadius, 0, 1);

   area = clamp((uv2.x - areaMin) / (areaMax - areaMin), 0, 1);
}

void SSMGVertexData_half(float2 uv1, float2 uv2, float2 objectScale, float convexityRadius, float visibilityRadius, float areaMin, float areaMax, float borderWorldWidth,
    out float convexity, out float visibility, out float area, out float border)
{
    SSMGVertexData_float(uv1, uv2, convexityRadius, visibilityRadius, areaMin, areaMax, borderWorldWidth, objectScale, convexity, visibility, area, border);
}

#endif