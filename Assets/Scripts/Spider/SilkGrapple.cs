using System;
using UnityEngine;

public class SilkGrapple : MonoBehaviour
{
    [SerializeField] Transform source;//shoot from here
    [SerializeField] Transform barrel;//rotate this
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
    [SerializeField] float aimRotationMax;
    [SerializeField] float aimRotationMin;
    [SerializeField] float aimRotationSpeed;
    [SerializeField] int constraintIterations;
    [SerializeField] float carrySpringForce;
    [SerializeField] float carrySpringDamping;
    //[SerializeField] float carryTensionThreshold;
    //[SerializeField] float carryForceSmoothingRate;
    [SerializeField] float freeHangSmoothTime;

    int grappleReleaseInput;//1 = release, -1 = retract, 0 = none
    //bool releaseInputEnabled;
    bool poweringUp;
    float shootSpeedPowerUp;
    float shootTimer;
    Vector2 shootDirection;

    int aimInput;
    float aimRotation0;//inefficient (should just add aimRotation0 to max & min values) but for now allows us to tweak max and min live
    float aimRotation;

    Rope grapple;
    LineRenderer lineRenderer;

    float fixedDt;
    float fixedDt2;

    bool freeHanging;
    float freeHangSmoothingTimer;

    //public float carryForceMultiplier = 1;

    public bool GrappleAnchored => grapple != null && grapple.nodes[grapple.lastIndex].Anchored;
    public int GrappleReleaseInput => grapple == null ? 0 : grappleReleaseInput;
    //public Vector2 LastCarryForceDirection { get; private set; }
    public Vector2 LastCarryForceApplied { get; private set; }
    public bool ShooterMovingTowardsGrapple => Vector2.Dot(shooterRb.linearVelocity, GrappleExtent) > 0;
    public Vector2 GrappleExtent => GrapplePosition - AnchorPosition;
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
    public bool SourceIsBelowGrapple => GrapplePosition.y > source.position.y;
    float ShootSpeed => (1 + shootSpeedPowerUp) * baseShootSpeed;
    Vector2 AnchorPosition => source.position;
    public Collider2D AnchorCollider => grapple.nodes[grapple.lastIndex].CurrentCollision;
    public int AnchorMask => grapple.terminusAnchorMask;
    public Vector2 FreeHangLeveragePoint => source.position;
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
    public Vector2 FreeHangUp => (/*Smoothed*/FreeHangLeveragePoint - shooterRb.centerOfMass).normalized;
    public bool FreeHanging
    {
        get => GrappleAnchored && freeHanging;
        set
        {
            if (value != freeHanging)
            {
                freeHanging = value;
                //freeHangSmoothingTimer = 0;
            }
        }
    }
    public float FreeHangStrength => freeHangSmoothingTimer / freeHangSmoothTime;

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
            UpdateFreeHangSmoothingTimer();
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

        return grapple.nodes[firstCollisionIndex].position - AnchorPosition;
    }

    private void UpdateFreeHangSmoothingTimer()
    {
        if (freeHanging && freeHangSmoothingTimer < freeHangSmoothTime)
        {
            freeHangSmoothingTimer += fixedDt;
            if (freeHangSmoothingTimer > freeHangSmoothTime)
            {
                freeHangSmoothingTimer = freeHangSmoothTime;
            }
        }
        else if (!freeHanging && freeHangSmoothingTimer > 0)
        {
            freeHangSmoothingTimer -= fixedDt;
            if (freeHangSmoothingTimer < 0)
            {
                freeHangSmoothingTimer = 0;
            }
        }
    }

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

    private void UpdateCarrySpring()
    {
        var d = GrappleExtentFromFirstCollision(out int lastIndex).normalized;
        var t = NormalizedStrictTension(lastIndex);
        if (t > 0)
        {
            LastCarryForceApplied = shooterRb.mass * carrySpringForce * t * d;
            if (FreeHanging)
            {
                shooterRb.AddForceAtPosition(LastCarryForceApplied - shooterRb.mass * carrySpringDamping * Vector2.Dot(shooterRb.linearVelocity, d) * d,
                    /*Smoothed*/FreeHangLeveragePoint);
            }
            else
            {
                shooterRb.AddForce(LastCarryForceApplied - shooterRb.mass * carrySpringDamping * Vector2.Dot(shooterRb.linearVelocity, d) * d);
            }
        }
        else
        {
            LastCarryForceApplied = Vector2.zero;
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
            shootTimer += fixedDt;
            if (shootTimer > 0 && GrappleExtent.magnitude > grapple.Length)
            {
                grapple.Length = Mathf.Clamp(0.5f * Physics2D.gravity.y * shootDirection.y * shootTimer * shootTimer + ShootSpeed * shootTimer + minLength, grapple.Length, maxLength);
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
        grapple = new Rope(source.position, width, nodeSpacing, numNodes,
                    drag, collisionMask, collisionSearchRadiusBuffer, bounciness, grappleAnchorMask, constraintIterations);
        grapple.nodes[0].Anchor();
        grapple.nodes[grapple.lastIndex].mass = grappleMass;
        var shootSpeed = ShootSpeed;
        shootDirection = barrel.up;
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
        LastCarryForceApplied = Vector2.zero;
        freeHangSmoothingTimer = 0;
        freeHanging = false;
        //releaseInputEnabled = false;
        //PositiveCarryForce = false;
    }
}