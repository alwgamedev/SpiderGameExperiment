#ifndef SSMG_VERTEX_DATA//if we use this function twice in one shader, this code will only get included once (like #pragma once)
#define SSMG_VERTEX_DATA

void ScaledPower01(float t, float scale, float power, out float s)
{
    s = clamp(pow(scale * t, power), 0, 1);
}

//process uv1, uv2 data stored in ssmg mesh
void SSMGVertexData_float(float4 uv1, float2 uv2, float2 objectScale, 
    float convexityPower, float concavityPower, float topsidePower, float undersidePower,
    float borderWorldWidth,  float borderPower, float crannyPower,
    out float convexity, out float concavity, out float topside, out float underside, out float border, out float cranny)
{
   convexity = pow(uv1.x, convexityPower);
   concavity = pow(uv1.y, concavityPower);
   topside = pow(uv1.z, topsidePower);
   underside = pow(uv1.w, undersidePower);

   float scale = max(objectScale.x, objectScale.y);
   float localDistToBorder = uv2.x;
   float worldDistToBorder = scale * localDistToBorder;
   border = clamp(1 - worldDistToBorder / borderWorldWidth, 0, 1);
   border = pow(border, borderPower);

   cranny = pow(uv2.y, crannyPower);
}

void SSMGVertexData_half(float4 uv1, float2 uv2, float2 objectScale, 
    float convexityPower, float concavityPower, float topsidePower, float undersidePower,
    float borderWorldWidth,  float borderPower, float crannyPower,
    out float convexity, out float concavity, out float topside, out float underside, out float border, out float cranny)
{
    SSMGVertexData_float(uv1, uv2, objectScale, convexityPower, concavityPower, topsidePower, undersidePower, borderWorldWidth, borderPower,
        crannyPower, convexity, concavity, topside, underside, border, cranny);
}

#endif