using System;
using UnityEngine;
using Unity.U2D.Physics;


[Serializable]
public class Thruster
{
    [SerializeField] float secondsToDrain;
    [SerializeField] float secondsToRecharge;
    [SerializeField] float rechargeThreshold;
    [SerializeField] float gravityReduction;

    float drainPerUpdate;
    float rechargePerUpdate;
   
    public bool Cooldown { get; private set; }//cooldown kicks in when you drain charge all the way to 0, then you can't engage again until back above rechargeThreshold
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
        drainPerUpdate = (1 / secondsToDrain) * Time.fixedDeltaTime;
        rechargePerUpdate = (1 / secondsToRecharge) * Time.fixedDeltaTime;
    }

    public ThrustersUpdateResult FixedUpdate(ref PhysicsBody pb)
    {
        if (Engaged)
        {
            Charge -= drainPerUpdate;
            if (!(pb.linearVelocity.y > 0))
            {
                pb.ApplyForceToCenter(-gravityReduction * pb.mass * pb.world.gravity);
            }
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
            Charge += rechargePerUpdate;
            if (Charge > 1)
            {
                Charge = 1;
            }
            if (Cooldown && Charge > rechargeThreshold)
            {
                Cooldown = false;
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
        Engaged = !Cooldown;
        return Engaged;
    }

    public bool Disengage()
    {
        if (!Engaged)
        {
            return false;
        }

        Engaged = false;
        Cooldown = Charge == 0;
        return true;
    }
}