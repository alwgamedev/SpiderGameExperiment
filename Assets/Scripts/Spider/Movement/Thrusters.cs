using System;
using UnityEngine;

[Serializable]
public struct Thrusters
{
    [SerializeField] float drainRate;//charge lost per second
    [SerializeField] float rechargeRate;//charge gained per second
    [SerializeField] float rechargeThreshold;

    bool cooldown;//cooldown kicks in when you drain charge all the way to 0, then you can't engage again until back above rechargeThreshold
   
    public float Charge { get; private set; }//0-1
    public bool Engaged { get; private set; }

    public void Update(float dt)
    {
        if (Engaged)
        {
            Charge -= drainRate * dt;
            if (Charge <= 0)
            {
                Charge = 0;
                Disengage();
            }
        }
        else if (Charge < 1)
        {
            Charge += rechargeRate * dt;
            if (Charge > 1)
            {
                Charge = 1;
            }
            if (cooldown && Charge > rechargeThreshold)
            {
                cooldown = false;
            }
        }
    }

    //called will have OnEngage, OnEngageFailed, and OnDisengage methods that will get called based on return value
    //should only called Engage when input is first pressed down, therefore we will assume that Engaged is false when this method is called
    public bool Engage()
    {
        Engaged = !cooldown;
        return Engaged;
    }

    public bool Disengage()
    {
        if (!Engaged)
        {
            return false;
        }

        Engaged = false;
        cooldown = Charge == 0;
        return true;
    }
}