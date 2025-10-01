using UnityEngine;

public class SilkGrapple : MonoBehaviour
{
    [SerializeField] Transform source;//shoot from here
    [SerializeField] Transform barrel;//rotate this
    [SerializeField] Rigidbody2D shooterRb;
    [SerializeField] float drag;
    [SerializeField] float bounciness;
    [SerializeField] LayerMask grappleAnchorMask;
    [SerializeField] LayerMask collisionMask;
    [SerializeField] float width;
    [SerializeField] float nodeSpacing;
    [SerializeField] int numNodes;
    [SerializeField] float aimRotationMax;
    [SerializeField] float aimRotationMin;
    [SerializeField] float aimRotationSpeed;
    [SerializeField] int timeStepsPerRelease;
    [SerializeField] int nodesPerRelease;
    [SerializeField] int nodesPerReleaseAnchored;
    [SerializeField] int anchorIndexMaxOffset;//counted backwards (so min # nodes past anchor is this number - 1)
    [SerializeField] int constraintIterations;
    //[SerializeField] int tensionSampleSize;
    [SerializeField] float carrySpringForce;
    [SerializeField] float carrySpringDamping;

    int grappleReleaseInput;//1 = release, -1 = retract, 0 = none
    int releaseTimer;
    //float shootSpeed;
    int anchorIndex;
    //int terminusIndex;

    int aimInput;
    float aimRotation0;//inefficient (should just add aimRotation0 to max & min values) but for now allows us to tweak max and min live
    float aimRotation;

    Rope grapple;
    LineRenderer lineRenderer;

    float fixedDt;
    float fixedDt2;

    public bool GrappleAnchored => grapple != null && anchorIndex != grapple.lastIndex && grapple.nodes[grapple.lastIndex].Anchored;
    public int GrappleReleaseInput => grappleReleaseInput;
    int NodesPerRelease => GrappleAnchored ? nodesPerReleaseAnchored : nodesPerRelease;
    float ShootSpeed => nodeSpacing * nodesPerRelease / (timeStepsPerRelease * fixedDt);
    int AnchorIndexMax => grapple.nodes.Length - anchorIndexMaxOffset;
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
            if (GrappleAnchored)
            {
                UpdateCarrySpring();
            }

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

    //fIXED UPDATE FUNCTIONS

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

    private void UpdateCarrySpring()
    {
        //var l0 = grapple.nodeSpacing * (grapple.lastIndex - anchorIndex);
        //var d = grapple.nodes[grapple.lastIndex].position - grapple.nodes[anchorIndex].position;
        //var l = d.magnitude;
        //var error = d.magnitude - l0;
        //if (error > 0)
        //{
        //    d /= l;
        //    var v = Vector2.Dot(shooterRb.linearVelocity, d);
        //    shooterRb.AddForceAtPosition(shooterRb.mass * (carrySpringForce * error - carrySpringDamping * v) * d, barrel.position);
        //}

        var t = CarryTension();
        if (t > 0)
        {
            var d = (grapple.nodes[grapple.lastIndex].position - grapple.nodes[anchorIndex].position).normalized;
            var v = Vector2.Dot(shooterRb.linearVelocity, d);
            shooterRb.AddForceAtPosition(shooterRb.mass * (carrySpringForce * t - carrySpringDamping * v) * d, barrel.position);
        }

    }

    //2do: should probably only sample the first few nodes near anchor(and average, so that changing how many are sampled won't throw things off)
    private float CarryTension()
    {
        float total = 0;
        int j = 0;
        //int m = Mathf.Min(grapple.nodes.Length, anchorIndex + tensionSampleSize + 1);
        for (int i = anchorIndex + 1; i < grapple.lastIndex; i++)
        {
            total += (grapple.nodes[i].position - grapple.nodes[i - 1].position).magnitude - nodeSpacing;
            j++;
        }

        return total / j;
    }

    private void UpdateGrow()
    {
        releaseTimer += grappleReleaseInput;
        var anchorIndexMax = AnchorIndexMax;

        if (releaseTimer == timeStepsPerRelease && anchorIndex > 0)
        {
            int n = NodesPerRelease;
            Vector2 d = fixedDt * ShootSpeed * barrel.up;

            int i = n;
            while (i > 0 && anchorIndex > 0)
            {
                i--;
                grapple.nodes[anchorIndex].position += (i * timeStepsPerRelease / n) * d;
                grapple.nodes[anchorIndex].DeAnchor(d);
                anchorIndex--;
            }

            releaseTimer = 0;
        }
        else if (releaseTimer == -timeStepsPerRelease && anchorIndex < anchorIndexMax)
        {
            int i = NodesPerRelease;
            Vector2 lastPos = grapple.nodes[anchorIndex].position;
            while (i > 0 && anchorIndex < anchorIndexMax)
            {
                var a = anchorIndex + 1;
                lastPos = grapple.nodes[a].position;
                grapple.nodes[a].position = grapple.nodes[anchorIndex].position;
                grapple.nodes[a].Anchor();
                anchorIndex++;
                i--;
            }

            var offset = grapple.nodes[anchorIndex].position - lastPos;
            offset = 1 / (grapple.lastIndex - anchorIndex) * offset;
            for (int j = anchorIndex + 1; j < grapple.nodes.Length; j++)
            {
                grapple.nodes[j].position += offset;
            }

            releaseTimer = 0;
        }

        //was there any point in this? (other than maybe "performance" in a not very important scenario)
        //var nextA = anchorIndex - releaseTimer + grappleReleaseInput
        //if (nextA < 0 || nextA > anchorIndexMax)
        //{
        //    grappleReleaseInput = 0;
        //    releaseTimer = 0;
        //}
    }

    //SPAWNING

    private void SpawnGrapple()
    {
        grapple = new Rope(source.position, width, nodeSpacing, numNodes, drag,
                    collisionMask, bounciness, grappleAnchorMask, constraintIterations);
        anchorIndex = grapple.lastIndex;
        for (int i = 0; i < numNodes; i++)
        {
            grapple.nodes[i].Anchor();
        }

        //shootSpeed = nodeSpacing * nodesPerRelease / (timeStepsPerRelease * fixedDt);
    }

    private void DestroyGrapple()
    {
        grapple = null;
        grappleReleaseInput = 0;
        anchorIndex = 0;
        releaseTimer = 0;
    }
}