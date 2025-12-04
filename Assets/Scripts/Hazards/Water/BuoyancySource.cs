using System.Collections.Generic;
using UnityEngine;

public class BuoyancySource : MonoBehaviour
{
    [SerializeField] float fluidDensity;
    [SerializeField] float dampingFactor;
    //[SerializeField] float testingHeight;

    public WaterMesh WaterMesh { get; private set; }

    private void Awake()
    {
        WaterMesh = GetComponent<WaterMesh>();
    }

    public Vector2 BuoyancyForce(float areaDisplaced)
    {
        return areaDisplaced * fluidDensity * Vector2.up;
    }

    public Vector2 DragForce(float speed, float crossSectionWidth, Vector2 velocityDirection)
    {
        //we'll say the density got swallowed into the dampingFactor
        //(when density is high, the dampingfactor has to be really small, making this 
        //more of a pain to fine tune)
        return -dampingFactor * crossSectionWidth * speed * speed * velocityDirection;
    }

    //to-do
    public float FluidHeight(float xPosition)
    {
        if (WaterMesh)
        {
            return WaterMesh.WaveYPosition(xPosition);
        }
        return transform.position.y + 0.5f * transform.lossyScale.y;
    }

    private void OnTriggerStay2D(Collider2D collider)
    {
        //im hoping the lookup is faster than TryGetComponent?
        if (gameObject.activeInHierarchy && BuoyantObject.LookUp.TryGetValue(collider.gameObject, out var b)) 
        {
            b.OnEnteredWater(this);
        }
    }
}