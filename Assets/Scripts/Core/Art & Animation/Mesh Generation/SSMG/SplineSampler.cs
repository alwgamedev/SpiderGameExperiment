using Unity.Collections;
using UnityEngine;
using UnityEngine.U2D;
using Unity.Burst;
using Unity.Mathematics;

public static class SplineSampler
{
    public static void SampleSpline(Spline spline, int arcLengthSamplesPerSegment, float splineSamplesPerUnitArcLength,
        NativeList<Vector2> output)
    {
        output.Clear();

        var numPoints = spline.GetPointCount();
        for (int i = 0; i < numPoints; i++)
        {
            var i1 = (i + 1) % numPoints;
            var p = spline.GetPosition(i);//already local position
            var pRightTangent = p + spline.GetRightTangent(i);
            var q = spline.GetPosition(i1);
            var qLeftTangent = q + spline.GetLeftTangent(i1);

            var arcLength = 0f;
            var p0 = p;
            for (int j = 1; j < arcLengthSamplesPerSegment + 1; j++)
            {
                var p1 = BezierUtility.BezierPoint(pRightTangent, p, q, qLeftTangent, (float)j / arcLengthSamplesPerSegment);
                arcLength += Vector2.Distance(p0, p1);
                p0 = p1;
            }

            int numSubPoints = (int)Mathf.Ceil(arcLength * splineSamplesPerUnitArcLength);

            for (int j = 0; j < numSubPoints; j++)
            {
                var s = (float)j / numSubPoints;
                var pj = BezierUtility.BezierPoint(pRightTangent, p, q, qLeftTangent, s);
                output.Add(pj);
            }
        }
    }
}