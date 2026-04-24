using System;
using UnityEngine;
using Unity.U2D.Physics;

[Serializable]
public struct SGrabberClaw
{
    [SerializeField] PhysicsRotate upperArmOpen;
    [SerializeField] PhysicsRotate upperArmClosed;
    [SerializeField] PhysicsRotate lowerArmOpen;
    [SerializeField] PhysicsRotate lowerArmClosed;
    [SerializeField] float grabArmRotationSpeed;
    [Range(0, 1)][SerializeField] float rotationTolerance;//closer to 1 is stricter
    [SerializeField] float grabTolerance;
    [SerializeField] float dropTolerance;
}