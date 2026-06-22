using Unity.U2D.Physics;
using UnityEngine;
using UnityEngine.VFX;

public class PowerBeam : MonoBehaviour
{
    public  VisualEffect visualEffect;
    public PhysicsQuery.QueryFilter queryFilter;
    public PhysicsMask spiderMask;
    public float maxLength;
    public float growthSpeed;
    public float force;
    public float damagePerSecond;

    [System.NonSerialized] public float length;
    [System.NonSerialized] public float goalLength;
    readonly int lengthProperty = Shader.PropertyToID("Length");

    void Start()
    {
        length = 0;
        goalLength = maxLength;
        UpdateVFXLength();
    }

    void Update()
    {
        if (length != goalLength)
        {
            if (goalLength < length)
            {
                length = goalLength;
            }
            else
            {
                length = MathTools.LerpAtConstantSpeed(length, goalLength, growthSpeed, Time.deltaTime);
            }
            
            UpdateVFXLength();
        }
    }

    private void FixedUpdate()
    {
        Vector2 o = transform.position;
        Vector2 r = transform.right;
        var d = maxLength * r;
        var cast = PhysicsWorld.defaultWorld.CastRay(o, d, queryFilter);

        if (cast.Length > 0)
        {
            var result = cast[0];
            goalLength = result.fraction * maxLength;
            
            if ((result.shape.contactFilter.categories & spiderMask) != 0)
            {
                var player = Spider.Player;
                var spideyBody = player.mover.SpideyBody;
                result.shape.body.ApplyForce(spideyBody.TotalMass * force * r, result.point);
                player.health.AddHealth(-damagePerSecond * Time.deltaTime);
            }
        }
        else
        {
            goalLength = maxLength;
        }
    }

    private void UpdateVFXLength()
    {
        visualEffect.SetFloat(lengthProperty, length);
    }
}
