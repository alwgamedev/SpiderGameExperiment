using UnityEngine;
using Unity.U2D.Physics;

public class JointedChainTest : MonoBehaviour
{
    [SerializeField] JointedChain jointedChain;
    [SerializeField] Transform target;
    [SerializeField] float reachForce;
    [SerializeField] bool pullUniformly;

    private void OnValidate()
    {
        jointedChain.OnValidate();
    }

    private void Start()
    {
        jointedChain.Initialize(PhysicsWorld.defaultWorld);
    }

    private void FixedUpdate()
    {
        var a = reachForce * ((Vector2)target.position - jointedChain.EffectorPosition);
        if (pullUniformly)
        {
            jointedChain.PullUniformly(a);
        }
        else
        {
            jointedChain.PullEffector(a);
        }
    }
}