static const float PI = 3.14159265;

//r = smoothing radius, d = distance to particle we're interpolating from

float Poly6Kernel(float r2, float d2)
{
    return 1.27323954 * pow(1 - d2 / r2, 3) / r2;
}

float SimpleLinearKernel(float r, float r2, float d)
{
    //normalized version of 1 - d / r
    return 0.954929658 * (r - d) / (r * r2);
}

float SimpleQuadraticKernel(float r, float r2, float d)
{
    //normalized version of (1 - d / r)^2
    return 1.90985931 * pow((r - d) / r2, 2);
}

float SimpleCubicKernel(float r, float r2, float d)
{
    //normalized version of (1 - d / r)^3
    return 3.18309886 * pow(1 - d / r, 3) / r2;
}