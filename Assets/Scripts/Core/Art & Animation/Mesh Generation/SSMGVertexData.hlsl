#ifndef SSMG_VERTEX_DATA//if we use this file twice in one shader, this code will only get included once (like #pragma once)
    #define SSMG_VERTEX_DATA

    //outputs jacInv = (dx/dxWorld, dy/dxWorld, dx/dyWorld, dy/dyWorld),
    //the Jacobian to the transformation from screen space back to world space
    //(could also use objectSpace if you wanted)
    float4 ScreenToWorldJacobian(float2 worldPos)
    {
        float4 jac = float4(ddx(worldPos), ddy(worldPos));
        float det = (jac.x * jac.w - jac.y * jac.z);
        return (1 / det) * float4(jac.w, -jac.y, -jac.z, jac.x);
    }

    float ddxWorld(float val, float4 jacInv)
    {
        return ddx(val) * jacInv.x + ddy(val) * jacInv.y;
    }

    float ddyWorld(float val, float4 jacInv)
    {
        return ddx(val) * jacInv.z + ddy(val) * jacInv.w;
    }

    void NormalZ_float(float height, float2 worldPos, out float nz)
    {
        float4 jacInv = ScreenToWorldJacobian(worldPos);
        float hx = ddxWorld(height, jacInv);
        float hy = ddyWorld(height, jacInv);
        nz = rsqrt(1 + hx * hx + hy * hy);
    }

    void NormalZ_half(float height, float2 worldPos, out float nz)
    {
        NormalZ_float(height, worldPos, nz);
    }

    void SSMGDistToBdry_float(float2 objectScale, float distToBdry, out float d)
    {
        float scale = max(objectScale.x, objectScale.y);
        d = scale * distToBdry;
    }

    void SSMGDistToBdry_half(float2 objectScale, float distToBdry, out float d)
    {
        SSMGDistToBdry_float(objectScale, distToBdry, d);
    }

    void SSMGShadow_float(float concavity, float concavityStrength, float underside, float undersideStrength,
        float border, float borderStrength, float crack, float crackStrengthMin,  float crackStrengthMax,
        float crackSpread, float crackSpreadStrengthMin, float crackSpreadStrengthMax, float crackNoise, 
        out float shadow)
    {
        float crackStrength = lerp(crackStrengthMin, crackStrengthMax, crackNoise);
        float crackSpreadStrength = lerp(crackSpreadStrengthMin, crackSpreadStrengthMax, crackNoise);

        shadow = saturate(1 - concavityStrength * concavity);
        shadow *= saturate(1 - undersideStrength * underside);
        shadow *= saturate(1 - borderStrength * border);
        shadow *= saturate(1 - crackStrength * crack);
        shadow *= saturate(1 - crackSpreadStrength * crackSpread);
    }

    void SSMGShadow_half(float concavity, float concavityStrength, float underside, float undersideStrength,
        float border, float borderStrength, float crack, float crackStrengthMin,  float crackStrengthMax,
        float crackSpread, float crackSpreadStrengthMin, float crackSpreadStrengthMax, float crackNoise, 
        out float shadow)
    {
        SSMGShadow_float(concavity, concavityStrength, underside, undersideStrength, border, borderStrength, crack, 
            crackStrengthMin, crackStrengthMax, crackSpread, crackSpreadStrengthMin, crackSpreadStrengthMax, crackNoise,
            shadow);
    }

    //-we have an edge thickness we want to use that's measured in world units. we need to divide by the height of triangle
    //to get the "local thickness" (in the range [0,1]).
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

        float4 cornerCrack = crack * max((bary - 1 + cornerThickness) / cornerThickness, 0);
        
        float4 result = max(edgeCrack1, float4(edgeCrack2, 0, 0));
        result = max(result, cornerCrack);
        val = max(max(result.x, result.y), max(result.z, result.w));
    }

    void SSMGCrackValue_half(float4 crack, float4 bary, float2 worldPos, float thickness, out float val)
    {
        SSMGCrackValue_float(crack, bary, worldPos, thickness, val);
    }

    //process uv1, uv2 data stored in ssmg mesh
    void SSMGVertexData_float(float4 uv1, float4 uv2, float4 uv3, float4 uv4, 
        float2 objectScale, float2 worldPos, float borderWorldWidth, float crackThickness,
        out float convexity, out float concavity, out float topside, out float underside, out float border, 
        out float crack, out float crackSpread)
    {
        convexity = uv1.x;//pow(uv1.x, convexityPower);
        concavity = uv1.y;//pow(uv1.y, concavityPower);
        topside = uv1.z;//pow(uv1.z, topsidePower);
        underside = uv1.w;//pow(uv1.w, undersidePower);

        float scale = max(objectScale.x, objectScale.y);
        float localDistToBorder = uv2.x;
        float worldDistToBorder = scale * localDistToBorder;
        border = clamp(1 - worldDistToBorder / borderWorldWidth, 0, 1);
        border *= border;//pow(border, borderPower);

        SSMGCrackValue_float(uv4, uv3, worldPos, crackThickness, crack);
        crack *= crack;
        crackSpread = uv2.y;//pow(uv2.y, crackSpreadPower);
    }

    void SSMGVertexData_half(float4 uv1, float4 uv2, float4 uv3, float4 uv4, 
        float2 objectScale, float2 worldPos, float borderWorldWidth, float crackThickness,
        out float convexity, out float concavity, out float topside, out float underside, out float border, 
        out float crackSpread, out float crack)
    {
        SSMGVertexData_float(uv1, uv2, uv3, uv4, objectScale, worldPos, borderWorldWidth, crackThickness,
            convexity, concavity, topside, underside, border, crackSpread, crack);
    }

#endif