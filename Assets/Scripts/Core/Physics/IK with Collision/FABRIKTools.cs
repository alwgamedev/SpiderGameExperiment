using UnityEngine;

public static class FABRIKTools
{
    public static bool RunFABRIKIteration(Vector2[] position, float[] length, Vector2 target, float tolerance)
    {
        if (Vector2.SqrMagnitude(target - position[^1]) < tolerance * tolerance)
        {
            return false;
        }

        Vector2 anchor = position[0];
        Forward(position, length, target);
        Backward(position, length, anchor);
        return true;
    }

    private static void Forward(Vector2[] position, float[] length, Vector2 target)
    {
        position[^1] = target;
        for (int i = position.Length - 2; i > -1 ; i--)
        {
            var u = (position[i] - position[i + 1]).normalized;
            position[i] = position[i + 1] + length[i] * u;
        }
    }

    private static void Backward(Vector2[] position, float[] length, Vector2 anchor)
    {
        position[0] = anchor;
        for (int i = 1; i < position.Length; i++)
        {
            var u = (position[i] - position[i - 1]).normalized;
            position[i] = position[i - 1] + length[i - 1] * u;
        }
    }
}