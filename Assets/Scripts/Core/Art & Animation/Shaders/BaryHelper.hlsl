#ifndef BARY_HELPER
#define BARY_HELPER

//bary coords coming from 4-colorings are float4's
//that may have one or two coords "missing" (zero).
//to extract valid bary coords, we throw out one of the zeroes
//and replace the other zero (if it occurs) with 1 - sum of other bary coords

void MinBaryCoord_float(float4 bary, out float val)
{
    float4 opp = 1 - bary.yzwx - bary.zwxy - bary.wxyz;
    float4 bad = step(-1E-05, -bary);
    bary = lerp(bary, opp, bad);//replace any bad bary coords with their opposites
    bad = step(-1E-05, -bary);
    bary = lerp(bary, 1, bad);//replace any remaining bad coords with 1
    float2 min2 = min(bary.xy, bary.zw);
    val = min(min2.x, min2.y);
}

void MinBaryCoord_half(half4 bary, out half val)
{
    MinBaryCoord_float(bary, val);
}

void RepairBary3_float(float3 bary, out float3 baryOut)
{
    float3 bad = step(-1E-05, -bary);
    float3 opp = 1 - bary.yzx - bary.zxy;
    baryOut = lerp(bary, opp, bad);
}

void RepairBary3_half(half3 bary, out half3 baryOut)
{
    RepairBary3_half(bary, baryOut);
}

void RepairBary4_float(float4 bary, out float3 baryOut)
{
    float4 bad = step(-1E-05, -bary);
    baryOut = bary.xyz;
    baryOut = lerp(baryOut, bary.yzw, bad.x);
    baryOut = lerp(baryOut, bary.xzw, bad.y);
    baryOut = lerp(baryOut, bary.xyw, bad.z);
    baryOut = lerp(baryOut, bary.xyz, bad.w);
    RepairBary3_float(baryOut, baryOut);
}

void RepairBary4_half(half4 bary, out half3 baryOut)
{
    RepairBary4_float(bary, baryOut);
}

#endif