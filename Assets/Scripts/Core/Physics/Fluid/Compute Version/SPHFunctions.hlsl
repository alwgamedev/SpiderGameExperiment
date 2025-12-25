static const float PI = 3.14159265;

//r = smoothing radius, d = distance to particle we're interpolating from
float DensityWt(float r2, float d2)
{
    return 4 / PI * pow(r2 - d2, 3) / pow(r2, 4);
}

//the actual gradient at (x,y) is this times (x,y) (where (x,y) will be the difference between two particle positions)
float PressureWt(float r, float d)
{
    return -pow(r - d, 2) / (d * pow(r, 5));
}

float ViscosityWt(float r, float d)
{
    return (r - d) / pow(r, 5);
}