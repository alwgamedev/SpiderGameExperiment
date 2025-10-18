using System;
using UnityEngine;

[Serializable]
public struct Thrusters
{
    [SerializeField] float drainRate;//charge lost per second
    [SerializeField] float rechargeRate;//charge gained per second
    [SerializeField] float rechargeThreshold;

    bool cooldown;//cooldown kicks in when you drain charge all the way to 0, then you can't engage again until back above rechargeThreshold
   
    public float Charge { get; private set; }
    public bool Engaged { get; private set; }

    public enum ThrustersUpdateResult
    {
        ChargeRanOut, CooldownEnded, None
    }
    //on cooldown ended may want to change ui (e.g. change color of UI or get rid of a "FORCED RECHARGE..." or "COOLDOWN..." message)

    public void Initialize()
    {
        Charge = 1;
    }

    //returns whether disengaged due to charge running out
    public ThrustersUpdateResult Update(float dt)
    {
        if (Engaged)
        {
            Charge -= drainRate * dt;
            if (Charge <= 0)
            {
                Charge = 0;
                Disengage();
                return ThrustersUpdateResult.ChargeRanOut;
            }
            return ThrustersUpdateResult.None;
        }
        else if (Charge < 1)
        {
            Charge += rechargeRate * dt;
            if (Charge > 1)
            {
                Charge = 1;
            }
            if (cooldown && Charge > rechargeThreshold)
                //use > rechargeThreshold so that we can set rechargeThreshold = 0;(if we end up doing that tho, we can get rid of cooldown)
                //but I think we want a (small) positive threshold, otherwise you basically never run out of charge (well when at zero charge you alternate on and off every update)
            {
                cooldown = false;
                return ThrustersUpdateResult.CooldownEnded;
            }
            return ThrustersUpdateResult.None;
        }

        return ThrustersUpdateResult.None;
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