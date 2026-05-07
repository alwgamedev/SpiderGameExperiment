using UnityEngine;

[CreateAssetMenu(fileName = "New Dynamic Fluid Obstacle", menuName = "Scriptable Objects/Physics/Dynamic Fluid Obstacle")]
public class PBFDynamicObstacleSO : ScriptableObject
{
    public float repulsionRadius;
    public float repulsionRadiusMax;
    public float extentsMultiplier;
}
