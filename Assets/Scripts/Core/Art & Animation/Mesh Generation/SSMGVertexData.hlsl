#ifndef SSMG_VERTEX_DATA//if we use this function twice in one shader, this code will only get included once (like #pragma once)
#define SSMG_VERTEX_DATA

void ScaledPower01(float t, float scale, float power, out float s)
{
    s = clamp(pow(scale * t, power), 0, 1);
}

//process uv1, uv2 data stored in ssmg mesh
void SSMGVertexData_float(float2 uv1, float2 uv2, float2 objectScale, 
    float convexityRadius, float convexityScale, float convexityPower,
    float concavityRadius, float concavityScale, float concavityPower,
    float topsideRadius, float topsidePower, float undersideRadius, float undersidePower,
    float areaMin, float areaMax, float areaPower, float borderWorldWidth,  float borderPower,
    out float convexity, out float concavity, out float topside, out float underside, out float area, out float border)
{
   float scale = max(objectScale.x, objectScale.y);
   float localDistToBorder = uv2.y;
   float worldDistToBorder = scale * localDistToBorder;
   border = clamp(1 - worldDistToBorder / borderWorldWidth, 0, 1);
   border = pow(border, borderPower);

   float x = uv1.x;
   convexity = max(x, 0) * clamp(1 - worldDistToBorder / convexityRadius, 0, 1);
   ScaledPower01(convexity, convexityScale, convexityPower, convexity);
   concavity = max(-x, 0) * clamp(1 - worldDistToBorder / concavityRadius, 0, 1);
   ScaledPower01(concavity, concavityScale, concavityPower, concavity);

   float y = uv1.y;
   topside = max(y, 0) * clamp(1 - worldDistToBorder / topsideRadius, 0, 1);
   topside = pow(topside, topsidePower);
   underside = max(-y, 0) * clamp(1 - worldDistToBorder / undersideRadius, 0, 1);
   underside = pow(underside, undersidePower);

   area = clamp((uv2.x - areaMin) / (areaMax - areaMin), 0, 1);
   area = pow(area, areaPower);
}

void SSMGVertexData_half(float2 uv1, float2 uv2, float2 objectScale, 
    float convexityRadius, float convexityScale, float convexityPower,
    float concavityRadius, float concavityScale, float concavityPower,
    float topsideRadius, float topsidePower, float undersideRadius, float undersidePower,
    float areaMin, float areaMax, float areaPower, float borderWorldWidth,  float borderPower,
    out float convexity, out float concavity, out float topside, out float underside, out float area, out float border)
{
    SSMGVertexData_float(uv1, uv2, objectScale, convexityRadius, convexityScale, convexityPower, 
        concavityRadius, concavityScale, concavityPower, 
        topsideRadius, topsidePower, undersideRadius, undersidePower,
        areaMin, areaMax, areaPower, borderWorldWidth, borderPower,
        convexity, concavity, topside, underside, area, border);
}

#endif