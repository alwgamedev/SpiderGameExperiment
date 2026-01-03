static const float PI = 3.14159265;

//for n >= 0;
float WholePow(float x, int n)
{
    float y = 1;
    while (n > 0)
    {
        if (n & 1)
        {
            y *= x;
        }
        
        x *= x;
        n >>= 1;
    }
    
    return y;
}

//from Lague
uint RandomUInt(inout uint state)
{
    state = state * 747796405 + 2891336453;
    uint result = ((state >> ((state >> 28) + 4)) ^ state) * 277803737;
    result = (result >> 22) ^ result;
    return result;
}

float RandomFloat01(inout uint state)
{
    return RandomUInt(state) / 4294967295.0;
}

float Poly2Kernel(float r2, float d2)
{
    //coeff is 1 / pi
    return 0.318309886 * (1 - d2 / r2) / r2;
}

float Poly4Kernel(float r2, float d2)
{
    //coeff is 3 / pi
    float a = 1 - d2 / r2;
    return 0.954929658 * a * a / r2;
}

//r = smoothing radius, d = distance to particle we're interpolating from
float Poly6Kernel(float r2, float d2)
{
    //coeff is 4 / pi
    float a = 1 - d2 / r2;
    return 1.27323954 * a * a * a / r2;
}

float2 Poly6KernelGradient(float r2, float d2, float2 dVector)
{
    //coeff is -24 / pi
    float a = (r2 - d2) / (r2 * r2);
    return -7.63943727 * a * a * dVector;
}

float Poly6KernelGradientNormSqrd(float r2, float d2)
{
    float a = (r2 - d2) / (r2 * r2);
    return 7.63943727 * a * a * d2;
}

float Poly18Kernel(float r2, float d2)
{
    //coeff is 10 / pi
    float a = 1 - d2 / r2;
    return 3.18309886 * WholePow(a, 9) / r2;
}

float2 Poly18KernelGradient(float r2, float d2, float2 dVector)
{
    //coeff is 180 / pi
    float a = 1 - d2 / r2;
    return -57.2957795 * WholePow(a, 8) / (r2 * r2) * dVector;

}

//just experimenting
float Poly2NKernel(int N, float r2, float d2)
{
    float a = 1 - d2 / r2;
    return (N + 1) * WholePow(a, N) / (PI * r2);
}

float2 Poly2NKernelGradient(int N, float r2, float d2, float2 dVector)
{
    float a = 1 - d2 / r2;
    return -2 * N * (N + 1) / (PI * r2 * r2) * WholePow(a, N - 1) * dVector;
}

float2 Poly2NKernelLaplacian(int N, float r2, float d2)
{
    float b = d2 / r2;
    float a = 1 - b;
    return -4 * N * (N + 1) / (PI * r2 * r2) * WholePow(a, N - 2) * (1 - N * b);

}

float SimpleLinearKernel(float r, float r2, float d)
{
    //normalized version of 1 - d / r
    //coeff is 3 / pi
    return 0.954929658 * (r - d) / (r * r2);
}

float SimpleQuadraticKernel(float r, float r2, float d)
{
    //normalized version of (1 - d / r)^2
    //coeff is 6 / pi
    float a = (r - d) / r2;
    return 1.90985931 * a * a;
}

//aka "spiky kernel" in mueller 03
float SimpleCubicKernel(float r, float r2, float d)
{
    //normalized version of (1 - d / r)^3
    //coeff is 10 / pi
    float a = 1 - d / r;
    return 3.18309886 * a * a * a / r2;
}
