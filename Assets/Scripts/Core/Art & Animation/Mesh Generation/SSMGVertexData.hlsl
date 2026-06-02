#ifndef SSMG_VERTEX_DATA//if we use this function twice in one shader, this code will only get included once (like #pragma once)
#define SSMG_VERTEX_DATA

void ScaledPower01(float t, float scale, float power, out float s)
{
    s = clamp(pow(scale * t, power), 0, 1);
}

// void FloatToHalf2(float x, out half x0, out half x1)
// {
//     uint bits = asuint(x);
//     uint bits0 = bits & 0xFFFF;
//     uint bits1 = bits >> 16;
//     x0 = f16tof32(bits0);
//     x1 = f16tof32(bits1);
// }

//process uv1, uv2 data stored in ssmg mesh
void SSMGVertexData_float(float4 uv1, float4 uv2, float2 objectScale, 
    float convexityPower, float concavityPower, float topsidePower, float undersidePower,
    float borderWorldWidth,  float borderPower, /*float crackPower,*/
    out float convexity, out float concavity, out float topside, out float underside, out float border/*, out float crack*/)
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

   //crack = pow(max(uv2.y, uv2.z), crackPower);
}

void SSMGVertexData_half(float4 uv1, float4 uv2, float2 objectScale, 
    float convexityPower, float concavityPower, float topsidePower, float undersidePower,
    float borderWorldWidth,  float borderPower, /*float crackPower,*/
    out float convexity, out float concavity, out float topside, out float underside, out float border/*, out float crack*/)
{
    SSMGVertexData_float(uv1, uv2, objectScale, convexityPower, concavityPower, topsidePower, undersidePower, borderWorldWidth, borderPower,
        /*crackPower,*/ convexity, concavity, topside, underside, border/*, crack*/);
}

void SSMGCrackValue_float(float4 crack, float4 bary, out float val)
{
    float4 baryClamped = max(bary, 10E-05);
    crack *= step(10E-05, bary) / baryClamped;

    float4 val0 = float4(
        bary.y * crack.x + bary.x * crack.y,
        bary.z * crack.x + bary.x * crack.z,
        bary.w * crack.x + bary.x * crack.w,
        bary.z * crack.y + bary.y * crack.z);
    float4 val1 = float4(
        bary.w * crack.y + bary.y * crack.w,
        bary.w * crack.z + bary.z * crack.w,
        val0.zw);
    
    val1 = max(val0, val1);
    val = max(max(val1.x, val1.y), max(val1.z, val1.w));
}

void SSMGCrackValue_half(float4 crack, float4 bary, out float val)
{
    SSMGCrackValue_float(crack, bary, val);
}

// void ExtractBary_float(float3 p, out float3 q)
// {
//     half x0, x1, x2, x3, x4, x5;

//     FloatToHalf2(p.x, x0, x1);
//     FloatToHalf2(p.y, x2, x3);
//     FloatToHalf2(p.z, x4, x5);

//     int i = 0;
//     half r[6] = { 0, 0, 0, 0, 0, 0 };
//     r[i] = x0;
//     i += (uint)(x0 != 0);
//     r[i] = x1;
//     i += (uint)(x1 != 0);
//     r[i] = x2;
//     i += (uint)(x2 != 0);
//     r[i] = x3;
//     i += (uint)(x3 != 0);
//     r[i] = x4;
//     i += (uint)(x4 != 0);
//     r[i] = x5;

//     q = float3(r[0], r[1], r[2]);
// }

// void ExtractBary_half(float3 p, out float3 q)
// {
//     q = p;//and run for your life
// }

// void FloatToHalf2_half(float x, out half x0, out half x1)
// {
//     x0 = x;
//     x1 = 0;
// }

#endif