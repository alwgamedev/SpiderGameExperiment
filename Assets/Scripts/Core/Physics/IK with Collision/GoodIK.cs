using System;
using UnityEngine;

public static class GoodIK
{
    public static bool RunIteration(Vector2[] position, float[] length, float[] gradient, Vector2 target, float tolerance, float maxLength, float smoothingRate)
    {
        var l2 = Vector2.SqrMagnitude(target - position[0]);
        if (l2 > maxLength * maxLength)
        {
            target = position[0] + maxLength * (target - position[0]).normalized;
        }

        var dx = position[^1].x - target.x;
        var dy = position[^1].y - target.y;
        var d2 = dx * dx + dy * dy;
        if (d2 < tolerance * tolerance)
        {
            return false;
        }

        var denom = 0f;
        for (int i = 1; i < position.Length; i++)
        {
            Vector2 v = position[i] - position[i - 1];
            float der = 2 / length[i - 1] * (-v.y * dx + v.x * dy);
            denom += der * der;
            gradient[i - 1] = der;
        }

        if (denom == 0)
        {
            return false;
        }

        float lambda = -smoothingRate * d2 / denom;
        for (int i = 1; i < position.Length; i++)
        {
            var u = (position[i] - position[i - 1]) / length[i - 1];
            u = MathTools.CheapFromToRotation(u, u.CCWPerp(), lambda * gradient[i - 1], out var changed); 
            if (changed)
            {
                position[i] = position[i - 1] + length[i - 1] * u;
            }

        }
        return true;
    }

    //can also add a collision width to each arm (very easy just do two raycasts at +/- offsets)
    public static bool RunIterationWithCollisionAvoidance(Vector2[] position, Vector2[] newPosition, float[] length, float[] gradient, int[] lambdaWeight, Vector2 target, 
        float tolerance, float maxLength, float smoothingRate, int collisionMask, int collisionIntervals)
    {
        var l2 = Vector2.SqrMagnitude(target - position[0]);
        if (l2 > maxLength * maxLength)
        {
            target = position[0] + maxLength * (target - position[0]).normalized;
        }

        var dx = position[^1].x - target.x;
        var dy = position[^1].y - target.y;
        var d2 = dx * dx + dy * dy;
        if (d2 < tolerance * tolerance)
        {
            return false;
        }

        var denom = 0f;
        for (int i = 1; i < position.Length; i++)
        {
            Vector2 v = position[i] - position[i - 1];
            float der = 2 / length[i - 1] * (-v.y * dx + v.x * dy);
            denom += der * der;
            gradient[i - 1] = der;
        }

        if (denom == 0)
        {
            return false;
        }

        float lambda = -smoothingRate * d2 / denom;
        newPosition[0] = position[0];
        Array.Fill(lambdaWeight, collisionIntervals);
        bool anyLambdaPositive;
        while (true)
        {
            bool collision = false;
            anyLambdaPositive= false;
            for (int i = 1; i < position.Length; i++)
            {
                var u = (position[i] - position[i - 1]) / length[i - 1];
                if (lambdaWeight[i - 1] > 0)
                {
                    anyLambdaPositive = true;
                    var a = (float)lambdaWeight[i - 1] / collisionIntervals;
                    u = MathTools.CheapFromToRotation(u, u.CCWPerp(), a * lambda * gradient[i - 1], out _);
                }

                newPosition[i] = newPosition[i - 1] + length[i - 1] * u;

                //may work slightly better if you set all new positions then go backwards down the newPosition arr to check collision
                //(might give better maneuvarability)
                var r = Physics2D.Linecast(newPosition[i - 1], newPosition[i], collisionMask);
                if (r)
                {
                    collision = true;
                    if (lambdaWeight[i - 1] > 0)
                    {
                        lambdaWeight[i - 1]--;
                    }
                    else
                    {
                        for (int j = 0; j < i - 1; j++)
                        {
                            if (lambdaWeight[j] > 0)
                            {
                                lambdaWeight[j]--;
                                break;
                            }
                        }
                    }
                    break;
                }
            }

            if (!collision || !anyLambdaPositive)
            {
                break;
            }
        }

        if (anyLambdaPositive)
        {
            Array.Copy(newPosition, position, newPosition.Length);
            return true;
        }
        else
        {
            return false;
        }
    }
}