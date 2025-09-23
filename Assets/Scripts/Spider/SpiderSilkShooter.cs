using UnityEngine;
using UnityEngine.Experimental.Rendering;

public class SpiderSilkShooter : MonoBehaviour
{
    [SerializeField] float silkDrag;
    [SerializeField] float silkBounciness;
    [SerializeField] float silkCollisionRadius;
    [Range(0,1)][SerializeField] float silkCollisionThresholdFraction;
    [SerializeField] float silkWidth;
    [SerializeField] float silkNodeSpacing;
    [SerializeField] int silkNumNodes;
    //[SerializeField] int silkCollisionIterations;
    [SerializeField] int silkConstraintIterations;

    Rope rope;
    bool mouseDown;

    private void Update()
    {
        mouseDown = Input.GetKey(KeyCode.Mouse0);
        if (rope != null)
        {
            DebugDrawRope();
        }
    }

    private void FixedUpdate()
    {
        if (mouseDown)
        {
            var mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mousePos.z = 0;

            if (rope == null)
            {
                rope = new Rope(mousePos, silkWidth, silkNodeSpacing, silkNumNodes, silkDrag, 
                    silkCollisionRadius, silkCollisionThresholdFraction, silkBounciness, /*silkCollisionIterations,*/ silkConstraintIterations);
                rope.nodes[0].Anchor();
            }
            else
            {
                rope.nodes[0].position = mousePos;
                rope.FixedUpate(Time.deltaTime);
            }
        }
        else if (rope != null)
        {
            rope = null;
        }
    }

    private void DebugDrawRope()
    {
        for (int i = 1; i < rope.nodes.Length; i++)
        {
            Debug.DrawLine(rope.nodes[i - 1].position, rope.nodes[i].position, Color.red);
        }
    }
}