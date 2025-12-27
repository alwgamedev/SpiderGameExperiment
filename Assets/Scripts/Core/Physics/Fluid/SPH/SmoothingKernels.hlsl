static const float PI = 3.14159265;

//for n >= 0;
float wholePow(float x, int n)
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

//r = smoothing radius, d = distance to particle we're interpolating from
float Poly6Kernel(float r2, float d2)
{
    //coeff is 4 / pi
    float a = 1 - d2 / r2;
    return 1.27323954 * a * a * a / r2;
}

float2 Poly6KernelGradient(float r2, float d2, float2 dVector)
{
    float a = (r2 - d2) / (r2 * r2);
    return -7.63943727 * a * a * dVector;
}

float2 Poly6KernelGradientNormSqrd(float r2, float d2)
{
    float a = (r2 - d2) / (r2 * r2);
    return 7.63943727 * a * a * d2;
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

float SimpleCubicKernel(float r, float r2, float d)
{
    //normalized version of (1 - d / r)^3
    //coeff is 10 / pi
    float a = 1 - d / r;
    return 3.18309886 * a * a * a / r2;
}