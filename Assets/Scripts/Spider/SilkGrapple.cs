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
    [SerializeField] int timeStepsPerGrow;
    //[SerializeField] int shootSpeedInt;
    //[SerializeField] float leadMass;
    [SerializeField] int constraintIterations;

    bool growing;
    float growTimer;
    float growInterval;
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
            if (Input.GetKeyDown(KeyCode.G))
            {
                DestroyGrapple();
                return;
            }

            if (Input.GetKeyDown(KeyCode.F) && anchorIndex > 0)
            {
                growing = true;
            }
            else if (growing && Input.GetKeyUp(KeyCode.F))
            {
                growing = false;
                grapple.nodeSpacing = nodeSpacing;
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
            if (growing)
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
        //var growInterval = GrowInterval;
        growTimer += Time.deltaTime;

        if (growTimer > growInterval && anchorIndex > 0)
        {
            Vector2 d = fixedDt * shootSpeed * source.up;
            //var c = fixedDt * d;
            while (growTimer > growInterval && anchorIndex > 0)
            {
                //for (int j = anchorIndex + 1; j < grapple.nodes.Length; j++)
                //{
                //    grapple.nodes[j].lastPosition -= c;
                //}
                grapple.nodes[anchorIndex].DeAnchor(d);
                anchorIndex--;
                growTimer -= growInterval;
            }
        }

        if (anchorIndex == 0)
        {
            growing = false;
            growTimer = 0;
            grapple.nodeSpacing = nodeSpacing;
        }
        //else
        //{
        //    var extra = (growTimer / growInterval) * nodeSpacing / (grapple.nodes.Length - anchorIndex - 1);
        //    grapple.nodeSpacing = nodeSpacing + extra;
        //}
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
        growInterval = timeStepsPerGrow * fixedDt;
        shootSpeed = nodeSpacing / growInterval;
        //Debug.Log(shootSpeed);
        growing = true;
        growTimer = 0;
    }

    private void DestroyGrapple()
    {
        grapple = null;
        growing = false;
        anchorIndex = 0;
        growTimer = 0;
    }
}