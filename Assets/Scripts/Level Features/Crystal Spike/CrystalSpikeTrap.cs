using Unity.U2D.Physics;
using UnityEngine;

public class CrystalSpikeTrap : MonoBehaviour, PhysicsCallbacks.ITriggerCallback
{
    [SerializeField] CrystalSpike[] spike;
    [SerializeField] PhysicsBodyKit physicsKit;
    [SerializeField] float impulseForce;
    [SerializeField] float bounciness;
    [SerializeField] float damagePerSpike;

    void Start()
    {
        if (!physicsKit.body.isValid)
        {
            physicsKit.CreateBody();
        }

        var shape = physicsKit.body.GetShapes()[0];
        shape.callbackTarget = this;
    }

    //will only be triggered by the player (for now player is the only thing on player layer)
    public void OnTriggerBegin2D(PhysicsEvents.TriggerBeginEvent beginEvent)
    {
        Attack();
    }

    public void OnTriggerEnd2D(PhysicsEvents.TriggerEndEvent endEvent)
    {
        
    }

    private void Attack()
    {
        int count = 0;
        for (int i = 0; i < spike.Length; i++)
        {
            if (spike[i].Attack())
            {
                count++;//for damage and to make sure we only apply force once
            }
        }

        if (count == 0)
        {
            return;
        }

        //apply impulse
        var spider = Spider.Player.mover.Abdomen;
        var mass = Spider.Player.mover.TotalMass;
        var n = (spider.position - physicsKit.body.position).normalized;
        if (n == Vector2.zero)
        {
            n = Vector2.up;
        }
        spider.ApplyLinearImpulseToCenter(mass * (impulseForce * n - bounciness * spider.linearVelocity));

        //apply damage
        Spider.Player.health.AddHealth(-damagePerSpike * count);
    }
}