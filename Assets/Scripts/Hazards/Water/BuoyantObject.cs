using System;
using UnityEngine;

public class BuoyantObject : MonoBehaviour
{
    [SerializeField] float exitBuffer = .1f;
    [SerializeField] float effectiveWidthMultiplier = 1;

    Rigidbody2D rb;
    Collider2D coll;
    BuoyancySource buoyancySource;
    bool inWater;

    public float Width { get; private set; }
    public float Height { get; private set; }
    public bool InWater => inWater;

    public event Action<BuoyancySource> WaterEntered;
    public event Action WaterExited;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        coll = GetComponent<Collider2D>();

        Width = effectiveWidthMultiplier * (coll.bounds.max.x - coll.bounds.min.x);
        Height = coll.bounds.max.y - coll.bounds.min.y;
    }

    private void FixedUpdate()
    {
        if (buoyancySource && buoyancySource.gameObject.activeInHierarchy)
        {
            var h = buoyancySource.FluidHeight(coll.bounds.center.x);

            if (coll.bounds.min.y > h + exitBuffer)
            {
                buoyancySource = null;
                inWater = false;
                WaterExited?.Invoke();
                return;
            }

            rb.AddForce(buoyancySource.BuoyancyForce(DisplacedArea(h)));

            var s = rb.linearVelocity.magnitude;
            if (s > 1E-05f)//unity uses this in their normalize function to prevent NaN errors from division
                           //(not using normalize bc I need the speed, and don't want to compute magnitude twice)
            {
                var u = rb.linearVelocity / s;
                var c = CrossSectionWidth(u);
                rb.AddForce(buoyancySource.DragForce(s, c, u));
            }
        }
        else if (inWater) //in case buoyancy source got destroyed or disabled
        {
            inWater = false;
            buoyancySource = null;
            WaterExited?.Invoke();
        }
    }

    //assume the buoyant object is rectangular and upright (never rotates)
    public float DisplacedArea(float fluidHeight)
    {
        return Mathf.Max((fluidHeight - coll.bounds.min.y) * Width, 0);
    }

    /// <summary>
    /// Cross section width in direction of current velocity (you can pass in speed if it has alread been calculated).
    public float CrossSectionWidth(Vector2 velocityDirection)
    {
        if (velocityDirection.y < 1E-05f)
        {
            return Height;
        }

        return Mathf.Min(Height, Width / velocityDirection.y);
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (!collider.gameObject.activeInHierarchy)
            return;

        if (collider.gameObject.TryGetComponent(out BuoyancySource b))
        {
            buoyancySource = b;
            inWater = true;
            WaterEntered?.Invoke(b);
        }
    }

    private void OnTriggerStay2D(Collider2D collider)
    {
        if (!buoyancySource)
        {
            OnTriggerEnter2D(collider);
        }
    }

    //private void OnTriggerExit2D(Collider2D collider)
    //{
    //    if (buoyancySource && collider.transform == buoyancySource.transform)
    //    {
    //        buoyancySource = null;
    //    }
    //}

    private void OnDestroy()
    {
        WaterEntered = null;
        WaterExited = null;
    }
}