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

    //-gradient of baryCoord has length = 1 / (height of the triangle) (in direction corresponding to that bary coord)
    //-we need to go from the provided screen space partial derivatives back to world space partial derivatives,
    //so we use jacInv = (dx/dxWorld, dy/dxWorld, dx/dyWorld, dy/dyWorld) the jacobian
    //to the transformation (xWorld, yWorld) -> (xScreen, yScreen)
    float HeightInverse(float baryCoord, float4 jacInv)
    {
        float2 gradScreen = float2(ddx(baryCoord), ddy(baryCoord));
        float2 gradWorld = float2(dot(gradScreen, jacInv.xy), dot(gradScreen, jacInv.zw));
        return length(gradWorld);
    }

    void SSMGCrackValue_float(float4 crack, float4 bary, float2 worldPos, float thickness, out float val)
    {
        crack = step(1E-05, crack);
        
        float4 jac = float4(ddx(worldPos), ddy(worldPos));
        float det = (jac.x * jac.w - jac.y * jac.z);
        float4 jacInv = (1 / det) * float4(jac.w, -jac.y, -jac.z, jac.x);

        float4 edgeThickness1 = thickness * float4(
            HeightInverse(1 - bary.x - bary.y, jacInv),
            HeightInverse(1 - bary.x - bary.z, jacInv),
            HeightInverse(1 - bary.x - bary.w, jacInv),
            HeightInverse(1 - bary.y - bary.z, jacInv)
        );
        float2 edgeThickness2 = thickness * float2(
            HeightInverse(1 - bary.y - bary.w, jacInv),
            HeightInverse(1 - bary.z - bary.w, jacInv)
        );
        float4 cornerThickness = thickness * float4(
            HeightInverse(bary.x, jacInv),
            HeightInverse(bary.y, jacInv),
            HeightInverse(bary.z, jacInv),
            HeightInverse(bary.w, jacInv)
        );
        edgeThickness1 = min(edgeThickness1, 1);
        edgeThickness2 = min(edgeThickness2, 1);
        cornerThickness = min(cornerThickness, 1);

        float4 edgeCrack1 = crack.xxxy * crack.yzwz;
        float2 edgeCrack2 = crack.yz * crack.ww;
        float4 t1 = bary.xxxy + bary.yzwz;
        float2 t2 = bary.yz + bary.ww;
        edgeCrack1 *= max((t1 - 1 + edgeThickness1) / edgeThickness1, 0);
        edgeCrack2 *= max((t2 - 1 + edgeThickness2) / edgeThickness2, 0);

        float4 cornerCrack = crack * max((bary - cornerThickness) / (1 - cornerThickness), 0);
        
        float4 result = max(edgeCrack1, float4(edgeCrack2, 0, 0));
        result = max(result, cornerCrack);
        val = max(max(result.x, result.y), max(result.z, result.w));
    }

    void SSMGCrackValue_half(float4 crack, float4 bary, float2 worldPos, float thickness, out float val)
    {
        SSMGCrackValue_float(crack, bary, worldPos, thickness, val);
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