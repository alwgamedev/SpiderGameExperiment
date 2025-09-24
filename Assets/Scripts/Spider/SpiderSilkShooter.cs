using UnityEngine;
using UnityEngine.Experimental.Rendering;

public class SpiderSilkShooter : MonoBehaviour
{
    [SerializeField] float silkDrag;
    [SerializeField] float silkBounciness;
    [SerializeField] float silkCollisionRadius;
    //[SerializeField] float silkCollisionThreshold;
    [SerializeField] float silkWidth;
    [SerializeField] float silkNodeSpacing;
    [SerializeField] int silkNumNodes;
    //[SerializeField] int silkCollisionIterations;
    [SerializeField] int silkConstraintIterations;

    Rope rope;
    bool mouseDown;
    LineRenderer lineRenderer;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
    }

    private void Update()
    {
        mouseDown = Input.GetKey(KeyCode.Mouse0);
        //if (rope != null)
        //{
        //    DebugDrawRope();
        //}
    }

    private void FixedUpdate()
    {
        if (mouseDown)
        {
            var mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mousePos.z = 0;

            if (rope == null)
            {
                if (lineRenderer.positionCount != silkNumNodes)
                {
                    lineRenderer.positionCount = silkNumNodes;
                }
                lineRenderer.startWidth = silkWidth;
                lineRenderer.endWidth = silkWidth;
                rope = new Rope(mousePos, silkWidth, silkNodeSpacing, silkNumNodes, silkDrag, 
                    silkCollisionRadius, /*silkCollisionThresholdFraction,*/ silkBounciness, /*silkCollisionIterations,*/ silkConstraintIterations);
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

    private void LateUpdate()
    {
        rope?.Render(lineRenderer);
    }

    private void DebugDrawRope()
    {
        for (int i = 1; i < rope.nodes.Length; i++)
        {
            Debug.DrawLine(rope.nodes[i - 1].position, rope.nodes[i].position, Color.red);
        }
    }
}