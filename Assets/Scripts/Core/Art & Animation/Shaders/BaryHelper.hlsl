#ifndef BARY_HELPER
#define BARY_HELPER

//bary coords coming from 4-colorings are float4's
//that may have one or two coords "missing" (zero).
//to extract valid bary coords, we throw out one of the zeroes to get down to a float3
//then replace the other zero (if it occurs) with (1 - sum of other bary coords)

//still for edge shading it's either one edge or all three... 
//(e.g. if we have a path through the triangulation we want to shade, the path can only use one edge in each triangle)
//and when it's only one edge per triangle you don't need bary coords, just assign a value of 1f to all the verts in the crack and 
//ordinary interpolation will fade away from the edge in the way you want.

//still the bary coords can be useful if you want a specific world thickness to the fade,
//then you can use bary coords (and partial derivatives) to find the height of the triangle you're in
//(see SSMGVertexData.hlsl)

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

//-we have an edge thickness we want to use that's measured in world units. we need to divide by the height of triangle
//to get the "local thickness" (in the range [0,1]).
//-gradient of baryCoord has length = 1 / (height of the triangle) (in direction corresponding to that bary coord)
//-we need to go from the provided screen space partial derivatives back to world space partial derivatives,
//so we use jacInv = (dx/dxWorld, dy/dxWorld, dx/dyWorld, dy/dyWorld),
//the jacobian to the transformation (xWorld, yWorld) -> (xScreen, yScreen)
float HeightInverse(float baryCoord, float4 jacInv)
{
    float2 gradScreen = float2(ddx(baryCoord), ddy(baryCoord));
    float2 gradWorld = float2(dot(gradScreen, jacInv.xy), dot(gradScreen, jacInv.zw));
    return length(gradWorld);
}

#endif