static const float PI = 3.14159265;
static const float densityNormalizer = 4 / PI;
static const float pressureNormalizer = -30 / PI;
static const float viscosityNormalizer = 40 / PI;

//r = smoothing radius, d = distance to particle we're interpolating from
float DensityWt(float r2, float d2)
{
    return densityNormalizer * pow(r2 - d2, 3) / pow(r2, 4);
}

//the actual gradient at (x,y) is this times (x,y) (where (x,y) will be the difference between two particle positions)
float PressureWt(float r, float d)
{
    return pressureNormalizer * pow(r - d, 2) / (d * pow(r, 5));

}

float ViscosityWt(float r, float d)
{
    return viscosityNormalizer * (r - d) / pow(r, 5);
}