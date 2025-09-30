using UnityEngine;

public class SilkGrapple : MonoBehaviour
{
    [SerializeField] Transform source;//shoot from here
    [SerializeField] Transform barrel;//rotate this
    [SerializeField] Rigidbody2D shooterRb;
    [SerializeField] float drag;
    [SerializeField] float bounciness;
    [SerializeField] LayerMask terminusAnchorMask;
    [SerializeField] LayerMask collisionMask;
    [SerializeField] float width;
    [SerializeField] float nodeSpacing;
    [SerializeField] int numNodes;
    [SerializeField] float aimRotationMax;
    [SerializeField] float aimRotationMin;
    [SerializeField] float aimRotationSpeed;
    [SerializeField] int timeStepsPerRelease;
    [SerializeField] int nodesPerRelease;
    [SerializeField] int nodesPerRetract;
    [SerializeField] int constraintIterations;

    int grappleReleaseInput;//1 = release, -1 = retract, 0 = none
    int releaseTimer;
    float shootSpeed;
    int anchorIndex;

    int aimInput;
    float aimRotation0;//inefficient (should just add aimRotation0 to max & min values) but for now allows us to tweak max and min live
    float aimRotation;

    Rope grapple;
    LineRenderer lineRenderer;

    float fixedDt;
    float fixedDt2;

    int AnchorIndexMax => grapple.nodes[^1].Anchored ? grapple.nodes.Length - 2 : grapple.nodes.Length - 1;
    Vector2 AnchorPosition => source.position;

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
        aimRotation0 = barrel.rotation.eulerAngles.z * Mathf.Deg2Rad;
    }

    private void Update()
    {
        aimInput = (Input.GetKey(KeyCode.A) ? 1 : 0) + (Input.GetKey(KeyCode.D) ? -1 : 0);

        if (grapple == null)
        {
            if (Input.GetKeyDown(KeyCode.W))
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

            grappleReleaseInput = (Input.GetKey(KeyCode.W) && anchorIndex > 0 ? 1 : 0) + (Input.GetKey(KeyCode.S) && anchorIndex < AnchorIndexMax ? -1 : 0);
        }
    }

    private void LateUpdate()
    {
        UpdateLineRenderer();
    }

    private void FixedUpdate()
    {
        if (aimInput != 0)
        {
            UpdateAim();
        }
        if (grapple != null)
        {
            if (grappleReleaseInput != 0)
            {
                UpdateGrow();
            }
            PositionAnchoredPoints();
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

    //FIXED UPDATE FUNCTIONS

    private void UpdateAim()
    {
        aimRotation += aimInput * aimRotationSpeed * fixedDt;
        aimRotation = Mathf.Clamp(aimRotation, aimRotationMin, aimRotationMax);
        var a = aimRotation0 + aimRotation;
        barrel.right = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
    }

    private void PositionAnchoredPoints()
    {
        var p = AnchorPosition;
        for (int i = 0; i < anchorIndex + 1; i++)
        {
            grapple.nodes[i].position = p;
        }
    }

    private void UpdateGrow()
    {
        releaseTimer += grappleReleaseInput;
        var anchorIndexMax = AnchorIndexMax;

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
            
            releaseTimer = 0;
        }
        else if (releaseTimer == -timeStepsPerRelease && anchorIndex < anchorIndexMax)
        {
            int i = nodesPerRetract;
            while (i > 0 && anchorIndex < anchorIndexMax)
            {
                var a = anchorIndex + 1;
                grapple.nodes[a].position = grapple.nodes[anchorIndex].position;
                grapple.nodes[a].Anchor();
                anchorIndex++;
                i--;
            }

            releaseTimer = 0;
        }

        var nextA = anchorIndex + grappleReleaseInput;
        if (nextA < 0 || nextA > anchorIndexMax)
        {
            grappleReleaseInput = 0;
            releaseTimer = 0;
        }
    }

    //SPAWNING

    private void SpawnGrapple()
    {
        grapple = new Rope(source.position, width, nodeSpacing, numNodes, drag,
                    collisionMask, bounciness, terminusAnchorMask, constraintIterations);
        anchorIndex = numNodes - 1;
        for (int i = 0; i < numNodes; i++)
        {
            grapple.nodes[i].Anchor();
        }

        shootSpeed = nodeSpacing * nodesPerRelease / (timeStepsPerRelease * fixedDt);
    }

    private void DestroyGrapple()
    {
        grapple = null;
        grappleReleaseInput = 0;
        anchorIndex = 0;
        releaseTimer = 0;
    }
}