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
    #endif