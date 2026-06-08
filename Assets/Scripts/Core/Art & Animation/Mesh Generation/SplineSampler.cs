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

    [BurstCompile]
    public static void RandomizeSpline(NativeArray<Vector2> points, float maxOffset, float smoothingIterations,
        Unity.Mathematics.Random rng, Transform transform)
    {
        for (int i = 0; i < points.Length; i++)
        {
            var i1 = math.select(i + 1, 0, i == points.Length - 1);
            Debug.DrawLine(transform.TransformPoint(points[i]), transform.TransformPoint(points[i1]), Color.blue, 30);
        }

        NativeArray<float> offset = new(points.Length, Allocator.Temp);//(normal, distance, edgeLength)

        for (int i = 0; i < points.Length; i++)
        {
            offset[i] = rng.NextFloat(-maxOffset, maxOffset);
        }

        for (int iter = 0; iter < smoothingIterations; iter++)
        {
            for (int i = 0; i < offset.Length; i++)
            {
                var iPrev = math.select(i - 1, offset.Length - 1, i == 0);
                var iNext = math.select(i + 1, 0, i == offset.Length - 1);
                offset[i] = 0.25f * offset[i] + 0.375f * (offset[iPrev] + offset[iNext]);
            }
        }

        for (int i = 0; i < points.Length; i++)
        {
            var i1 = math.select(i + 1, 0, i == points.Length - 1);
            var p = points[i];
            var p1 = points[i1];
            var l = math.distance(p, p1);
            var n = 1 / l * (p1 - p).CCWPerp();
            points[i] += offset[i] * n;
        }

        for (int i = 0; i < points.Length; i++)
        {
            var i1 = math.select(i + 1, 0, i == points.Length - 1);
            Debug.DrawLine(transform.TransformPoint(points[i]), transform.TransformPoint(points[i1]), Color.yellow, 30);
        }
    }
}