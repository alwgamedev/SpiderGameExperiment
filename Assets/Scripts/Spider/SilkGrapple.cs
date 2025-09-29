using UnityEngine;

public class SilkGrapple : MonoBehaviour
{
    [SerializeField] Transform source;
    [SerializeField] float drag;
    [SerializeField] float bounciness;
    [SerializeField] LayerMask collisionMask;
    [SerializeField] float width;
    [SerializeField] float nodeSpacing;
    [SerializeField] int numNodes;
    [SerializeField] int initialAnchorIndex;
    [SerializeField] int timeStepsPerRelease;
    [SerializeField] int nodesPerRelease;
    [SerializeField] int nodesPerRetract;
    [SerializeField] int constraintIterations;

    int grappleReleaseInput;//1 = release, -1 = retract, 0 = none
    int releaseTimer;
    //float growInterval;
    float shootSpeed;
    int anchorIndex;

    Rope grapple;
    LineRenderer lineRenderer;

    float fixedDt;
    float fixedDt2;

    //float GrowInterval => growIntervalMultiplier * nodeSpacing / shootSpeed;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>(); 
        fixedDt = Time.fixedDeltaTime;
        fixedDt2 = fixedDt * fixedDt;
    }

    private void Start()
    {
        lineRenderer.enabled = false;
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
            if (Input.GetKeyDown(KeyCode.Z))
            {
                DestroyGrapple();
                return;
            }

            grappleReleaseInput = Input.GetKey(KeyCode.F) && anchorIndex > 0 ? 1 : (Input.GetKey(KeyCode.G) && anchorIndex < grapple.nodes.Length - 1 ? -1 : 0);
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
            if (grappleReleaseInput != 0)
            {
                UpdateGrow();
            }
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
        releaseTimer += grappleReleaseInput;
        var n = grapple.nodes.Length - 1;

        if (releaseTimer == timeStepsPerRelease && anchorIndex > 0)
        {
            Vector2 d = fixedDt * shootSpeed * source.up;
            int i = nodesPerRelease;
            while (i > 0 && anchorIndex > 0)
            {
                i--;
                grapple.nodes[anchorIndex].position += (i / nodesPerRelease) * d;
                grapple.nodes[anchorIndex].DeAnchor(d);
                anchorIndex--;
            }
            
            releaseTimer -= timeStepsPerRelease;
        }
        else if (releaseTimer == -timeStepsPerRelease && anchorIndex < n)
        {
            int i = nodesPerRetract;
            while (i > 0 && anchorIndex < n)
            {
                var a = anchorIndex + 1;
                grapple.nodes[a].position = grapple.nodes[anchorIndex].position;
                grapple.nodes[a].Anchor();
                anchorIndex++;
                i--;
            }

            releaseTimer += timeStepsPerRelease;
        }

        var nextA = anchorIndex + grappleReleaseInput;
        if (nextA < 0 || nextA > n)
        {
            grappleReleaseInput = 0;
            releaseTimer = 0;
        }
    }

    //SPAWNING

    private void SpawnGrapple()
    {
        grapple = new Rope(source.position, shootSpeed * source.up, width, nodeSpacing, numNodes, drag,
                    collisionMask, bounciness, constraintIterations);
        anchorIndex = initialAnchorIndex;
        for (int i = 0; i < anchorIndex + 1; i++)
        {
            grapple.nodes[i].Anchor();
        }

        //var g = -Physics2D.gravity.y;
        //growInterval = (-shootSpeed + Mathf.Sqrt(shootSpeed * shootSpeed + 2 * g * nodeSpacing)) / g;
        //^this is a lower estimate of how long it takes first active node to travel nodeSpacing distance from anchor
        //so we make sure we release next node before constraints kick in
        //growInterval = timeStepsPerRelease * fixedDt;
        shootSpeed = nodeSpacing * nodesPerRelease / (timeStepsPerRelease * fixedDt);
        //Debug.Log(shootSpeed);
    }

    private void DestroyGrapple()
    {
        grapple = null;
        grappleReleaseInput = 0;
        anchorIndex = 0;
        releaseTimer = 0;
    }
}