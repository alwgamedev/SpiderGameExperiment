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
    //[SerializeField] float nodeSpacing;
    [SerializeField] int numNodes;
    [SerializeField] float minLength;
    [SerializeField] float maxLength;
    [SerializeField] float baseShootSpeed;
    [SerializeField] float shootSpeedPowerUpRate;
    [SerializeField] float shootSpeedPowerUpMax;
    [SerializeField] float grappleMass;
    [SerializeField] float releaseRate;
    [SerializeField] float aimRotationMax;
    [SerializeField] float aimRotationMin;
    [SerializeField] float aimRotationSpeed;
    //[SerializeField] int timeStepsPerRelease;
    //[SerializeField] int nodesPerRelease;
    //[SerializeField] int nodesPerReleaseAnchored;
    //[SerializeField] int anchorIndexMaxOffset;//counted backwards (so min # nodes past anchor is this number - 1)
    [SerializeField] int constraintIterations;
    //[SerializeField] int tensionSampleSize;
    [SerializeField] float carrySpringForce;
    [SerializeField] float carrySpringDamping;

    int grappleReleaseInput;//1 = release, -1 = retract, 0 = none
    //float grappleLength;
    //int releaseTimer;
    ////float shootSpeed;
    //int anchorIndex;
    //int terminusIndex;
    bool poweringUp;
    float shootSpeedPowerUp;

    int aimInput;
    float aimRotation0;//inefficient (should just add aimRotation0 to max & min values) but for now allows us to tweak max and min live
    float aimRotation;

    Rope grapple;
    LineRenderer lineRenderer;

    float fixedDt;
    float fixedDt2;

    public bool GrappleAnchored => grapple != null /*&& anchorIndex != grapple.lastIndex*/ && grapple.nodes[grapple.lastIndex].Anchored;
    public int GrappleReleaseInput => grappleReleaseInput;
    //int NodesPerRelease => GrappleAnchored ? nodesPerReleaseAnchored : nodesPerRelease;
    //float ShootSpeed => nodeSpacing * nodesPerRelease / (timeStepsPerRelease * fixedDt);
    //int AnchorIndexMax => grapple.nodes.Length - anchorIndexMaxOffset;
    float ShootSpeed => shootSpeedPowerUp * baseShootSpeed;
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

    //2do: length should gradually increase in the first period after shooting
    //will grow in length until grapple anchors or we reach maxLength
    //we just need to figure out what rate it shoot grow at
    
    private void Update()
    {
        aimInput = (Input.GetKey(KeyCode.A) ? 1 : 0) + (Input.GetKey(KeyCode.D) ? -1 : 0);

        if (grapple == null)
        {
            if (poweringUp && shootSpeedPowerUp < shootSpeedPowerUpMax)
            {
                shootSpeedPowerUp += shootSpeedPowerUpRate * Time.deltaTime;
                if (shootSpeedPowerUp > shootSpeedPowerUpMax)
                {
                    shootSpeedPowerUp = shootSpeedPowerUpMax;
                    //but we keep poweringUp = true, so grapple doesn't shoot until you release W
                }
            }
            poweringUp = Input.GetKey(KeyCode.W);
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.Z))
            {
                DestroyGrapple();
            }
            else if (GrappleAnchored)
            {
                grappleReleaseInput = (Input.GetKey(KeyCode.W) && grapple.Length < maxLength ? 1 : 0) + (Input.GetKey(KeyCode.S) && grapple.Length > minLength ? -1 : 0);
            }
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
            UpdateGrappleLength();
            UpdateAnchorPosition();
            grapple.FixedUpate(fixedDt, fixedDt2);
            if (GrappleAnchored)
            {
                UpdateCarrySpring();
            }

        }
        else
        {
            if (!poweringUp && shootSpeedPowerUp > 0)
            {
                ShootGrapple();
                //shootSpeedPowerUp = 0;
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

    private void UpdateAnchorPosition()
    {
        grapple.nodes[0].position = AnchorPosition;
    }

    //private void PositionAnchoredPoints()
    //{
    //    var p = AnchorPosition;
    //    for (int i = 0; i < anchorIndex + 1; i++)
    //    {
    //        grapple.nodes[i].position = p;
    //    }
    //}

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

        var t = NetTension();
        if (t > 0)
        {
            t /= grapple.lastIndex;//average tension
            var d = (grapple.nodes[grapple.lastIndex].position - grapple.nodes[0].position).normalized;
            var v = Vector2.Dot(shooterRb.linearVelocity, d);
            shooterRb.AddForceAtPosition(shooterRb.mass * (carrySpringForce * t - carrySpringDamping * v) * d, barrel.position);
        }

    }

    //2do: should probably only sample the first few nodes near anchor(and average, so that changing how many are sampled won't throw things off)
    private float NetTension()
    {
        float total = 0;
        //int m = Mathf.Min(grapple.nodes.Length, anchorIndex + tensionSampleSize + 1);
        for (int i = 1; i < grapple.lastIndex; i++)
        {
            total += (grapple.nodes[i].position - grapple.nodes[i - 1].position).magnitude - grapple.nodeSpacing;
        }

        return total;
    }

    private void UpdateGrappleLength()
    {
        if (GrappleAnchored)
        {
            if (grappleReleaseInput != 0)
            {
                AddGrappleLength(grappleReleaseInput * releaseRate * fixedDt);
            }
        }
        else if (grapple.Length < maxLength && NetTension() > 0)
        {
            AddGrappleLength(ShootSpeed * fixedDt);
        }
    }

    private void AddGrappleLength(float l)
    {
        grapple.Length = Mathf.Clamp(grapple.Length + l, minLength, maxLength);
    }

    private void ShootGrapple()
    {
        SpawnGrapple();
        grapple.nodes[0].Anchor();
        var lastIndex = grapple.lastIndex;
        grapple.nodes[lastIndex].mass = grappleMass;
        Vector2 shootVelocity = ShootSpeed * barrel.up;
        grapple.nodes[lastIndex].lastPosition -= fixedDt * shootVelocity;
    }

    //private void UpdateGrow()
    //{
    //    releaseTimer += grappleReleaseInput;
    //    var anchorIndexMax = AnchorIndexMax;

    //    if (releaseTimer == timeStepsPerRelease && anchorIndex > 0)
    //    {
    //        int n = NodesPerRelease;
    //        Vector2 d = fixedDt * ShootSpeed * barrel.up;

    //        int i = n;
    //        while (i > 0 && anchorIndex > 0)
    //        {
    //            i--;
    //            grapple.nodes[anchorIndex].position += (i * timeStepsPerRelease / n) * d;
    //            grapple.nodes[anchorIndex].DeAnchor(d);
    //            anchorIndex--;
    //        }

    //        releaseTimer = 0;
    //    }
    //    else if (releaseTimer == -timeStepsPerRelease && anchorIndex < anchorIndexMax)
    //    {
    //        int i = NodesPerRelease;
    //        Vector2 lastPos = grapple.nodes[anchorIndex].position;
    //        while (i > 0 && anchorIndex < anchorIndexMax)
    //        {
    //            var a = anchorIndex + 1;
    //            lastPos = grapple.nodes[a].position;
    //            grapple.nodes[a].position = grapple.nodes[anchorIndex].position;
    //            grapple.nodes[a].Anchor();
    //            anchorIndex++;
    //            i--;
    //        }

    //        var offset = grapple.nodes[anchorIndex].position - lastPos;
    //        offset = 1 / (grapple.lastIndex - anchorIndex) * offset;
    //        for (int j = anchorIndex + 1; j < grapple.nodes.Length; j++)
    //        {
    //            grapple.nodes[j].position += offset;
    //        }

    //        releaseTimer = 0;
    //    }

    //    //was there any point in this? (other than maybe "performance" in a not very important scenario)
    //    //var nextA = anchorIndex - releaseTimer + grappleReleaseInput
    //    //if (nextA < 0 || nextA > anchorIndexMax)
    //    //{
    //    //    grappleReleaseInput = 0;
    //    //    releaseTimer = 0;
    //    //}
    //}

    //SPAWNING

    private void SpawnGrapple()
    {
        var nodeSpacing = minLength / (numNodes - 1);
        grapple = new Rope(source.position, width, nodeSpacing, numNodes, drag,
                    collisionMask, bounciness, grappleAnchorMask, constraintIterations);
        //anchorIndex = grapple.lastIndex;
        //for (int i = 0; i < numNodes; i++)
        //{
        //    grapple.nodes[i].Anchor();
        //}

        //shootSpeed = nodeSpacing * nodesPerRelease / (timeStepsPerRelease * fixedDt);
    }

    private void DestroyGrapple()
    {
        grapple = null;
        grappleReleaseInput = 0;
        shootSpeedPowerUp = 0;
        //anchorIndex = 0;
        //releaseTimer = 0;
    }
}