public class Spring1D
{
    float displacement;
    float velocity;
    float acceleration;

    public float Displacement => displacement;
    public float Velocity => velocity;

    public void Update(float springConstant, float dampingFactor)
    {
        displacement += velocity;
        velocity += acceleration;
        acceleration = -springConstant * displacement - dampingFactor * velocity;
    }

    public void ApplyAcceleration(float a)
    {
        velocity += a;
    }

    public void ApplyVelocity(float v)
    {
        displacement += v;
    }
}