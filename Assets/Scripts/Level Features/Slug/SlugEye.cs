using System;
using UnityEngine;

[Serializable]
public struct SlugeEye
{
    [Range(0, 1)][SerializeField] float bodyPosX;//interpolate btwn p1 and p2 in the slug pose
    [SerializeField] float bodyPosY;//how far out from body the eye extends
    [SerializeField] float tangentStrength0;
    [SerializeField] float tangentStrength1;
    [SerializeField] float springForce;
    [SerializeField] float springDamping;

    struct SpringNode
    {
        public Vector2 position;
        public Vector2 velocity;

        public void Integrate(float dt, Vector2 goalPosition, float springForce, float springDamping)
        {
            velocity += dt * (springForce * (goalPosition - position) - springDamping * velocity);
            position += dt * velocity;
        }
    }

    Vector2 basePosition;//start of stem
    Vector2 baseDirection;
    // SpringNode n1;//simulated point in the middle of stem
    SpringNode springNode;//end of stem

    //bezier control points for rendering
    public readonly Vector2 P0 => basePosition;
    public readonly Vector2 V0 => tangentStrength0 * baseDirection;
    public readonly Vector2 P1 => springNode.position;
    public readonly Vector2 V1 => tangentStrength1 * (springNode.position - basePosition);

    public void SnapToPosition(in SlugPose worldPose, float orientation)
    {
        var (basePoint, direction) = GetAnchorData(in worldPose, orientation);
        var endPoint = basePoint + bodyPosY * direction;

        basePosition = basePoint;
        // n1.position = 0.5f * (basePoint + endPoint);
        // n1.velocity = Vector2.zero;
        springNode.position = endPoint;
        springNode.velocity = Vector2.zero;
    }

    public void UpdateSpring(in SlugPose worldPose, float orientation, float dt)
    {
        var (basePoint, direction) = GetAnchorData(in worldPose, orientation);
        var anchorPoint = basePoint + bodyPosY * direction;

        basePosition = basePoint;

        // var n1Goal = 0.25f * basePoint + 0.75f * anchorPoint;
        // n1.Integrate(dt, n1Goal, springForce, springDamping);

        var n2Goal = anchorPoint;
        springNode.Integrate(dt, n2Goal, springForce, springDamping);
    }

    public void OnChangeOrientation(Vector2 right)
    {
        // n1.velocity = MathTools.ReflectAcrossHyperplane(n1.velocity, right);
        springNode.velocity = MathTools.ReflectAcrossHyperplane(springNode.velocity, right);
    }

    readonly (Vector2 basePoint, Vector2 direction) GetAnchorData(in SlugPose pose, float orientation)
    {
        var basePoint = MathTools.CubicInterpolation(pose.p1, pose.v1, pose.p2, pose.v2, bodyPosX);
        var v = MathTools.CubicTangent(pose.p1, pose.v1, pose.p2, pose.v2, bodyPosX);
        var direction = Mathf.Sign(orientation) * v.normalized.CCWPerp();
        return (basePoint, direction);
    }
}