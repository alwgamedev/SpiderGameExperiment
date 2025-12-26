static const float PI = 3.14159265;

//r = smoothing radius, d = distance to particle we're interpolating from
//float DensityWt(float r, float d)
//{
//    return pow(1 - d / r, 2);
//}

//float NearDensityWt(float r, float d)
//{
//    return pow(1 - d / r, 3);
//}

//////the actual gradient at (x,y) is this times (x,y) (where (x,y) will be the difference between two particle positions)
//float pressurewt(float r, float d)
//{
//    return pow(r - d, 2) / (d * pow(r, 5));
//}

//float PressureWt(float r, float d)
//{
//    return 1 - d / r;
//}

//float ViscosityWt(float r, float d)
//{
//    return (r - d) / pow(r, 5);
//}

float LinearWt(float r, float d)
{
    return 1 - d / r;
}

float QuadraticWt(float r, float d)
{
    return pow(1 - d / r, 2);
}

float CubicWt(float r, float d)
{
    return pow(1 - d / r, 3);
}