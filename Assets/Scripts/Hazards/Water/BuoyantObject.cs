using System.Collections.Generic;
using UnityEngine;

public class BuoyantObject : MonoBehaviour
{
    //[SerializeField] float exitBuffer = .1f;
    //[SerializeField] float effectiveWidthMultiplier = 1;

    Rigidbody2D rb;
    Collider2D coll;
    BuoyancySource buoyancySource;

    float width;
    float weight;

    public static Dictionary<GameObject, BuoyantObject> LookUp = new();

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        coll = GetComponent<Collider2D>();

        //width = effectiveWidthMultiplier * (coll.bounds.max.x - coll.bounds.min.x);
        //weight = coll.bounds.max.y - coll.bounds.min.y;
    }

    private void OnEnable()
    {
        LookUp[gameObject] = this;
    }

    //private void FixedUpdate()
    //{
    //    if (buoyancySource)
    //    {
    //        var h = buoyancySource.FluidHeight(coll.bounds.center.x);

    //        if (coll.bounds.min.y > h + exitBuffer)
    //        {
    //            buoyancySource = null;
    //            return;
    //        }

    //        //APPLY BUOYANCY FORCE & DRAG
    //        rb.AddForce(buoyancySource.BuoyancyForce(DisplacedArea(h)));

    //        //surely there is a better way to do this...
    //        var s = rb.linearVelocity.magnitude;
    //        if (s > 1E-05f)
    //        {
    //            var u = rb.linearVelocity / s;
    //            var c = CrossSectionWidth(u);
    //            rb.AddForce(buoyancySource.DragForce(s, c, u));
    //        }

    //        //AGITATE WATER
    //        buoyancySource.WaterMeshManager.HandleDisplacement(coll, Time.deltaTime);
    //    }
    //}

    ////assume the buoyant object is rectangular and upright (never rotates)
    //public float DisplacedArea(float fluidHeight)
    //{
    //    return Mathf.Max((fluidHeight - coll.bounds.min.y) * width, 0);
    //}

    ///// <summary>
    ///// Cross section width in direction of current velocity
    //public float CrossSectionWidth(Vector2 velocityDirection)
    //{
    //    if (velocityDirection.y < MathTools.o41)
    //    {
    //        return weight;
    //    }

    //    return Mathf.Min(weight, width / velocityDirection.y);
    //}

    public void OnEnteredWater(BuoyancySource b)
    {
        buoyancySource = b;
    }

    private void OnDisable()
    {
        LookUp.Remove(gameObject);
    }
}