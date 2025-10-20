using UnityEngine;

public class GrappleCannon : MonoBehaviour
{
    //[SerializeField] Transform source;//shoot from here
    //[SerializeField] Transform barrel;//rotate this
    //[SerializeField] Transform barrelBase;
    [SerializeField] Rigidbody2D shooterRb;
    [SerializeField] float drag;
    [SerializeField] float bounciness;
    [SerializeField] LayerMask grappleAnchorMask;
    [SerializeField] LayerMask collisionMask;
    [SerializeField] float width;
    [SerializeField] int numNodes;
    [SerializeField] float collisionSearchRadiusBuffer;
    [SerializeField] float minLength;
    [SerializeField] float maxLength;
    [SerializeField] float baseShootSpeed;
    [SerializeField] float shootSpeedPowerUpRate;
    [SerializeField] float shootSpeedPowerUpMax;
    [SerializeField] float grappleMass;
    [SerializeField] float releaseRate;
    [SerializeField] float tensionCalculationInterval;
    [SerializeField] float retractMaxTension;
    //[SerializeField] float aimRotationMax;
    //[SerializeField] float aimRotationMin;
    //[SerializeField] float aimRotationSpeed;
    [SerializeField] int constraintIterations;
    [SerializeField] float carrySpringForce;
    //[SerializeField] float carrySpringDamping;
    [SerializeField] CannonFulcrum cannonFulcrum;
    //[SerializeField] float carryTensionThreshold;
    //[SerializeField] float carryForceSmoothingRate;
    //[SerializeField] float freeHangSmoothTime;

    int grappleReleaseInput;//1 = release, -1 = retract, 0 = none
    //bool releaseInputEnabled;
    bool poweringUp;
    float shootSpeedPowerUp;
    float shootTimer;
    Vector2 shootDirection;

    int aimInput;
    //float aimRotation0;//inefficient (should just add aimRotation0 to max & min values) but for now allows us to tweak max and min live
    //float aimRotation;

    Rope grapple;
    LineRenderer lineRenderer;

    float fixedDt;
    float fixedDt2;

    bool freeHanging;

    public Rope Grapple => grapple;
    public bool GrappleAnchored => grapple != null && grapple.nodes[grapple.lastIndex].Anchored;
    public int GrappleReleaseInput => grapple == null ? 0 : grappleReleaseInput;
    //public Vector2 LastCarryForceDirection { get; private set; }
    public Vector2 LastCarryForce { get; private set; }
    //public float LastCarryForceMagnitude { get; private set; }
    public Vector2 LastCarryForceDirection { get; private set; }
    public bool ShooterMovingTowardsGrapple => Vector2.Dot(shooterRb.linearVelocity, GrappleExtent) > 0;
    public Vector2 GrappleExtent => GrapplePosition - SourcePosition;
    //public Vector2 GrappleExtentFromGround
    //{
    //    get
    //    {
    //        int i = 1;
    //        while (i < grapple.nodes.Length && !grapple.nodes[i].CurrentCollision)
    //        {
    //            i++;
    //        }

    //        return grapple.nodes[i].position - AnchorPosition;
    //    }
    //}
    public Vector2 GrapplePosition => grapple.nodes[grapple.lastIndex].position;
    //public bool SourceIsBelowGrapple => GrapplePosition.y > source.position.y;
    float ShootSpeed => (1 + shootSpeedPowerUp) * baseShootSpeed;
    Vector2 SourcePosition => cannonFulcrum.LeveragePoint;//source.position;
    public Collider2D AnchorCollider => grapple.nodes[grapple.lastIndex].CurrentCollision;
    public int AnchorMask => grapple.terminusAnchorMask;
    public Vector2 FreeHangLeveragePoint => cannonFulcrum.FulcrumPosition;/*barrelBase.position;*///source.position;
    //public Vector2 SmoothedFreeHangLeveragePoint => FreeHangLeveragePoint;
    //{
    //    get
    //    {
    //        if (freeHangSmoothingTimer < freeHangSmoothTime)
    //        {
    //            return Vector2.Lerp(shooterRb.worldCenterOfMass, FreeHangLeveragePoint, FreeHangStrength);
    //        }
    //        return FreeHangLeveragePoint;
    //    }
    //}
    //public Vector2 FreeHangUp => (/*Smoothed*/FreeHangLeveragePoint - shooterRb.centerOfMass).normalized;
    public bool FreeHanging
    {
        get => GrappleAnchored && freeHanging;
        set
        {
            if (value != freeHanging)
            {
                freeHanging = value;
                if (!value)
                {
                    cannonFulcrum.ResetPhysics();
                }
                //freeHangSmoothingTimer = 0;
            }
        }
    }
    //public float FreeHangStrength => freeHangSmoothingTimer / freeHangSmoothTime;

    //private void OnDrawGizmos()
    //{
    //    if (grapple != null)
    //    {
    //        Gizmos.color = Color.yellow;
    //        for (int i = 0; i < grapple.nodes.Length; i++)
    //        {
    //            Gizmos.DrawSphere(grapple.nodes[i].position, 0.1f);
    //        }
    //    }
    //}

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        fixedDt = Time.fixedDeltaTime;
        fixedDt2 = fixedDt * fixedDt;
    }

    private void Start()
    {
        lineRenderer.enabled = false;
        cannonFulcrum.Initialize();
        //aimRotation0 = barrel.rotation.eulerAngles.z * Mathf.Deg2Rad;
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
        //if (aimInput != 0)
        //{
        //    UpdateAim();
        //}

        UpdateCannonFulcrum();

        if (grapple != null)
        {
            //UpdateFreeHangSmoothingTimer();
            UpdateAnchorPosition();
            grapple.FixedUpate(fixedDt, fixedDt2);
            UpdateGrappleLength();
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
            }
        }
    }

    private Vector2 GrappleExtentFromFirstCollision(out int firstCollisionIndex)
    {
        firstCollisionIndex = 1;
        while (firstCollisionIndex < grapple.nodes.Length && !grapple.nodes[firstCollisionIndex].CurrentCollision)
        {
            firstCollisionIndex++;
        }

        return grapple.nodes[firstCollisionIndex].position - SourcePosition;
    }

    //private void UpdateFreeHangSmoothingTimer()
    //{
    //    if (freeHanging && freeHangSmoothingTimer < freeHangSmoothTime)
    //    {
    //        freeHangSmoothingTimer += fixedDt;
    //        if (freeHangSmoothingTimer > freeHangSmoothTime)
    //        {
    //            freeHangSmoothingTimer = freeHangSmoothTime;
    //        }
    //    }
    //    else if (!freeHanging && freeHangSmoothingTimer > 0)
    //    {
    //        freeHangSmoothingTimer -= fixedDt;
    //        if (freeHangSmoothingTimer < 0)
    //        {
    //            freeHangSmoothingTimer = 0;
    //        }
    //    }
    //}

    public float Tension()
    {
        int nodesPerSeg = (int)Mathf.Ceil(tensionCalculationInterval / grapple.nodeSpacing);
        float total = 0;
        int i = 0;
        int j = 0;
        var d = nodesPerSeg * grapple.nodeSpacing;
        while (i < grapple.lastIndex)
        {
            j += nodesPerSeg;
            if (j > grapple.lastIndex)
            {
                j = grapple.lastIndex;
                d = (j - i) * grapple.nodeSpacing;
            }
            total += (grapple.nodes[j].position - grapple.nodes[i].position).magnitude - d;
            i = j;
        }
        //for (int i = 1; i < grapple.nodes.Length; i++)
        //{

        //    total += (grapple.nodes[i].position - grapple.nodes[i - 1].position).magnitude - grapple.nodeSpacing;
        //}

        return total;
    }

    public float Tension(int lastIndex, out float length)
    {
        int nodesPerSeg = (int)Mathf.Ceil(tensionCalculationInterval / grapple.nodeSpacing);
        float total = 0;
        int i = 0;
        int j = 0;
        var d = nodesPerSeg * grapple.nodeSpacing;
        length = 0;
        while (i < lastIndex)
        {
            j += nodesPerSeg;
            if (j > lastIndex)
            {
                j = lastIndex;
                d = (j - i) * grapple.nodeSpacing;
            }
            length += d;
            total += (grapple.nodes[j].position - grapple.nodes[i].position).magnitude - d;
            i = j;
        }

        return total;
    }

    public float StrictTension()
    {
        int nodesPerSeg = (int)Mathf.Ceil(tensionCalculationInterval / grapple.nodeSpacing);
        float total = 0;
        int i = 0;
        int j = 0;
        var d = nodesPerSeg * grapple.nodeSpacing;
        while (i < grapple.lastIndex)
        {
            j += nodesPerSeg;
            if (j > grapple.lastIndex)
            {
                j = grapple.lastIndex;
                d = (j - i) * grapple.nodeSpacing;
            }
            total += (grapple.nodes[j].position - grapple.nodes[i].position).magnitude - d;
            if (total < 0)
            {
                return total;
            }
            i = j;
        }

        return total;
    }

    public float StrictTension(int lastIndex, out float length)
    {
        int nodesPerSeg = (int)Mathf.Ceil(tensionCalculationInterval / grapple.nodeSpacing);
        float total = 0;
        int i = 0;
        int j = 0;
        var d = nodesPerSeg * grapple.nodeSpacing;
        length = 0;
        while (i < lastIndex)
        {
            j += nodesPerSeg;
            if (j > lastIndex)
            {
                j = lastIndex;
                d = (j - i) * grapple.nodeSpacing;
            }
            length += d;
            total += (grapple.nodes[j].position - grapple.nodes[i].position).magnitude - d;
            if (total < 0)
            {
                return total;
            }
            i = j;
        }

        return total;
    }

    public float MaxTension()
    {
        float max = -Mathf.Infinity;
        for (int i = 1; i < grapple.nodes.Length; i++)
        {
            var t = (grapple.nodes[i].position - grapple.nodes[i - 1].position).magnitude - grapple.nodeSpacing;
            if (t > max)
            {
                max = t;
            }
        }

        return max;
    }

    public float NormalizedTension() => Tension() / grapple.Length;
    public float NormalizedTension(int lastIndex) => Tension(lastIndex, out var length) / length;

    public float NormalizedStrictTension() => StrictTension() / grapple.Length;
    public float NormalizedStrictTension(int lastIndex) => StrictTension(lastIndex, out var length) / length;

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
        if (lineRenderer.positionCount != grapple.renderPositions.Length)
        {
            lineRenderer.positionCount = grapple.renderPositions.Length;
        }
        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width;
    }

    //fIXED UPDATE FUNCTIONS

    //private void UpdateAim()
    //{
    //    aimRotation += aimInput * aimRotationSpeed * fixedDt;
    //    aimRotation = Mathf.Clamp(aimRotation, aimRotationMin, aimRotationMax);
    //    var a = aimRotation0 + aimRotation;
    //    barrel.right = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
    //}

    private void UpdateCannonFulcrum()
    {
        if (GrappleAnchored)
        {
            cannonFulcrum.UpdateDynamic(fixedDt);
        }
        else
        {
            cannonFulcrum.UpdateKinematic(fixedDt, aimInput, shooterRb.transform);
        }
    }

    private void UpdateAnchorPosition()
    {
        grapple.nodes[0].position = SourcePosition;
    }

    private void UpdateCarrySpring()
    {
        LastCarryForceDirection = GrappleExtentFromFirstCollision(out int lastIndex).normalized;
        var t = NormalizedStrictTension(lastIndex);
        if (t > 0)
        {
            //LastCarryForceMagnitude = shooterRb.mass * carrySpringForce * t;
            LastCarryForce = shooterRb.mass * carrySpringForce * t * LastCarryForceDirection;//LastCarryForceMagnitude * LastCarryForceDirection;
            cannonFulcrum.ApplyForce(LastCarryForce, LastCarryForceDirection, shooterRb, FreeHanging);
            //if (FreeHanging)
            //{
            //    shooterRb.AddForceAtPosition(LastCarryForce - shooterRb.mass * carrySpringDamping * Vector2.Dot(shooterRb.linearVelocity, LastCarryForceDirection) * LastCarryForceDirection,
            //        FreeHangLeveragePoint);
            //}
            //else
            //{
            //    shooterRb.AddForce(LastCarryForce - shooterRb.mass * carrySpringDamping * Vector2.Dot(shooterRb.linearVelocity, LastCarryForceDirection) * LastCarryForceDirection);
            //}
        }
        else
        {
            LastCarryForce = Vector2.zero;
        }
    }

    private void UpdateGrappleLength()
    {
        if (GrappleAnchored)
        {
            if (grappleReleaseInput < 0 && MaxTension() > retractMaxTension)
            {
                return;
            }
            if (grappleReleaseInput != 0)
            {
                AddGrappleLength(grappleReleaseInput * releaseRate * fixedDt);
            }
        }
        else if (grapple.Length < maxLength)
        {
            shootTimer += fixedDt;//shoot timer starts negative so doesn't start growing until grapple has extended out it's initial length, ideally
            if (shootTimer > 0 && GrappleExtent.magnitude > grapple.Length) 
            {
                var p = (0.5f * shootTimer * Physics2D.gravity + ShootSpeed * shootDirection) * shootTimer + minLength * shootDirection;
                grapple.Length = Mathf.Clamp(p.magnitude, grapple.Length, maxLength);
            }
            //2do: if grapple length stagnant for certain amount of time (i.e. we have reached max length or the dot > length fails for number of updates), then enable release input)
        }
    }

    private void AddGrappleLength(float l)
    {
        grapple.Length = Mathf.Clamp(grapple.Length + l, minLength, maxLength);
    }

    //SPAWNING

    private void ShootGrapple()
    {
        var nodeSpacing = minLength / (numNodes - 1);
        grapple = new Rope(SourcePosition, width, nodeSpacing, numNodes,
                    drag, collisionMask, collisionSearchRadiusBuffer, bounciness, grappleAnchorMask, constraintIterations);
        grapple.nodes[0].Anchor();
        grapple.nodes[grapple.lastIndex].mass = grappleMass;
        var shootSpeed = ShootSpeed;
        shootDirection = cannonFulcrum.LeverDirection;//barrel.up;
        Vector2 shootVelocity = shootSpeed * shootDirection;
        grapple.nodes[grapple.lastIndex].lastPosition -= fixedDt * shootVelocity;
        var g = Physics2D.gravity.y;
        //var t0 = - 2 * shootSpeed / g;
        shootTimer = - minLength / shootSpeed;//(-shootSpeed + Mathf.Sqrt(shootSpeed * shootSpeed + 2 * g * minLength)) / g;//-minLength / shootSpeed;
        //releaseInputEnabled = false;
    }

    private void DestroyGrapple()
    {
        grapple = null;
        grappleReleaseInput = 0;
        shootSpeedPowerUp = 0;
        LastCarryForce = Vector2.zero;
        //LastCarryForceMagnitude = 0;
        //freeHangSmoothingTimer = 0;
        FreeHanging = false;
        //releaseInputEnabled = false;
        //PositiveCarryForce = false;
    }
}