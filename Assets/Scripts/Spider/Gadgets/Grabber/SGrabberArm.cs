using System;
using UnityEngine;
using Unity.U2D.Physics;

[Serializable]
public class SGrabberArm
{
    [SerializeField] JointedChain arm;
    [SerializeField] SGrabberClaw claw;
    [SerializeField] SDoubleDoor depositDoor;
    [SerializeField] SDoubleDoor mouth;
    [SerializeField] SpriteRenderer[] sprites;
    [SerializeField] PhysicsQuery.QueryFilter grabFilter;
    [SerializeField] Transform upperClawArm;
    [SerializeField] Transform lowerClawArm;
    [SerializeField] Transform depositTarget;
    [SerializeField] Transform offAnchorPosition;
    [SerializeField] Transform deployedAnchorPosition;
    [SerializeField] Vector2[] foldedPose;
    [SerializeField] Vector2[] defaultPose;
    [SerializeField] Vector2[] depositPose;
    [SerializeField] float[] depositPoseWeight;
    [SerializeField] float grabTimeOut;

    SpiderInput input;

    bool actionInProgress;//will not process input while action in progress
    float grabTimer;

    PhysicsBody grabTarget;

    Action onArmTargetReached;
    Action onAnchorTargetReached;
    Action onGrabberTargetReached;

    public void Start(SpiderInput input)
    {
        this.input = input;
    }
}