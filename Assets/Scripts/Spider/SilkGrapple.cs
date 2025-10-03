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
    bool poweringUp;
    float shootSpeedPowerUp;

    int aimInput;
    float aimRotation0;//inefficient (should just add aimRotation0 to max & min values) but for now allows us to tweak max and min live
    float aimRotation;

    Rope grapple;
    LineRenderer lineRenderer;

    float fixedDt;
    float fixedDt2;

    public bool GrappleAnchored => grapple != null && grapple.nodes[grapple.lastIndex].Anchored;
    public int GrappleReleaseInput => grapple == null ? 0 : grappleReleaseInput;
    //public bool JustStoppedPullingRb { get; private set; }
    //public float LastTension { get; private set; }
    public bool PullingRb { get; private set; }
    public bool StronglyPullingRb { get; private set; }
    public Vector2 GrappleExtent => GrapplePosition - AnchorPosition;
    public Vector2 GrapplePosition => grapple.nodes[grapple.lastIndex].position;
    public bool SourceIsBelowGrapple => GrapplePosition.y > source.position.y;
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
                //bool neg = grappleReleaseInput < 0;
                grappleReleaseInput = (Input.GetKey(KeyCode.W) && grapple.Length < maxLength ? 1 : 0) 
                    + (Input.GetKey(KeyCode.S) && grapple.Length > minLength ? -1 : 0);
                //JustStoppedPullingRb = neg && !(grappleReleaseInput < 0);
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
            UpdateAnchorPosition();
            grapple.FixedUpate(fixedDt, fixedDt2);
            var t = Tension();
            UpdateGrappleLength(t);
            if (GrappleAnchored)
            {
                UpdateCarrySpring(t);
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

    private void UpdateCarrySpring(float tension)
    {
        PullingRb = tension > 0;
        StronglyPullingRb = PullingRb && Vector2.Dot(shooterRb.linearVelocity, GrappleExtent) > 0;
        if (PullingRb)
        {
            
            tension /= grapple.lastIndex;//average tension -- if tension calculation returned early (less segments), then tension was 0 anyway
            var d = (grapple.nodes[1].position - grapple.nodes[0].position).normalized;//can use direction from anchor to grapple, but doesn't work well when grapple is wrapped tightly around a corner
            var v = Vector2.Dot(shooterRb.linearVelocity, d);
            shooterRb.AddForceAtPosition(shooterRb.mass * (carrySpringForce * tension - carrySpringDamping * v) * d, barrel.position);
        }

    }

    //2do: should probably only sample the first few nodes near anchor(and average, so that changing how many are sampled won't throw things off)
    private float Tension()
    {
        float total = 0;
        for (int i = 1; i < grapple.lastIndex; i++)
        {

            total += (grapple.nodes[i].position - grapple.nodes[i - 1].position).magnitude - grapple.nodeSpacing;
            if (total < 0)
            {
                //this makes sense bc we're counting from anchor up, so we've got net slack *near anchor*
                return 0;
            }
        }

        return total;
    }

    private void UpdateGrappleLength(float tension)
    {
        if (GrappleAnchored)
        {
            if (grappleReleaseInput != 0)
            {
                AddGrappleLength(grappleReleaseInput * releaseRate * fixedDt);
            }
        }
        else if (grapple.Length < maxLength && tension > 0)
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
        PullingRb = false;
        //JustStoppedPullingRb = false;
        //anchorIndex = 0;
        //releaseTimer = 0;
    }
}