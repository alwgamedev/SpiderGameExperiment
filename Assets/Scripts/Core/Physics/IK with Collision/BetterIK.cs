using System.Collections.Generic;
using UnityEngine;

public static class BetterIK
{
    //make sure to fill ray before passing to method
    public static bool RunIterationWithCollisionAvoidance(Vector2[] position, Vector2[] ray, float[] length, float[] armHalfWidth,
        Vector2 target, float tolerance, float totalLength,
        int collisionMask, float horizontalRaycastSpacing, float failedRaycastShift, int failedRaycastNumShifts, float raycastLength,
        Vector2[] boundary, float[] deltaAngle, float[] gradient, float maxChange,
        int fwIterations, HashSet<int> verticesVisited)
    {
        var l2 = Vector2.SqrMagnitude(target - position[0]);
        if (l2 > totalLength * totalLength)
        {
            target = position[0] + totalLength * (target - position[0]).normalized;
        }

        var error = target - position[^1];
        var d2 = error.sqrMagnitude;
        if (d2 < tolerance * tolerance)
        {
            return false;
        }

        //this helped but we really need more sophisticated pathing
        //var r = Physics2D.Linecast(position[^1], target, collisionMask);
        //if (r.distance == 0)
        //{
        //    r = Physics2D.Linecast(position[^2], target, collisionMask);
        //}
        //if (r && r.distance > 0)
        //{
        //    target = position[^1] + error - Vector2.Dot(error, r.normal) * r.normal;
        //    error = target - position[^1];
        //}

        ComputeBoundary(position, ray, length, armHalfWidth, boundary, collisionMask, horizontalRaycastSpacing, failedRaycastShift, failedRaycastNumShifts, raycastLength);

        Vector2 dp = Vector2.zero;
        int startingVertex = 0;
        int irrelevantRays = 0;//nonzero bits are the rays parallel to error vector (which we can ignore in our optimization search, since they'd move perpendicularly to error)

        for (int i = 0; i < deltaAngle.Length; i++)
        {
            var c = MathTools.Cross2D(ray[i], dp) / length[i];
            var a = (boundary[i].x - c) / length[i];
            var b = (boundary[i].y - c) / length[i];

            var d = MathTools.Cross2D(ray[i], error);
            if (d > 0)
            {
                startingVertex |= 1 << i;
            }
            else if (d == 0)
            {
                irrelevantRays |= 1 << i;
            }

            a = Mathf.Clamp(0, a, b);
            deltaAngle[i] = a;
            dp += a * ray[i].CCWPerp();
        }

        //Now... deltaAngle lives in a compact convex polytope, and we want to minimize the convex function
        //|error - dp|^2 over this polytope, where dp = change in final position. We can minimize using the Frank-Wolfe Algorithm.

        //FRANK-WOLFE ALGORITHM:
        //-Start at any point in the polytope, and compute the objective function's gradient there.
        //-Find the vertex that is in the direction most opposite to the gradient.
        //-Move to the point between you and that vertex that minimizes the objective function, and repeat.
        for (int i = 0; i < fwIterations; i++)
        {
            //STEP 1: compute gradient at current point (and its dot with the starting vertex, while we're at it)
            var dq = Vector2.zero;
            var predictedPosition = position[^1] + dp;
            var predictedError = target - predictedPosition;
            int bestVertex = startingVertex;
            bool gradientIsZero = false;
            float min = 0;//minimum dot of gradient with vertices

            if (predictedError.sqrMagnitude < tolerance * tolerance || (predictedPosition - position[0]).sqrMagnitude > totalLength * totalLength)
            {
                break;
                //break if we're already within tolerance or have gone outside our reach
                //(2nd condition keeps us from doing excessive iterations when target far away)
            }

            for (int j = 0; j < deltaAngle.Length; j++)
            {
                var bit = 1 << j;
                if ((irrelevantRays & bit) != 0)
                {
                    dq += deltaAngle[j] * ray[j].CCWPerp();
                    continue;
                }

                //we don't need gradient for irrelevant rays, since those deltaAngles are fixed (and will contribute the same to the dot for every vertex)
                gradient[j] = MathTools.Cross2D(predictedError, ray[j]);
                if (gradientIsZero && gradient[j] != 0)
                {
                    gradientIsZero = false;
                }

                var c = MathTools.Cross2D(ray[j], dq) / length[j];
                bool useMax = (bestVertex & bit) != 0;
                var a = ((useMax ? boundary[j].y : boundary[j].x) - c) / length[j];

                min += a * gradient[j];
                dq += a * ray[j].CCWPerp();
            }

            if (gradientIsZero)//we're already at the min; end our FW iterations (this never seems to happen btw)
            {
                break;
            }

            verticesVisited.Clear();
            verticesVisited.Add(bestVertex);

            //STEP 2: find the vertex that has smallest dot with the gradient
            while (true)
            {
                int nextVertex = bestVertex;

                //check all adjacent vertices to see if we can reduce min
                for (int j = 0; j < deltaAngle.Length; j++)
                {
                    if ((irrelevantRays & 1 << j) != 0)//because deltaAngle[j] is fixed
                    {
                        continue;
                    }

                    int vertex = bestVertex ^ 1 << j;//flip j-th bit to move to an adjacent vertex
                    if (verticesVisited.Contains(vertex))
                    {
                        continue;
                    }
                    else
                    {
                        verticesVisited.Add(vertex);
                    }

                    //compute dot of vertex with gradient (there is some redundancy in these calculations atm)
                    var dot = 0f;
                    dq = Vector2.zero;
                    for (int k = 0; k < deltaAngle.Length; k++)
                    {
                        var bit = 1 << k;
                        if ((irrelevantRays & bit) != 0)
                        {
                            dq += deltaAngle[k] * ray[k].CCWPerp();
                            continue;
                        }

                        var c = MathTools.Cross2D(ray[k], dq) / length[k];
                        bool useMax = (vertex & bit) != 0;
                        var a = ((useMax ? boundary[k].y : boundary[k].x) - c) / length[k];

                        dot += a * gradient[k];
                        dq += a * ray[k].CCWPerp();
                    }

                    if (dot < min)
                    {
                        min = dot;
                        nextVertex = vertex;
                    }
                }

                if (nextVertex == bestVertex)//none of the adjacent vertices showed an improvement on min; we're already at the best vertex
                {
                    break;
                }
                else
                {
                    bestVertex = nextVertex;
                }
            }

            //STEP 3: minimize objective function between current deltaAngle and bestVertex
            dq = Vector2.zero;
            for (int j = 0; j < deltaAngle.Length; j++)
            {
                var bit = 1 << j;
                if ((irrelevantRays & bit) != 0)
                {
                    dq += deltaAngle[j] * ray[j].CCWPerp();
                    continue;
                }

                var c = MathTools.Cross2D(ray[j], dq) / length[j];
                bool useMax = (bestVertex & bit) != 0;
                var a = ((useMax ? boundary[j].y : boundary[j].x) - c) / length[j];
                dq += a * ray[j].CCWPerp();
                gradient[j] = a;//store the bestVertex's deltaAngle in gradient array, because it's available
            }

            var t = (dq - dp).sqrMagnitude;
            if (t == 0)
            {
                break;
            }
            t = Mathf.Clamp(Vector2.Dot(dq - dp, error - dp) / t, 0, 1);
            dp += t * (dq - dp);
            for (int j = 0; j < deltaAngle.Length; j++)
            {
                deltaAngle[j] += t * (gradient[j] - deltaAngle[j]);//we stored bestVertex's coordinates in gradient
            }
        }

        var totalChange = 0f;
        for (int i = 0; i < deltaAngle.Length; i++)
        {
            totalChange += Mathf.Abs(deltaAngle[i]);
        }
        var s = totalChange > maxChange ? maxChange / totalChange : 1f;

        //compute the new positions
        for (int i = 1; i < position.Length; i++)
        {
            var u = ray[i - 1] / length[i - 1];
            u = MathTools.CheapRotation(u, s * deltaAngle[i - 1], out _);//multiply deltaAngles by s to keep movement smooth
            position[i] = position[i - 1] + length[i - 1] * u;
            ray[i - 1] = position[i] - position[i - 1];
        }
        //before returning we could also check for a stall (stuck)
        //if the actual change in position is very small and error big, that's a good indicator of a stall
        return true;
    }

    private static void ComputeBoundary(Vector2[] position, Vector2[] ray, float[] length, float[] armHalfWidth, Vector2[] boundary, int collisionMask,
        float horizontalRaycastSpacing, float failedRaycastShift, int failedRaycastNumShifts, float raycastLength)
    {
        for (int i = 1; i < position.Length; i++)
        {
            var u = ray[i - 1] / length[i - 1];
            var n = u.CCWPerp();

            var v = raycastLength * n;
            var x = 0f;
            float lowerYMax = -length[i - 1];
            float upperYMin = length[i - 1];

            while (x < length[i - 1])
            {
                var q = position[i - 1] + x * u;

                var r = Physics2D.Linecast(q, q - v, collisionMask);
                if (r)
                {
                    if (r.distance == 0)
                    {
                        for (int j = 1; j < failedRaycastNumShifts + 1; j++)
                        {
                            r = Physics2D.Linecast(q + j * failedRaycastShift * n, q - v, collisionMask);
                            if (r && (r.distance > 0 || j == failedRaycastNumShifts))
                            {
                                var y = j * failedRaycastShift - r.distance;
                                if (y > lowerYMax)
                                {
                                    lowerYMax = y;
                                }
                                break;
                            }
                        }
                    }
                    else if (-r.distance > lowerYMax)
                    {
                        lowerYMax = -r.distance;
                    }
                }

                r = Physics2D.Linecast(q, q + v, collisionMask);
                if (r)
                {
                    if (r.distance == 0)
                    {
                        for (int j = 1; j < failedRaycastNumShifts + 1; j++)
                        {
                            r = Physics2D.Linecast(q - j * failedRaycastShift * n, q + v, collisionMask);
                            if (r && (r.distance > 0 || j == failedRaycastNumShifts))
                            {
                                var y = -j * failedRaycastShift + r.distance;
                                if (y < upperYMin)
                                {
                                    upperYMin = y;
                                }
                                break;
                            }
                        }
                    }
                    else if (r.distance < upperYMin)
                    {
                        upperYMin = r.distance;
                    }
                }

                x += horizontalRaycastSpacing;
            }

            upperYMin -= armHalfWidth[i - 1];
            lowerYMax += armHalfWidth[i - 1];
            upperYMin = Mathf.Max(upperYMin, lowerYMax);
            //Debug.DrawLine(position[i - 1] + lowerYMax * n, position[i] + lowerYMax * n, Color.red);
            //Debug.DrawLine(position[i - 1] + upperYMin * n, position[i] + upperYMin * n, Color.yellow);

            boundary[i - 1] = new(lowerYMax, upperYMin);
        }
    }
}