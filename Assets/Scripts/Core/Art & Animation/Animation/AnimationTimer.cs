using UnityEngine;

public struct AnimationTimer
{
    //if these are colors, make sure they're in linear space, so they lerp properly!
    //note that material.GetColor as well as colors from SerializeFields are in gamma space,
    //and when you use material.SetColor, the input color is assumed to be in gamma space.
    //so it's best to A) make sure colors stored here are in linear space,
    //and B) use material.GetVector/SetVector instead of GetColor/SetColor

    public Vector4 startVal;
    public Vector4 goalVal;
    public float speed;
    public float timer;

    public readonly Vector4 AnimatedVal => Vector4.Lerp(startVal, goalVal, timer);

    public void SnapTo(Vector4 val, Material material, int propertyID, bool isFloat)
    {
        SnapTo(val);
        ApplyAnimatedValue(material, propertyID, isFloat);
    }

    public void SnapTo(Vector4 val)
    {
        startVal = val;
        goalVal = val;
        speed = 0;
        timer = 1;
    }

    public void BeginAnimation(Vector4 curVal, Vector4 goalVal, float speed)
    {
        startVal = curVal;
        this.goalVal = goalVal;
        this.speed = speed;
        timer = 1 - Mathf.Clamp(MaxDifference(curVal, goalVal) / speed, 0, 1);
    }

    public void Update(float dt)
    {
        timer += speed * dt;
    }

    public void Update(float dt, Material material, int propertyID, bool isFloat)
    {
        if (timer < 1)
        {
            Update(dt);
            ApplyAnimatedValue(material, propertyID, isFloat);
        }
    }


    public readonly void ApplyAnimatedValue(Material material, int propertyID, bool isFloat)
    {
        if (isFloat)
        {
            material.SetFloat(propertyID, AnimatedVal.x);
        }
        else
        {
            //sadly SetVector will cause an error when the propertyID points to a float 
            material.SetVector(propertyID, AnimatedVal);
        }
    }

    public static float MaxDifference(Vector4 v, Vector4 w)
    {
        var u = w - v;
        return Mathf.Max(Mathf.Max(Mathf.Abs(u.x), Mathf.Abs(u.y)), Mathf.Max(Mathf.Abs(u.z), Mathf.Abs(u.w)));
    }
}