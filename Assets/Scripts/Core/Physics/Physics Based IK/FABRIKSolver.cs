using System;
using UnityEngine;

public static class FABRIKSolver
{
    public static bool RunFABRIKIteration(Span<Vector2> position, Span<float> length, float totalLength, Vector2 target, float toleranceSqrd)
    {
        if (Vector2.SqrMagnitude(target - position[0]) > totalLength * totalLength)
        {
            target = position[0] + totalLength * (target - position[0]).normalized;
        }

        if (Vector2.SqrMagnitude(target - position[^1]) < toleranceSqrd)
        {
            return false;
        }

        var anchor = position[0];//needs to be captured before running forward
        Forward(position, length, target);
        Backward(position, length, anchor);
        return true;
    }

    private static void Forward(Span<Vector2> position, Span<float> length, Vector2 target)
    {
        position[^1] = target;
        for (int i = position.Length - 2; i > -1; i--)
        {
            var u = (position[i] - position[i + 1]).normalized;
            position[i] = position[i + 1] + length[i] * u;
        }
    }

    private static void Backward(Span<Vector2> position, Span<float> length, Vector2 anchor)
    {
        position[0] = anchor;
        for (int i = 1; i < position.Length; i++)
        {
            var u = (position[i] - position[i - 1]).normalized;
            position[i] = position[i - 1] + length[i - 1] * u;
        }
    }
}