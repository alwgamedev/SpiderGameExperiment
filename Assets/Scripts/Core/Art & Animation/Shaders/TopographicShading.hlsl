    #ifndef TOPOGRAPHIC_SHADING
    #define TOPOGRAPHIC_SHADING
    
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

    void NormalZ_float(float height, float2 worldPos, out float hx, out float hy, out float nz)
    {
        float4 jacInv = ScreenToWorldJacobian(worldPos);
        hx = ddxWorld(height, jacInv);
        hy = ddyWorld(height, jacInv);
        nz = rsqrt(1 + hx * hx + hy * hy);
    }

    void NormalZ_half(float height, float2 worldPos, out float hx, out float hy, out float nz)
    {
        NormalZ_float(height, worldPos, hx, hy, nz);
    }

    void HeightNormal_float(float hx, float hy, float nz, out float3 n)
    {
        n = float3(-nz * hx, -nz * hy, nz);
    }

    void HeightNormal_half(float hx, float hy, float nz, out float3 n)
    {
        HeightNormal_float(hx, hy, nz, n);
    }
    #endif