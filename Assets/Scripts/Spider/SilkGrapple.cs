using UnityEngine;

public class SilkGrapple : MonoBehaviour
{
    [SerializeField] Transform source;
    [SerializeField] float drag;
    [SerializeField] float bounciness;
    [SerializeField] LayerMask collisionMask;
    [SerializeField] float width;
    [SerializeField] int numNodes;
    [SerializeField] int constraintIterations;
    [SerializeField] float initialLength;
    [SerializeField] float maxLength;
    [SerializeField] float growthRate;
    [SerializeField] float shootSpeed;
    [SerializeField] float leadMass;

    bool growing;
    float currentLength;

    Rope grapple;
    LineRenderer lineRenderer;

    float fixedDt;
    float fixedDt2;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
    }

    private void Start()
    {
        fixedDt = Time.fixedDeltaTime;
        fixedDt2 = fixedDt * fixedDt;
    }

    private void Update()
    {
        if (grapple == null)
        {
            if (Input.GetKeyDown(KeyCode.F))
            {
                SpawnGrapple();
            }
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.G))
            {
                DestroyGrapple();
                return;
            }
            if (growing)
            {
                UpdateGrow();
            }
        }
    }

    private void LateUpdate()
    {
        UpdateLineRenderer();
    }

    private void FixedUpdate()
    {
        if (grapple != null)
        {
            grapple.nodes[0].position = source.position;
            grapple.FixedUpate(fixedDt, fixedDt2);
        }
    }

    //RENDERING

    private void UpdateLineRenderer()
    {
        if (grapple != null)
        {
            if (!lineRenderer.enabled)
            {
                EnableLineRenderer();
            }
            grapple.SetLineRendererPositions(lineRenderer);
        }
        else if (lineRenderer.enabled)
        {
            lineRenderer.enabled = false;
        }
    }

    private void EnableLineRenderer()
    {
        lineRenderer.enabled = true;
        if (lineRenderer.positionCount != numNodes)
        {
            lineRenderer.positionCount = numNodes;
        }
        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width;
    }

    //GROW

    private void UpdateGrow()
    {
        if (Input.GetKeyUp(KeyCode.F))
        {
            growing = false;
        }
        else
        {
            var lengthIncrease = growthRate * maxLength * Time.deltaTime;
            currentLength += lengthIncrease;
            //we'll just see how this goes but in future you may also want to provide a boost to existing nodes (based on length increase)
            //we also consider the approach of releasing dormant nodes with an initial velocity

            if (currentLength > maxLength)
            {
                currentLength = maxLength;
                growing = false;
            }

            grapple.nodeSpacing = currentLength / (grapple.nodes.Length - 1);
            grapple.SpacingConstraintIteration();
            //grapple.GrowRopeFromBase(lengthIncrease);

        }
    }

    //SPAWNING

    private void SpawnGrapple()
    {
        currentLength = initialLength;
        grapple = new Rope(source.position, shootSpeed * source.up, width, initialLength / numNodes, numNodes, drag,
                    collisionMask, bounciness, constraintIterations);
        grapple.nodes[0].Anchor();
        grapple.nodes[^1].mass = leadMass;
        growing = true;
        Debug.Log("shooting grapple!");
    }

    private void DestroyGrapple()
    {
        grapple = null;
        growing = false;
        currentLength = 0;
        Debug.Log("destroyed grapple");
    }
}