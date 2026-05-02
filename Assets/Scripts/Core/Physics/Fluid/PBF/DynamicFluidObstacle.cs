using UnityEngine;

[CreateAssetMenu(fileName = "New Dynamic Fluid Obstacle", menuName = "Scriptable Objects/Physics/Dynamic Fluid Obstacle")]
public class DynamicFluidObstacle : ScriptableObject
{
    public float repulsionRadius;
    public float repulsionRadiusMax;
    public float extentsMultiplier;
}