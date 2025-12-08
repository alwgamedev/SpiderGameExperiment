using System;
using UnityEngine;
using UnityEngine.Events;

public class GrappleCannon : MonoBehaviour
{
    [SerializeField] Rigidbody2D shooterRb;
    [SerializeField] int numNodes;
    [SerializeField] float width;
    [SerializeField] float minNodeSpacing;
    [SerializeField] float maxNodeSpacing;
    [SerializeField] float drag;
    [SerializeField] float bounciness;
    [SerializeField] LayerMask grappleAnchorMask;
    [SerializeField] LayerMask collisionMask;
    [SerializeField] float collisionSearchRadius;
    [SerializeField] float tunnelEscapeRadius;
    [SerializeField] float minLength;
    [SerializeField] float maxLength;
    [SerializeField] float baseShootSpeed;
    [SerializeField] float shootSpeedPowerUpRate;
    [SerializeField] float shootSpeedPowerUpMax;
    [SerializeField] float grappleMass;
    [SerializeField] float releaseRate;
    [SerializeField] float tensionCalculationInterval;
    [SerializeField] float retractMaxTension;
    [SerializeField] int constraintIterations;
    [SerializeField] int constraintIterationsPerCollisionCheck;
    [SerializeField] int nodesPerRendererPosition;
    [SerializeField] float carrySpringForce;
    [SerializeField] float carryTensionMax;
    [SerializeField] CannonFulcrum cannonFulcrum;

    int grappleReleaseInput;//1 = release, -1 = retract, 0 = none
    bool poweringUp;
    float shootSpeedPowerUp;
    float shootTimer;
    Vector2 lastShootDirection;

    public int aimInput;

    Rope grapple;
    RopeNode[] rescaleBuffer;
    LineRenderer lineRenderer;

    float fixedDt;
    float fixedDt2;

    bool freeHanging;
    //public bool grounded;

    //public Rope Grapple => grapple;
    public bool GrappleAnchored => grapple != null && grapple.nodes[grapple.lastIndex].Anchored;
    public int GrappleReleaseInput => grapple == null ? 0 : grappleReleaseInput;
    public Vector2 LastCarryForce { get; private set; }
    public Vector2 LastCarryForceDirection { get; private set; }
    public Vector2 GrappleExtent => GrapplePosition - SourcePosition;
    public Vector2 GrapplePosition => grapple.nodes[grapple.lastIndex].position;
    public bool PoweringUp => poweringUp;
    public float PowerUpFraction => shootSpeedPowerUp / shootSpeedPowerUpMax;
    public float ShootSpeed => (1 + shootSpeedPowerUp) * baseShootSpeed;
    public Vector2 ShootDirection => cannonFulcrum.LeverDirection;
    public Vector2 ShootVelocity => ShootSpeed * ShootDirection;
    public Vector2 SourcePosition => cannonFulcrum.LeveragePoint;
    public Collider2D AnchorCollider => grapple.nodes[grapple.lastIndex].CurrentCollision;
    public int AnchorMask => grapple.terminusAnchorMask;
    public Vector2 FreeHangLeveragePoint => cannonFulcrum.FulcrumPosition;
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
            }
        }
    }

    public UnityEvent GrappleShot;
    public UnityEvent GrappleBecameAnchored;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        fixedDt = Time.fixedDeltaTime;
        fixedDt2 = fixedDt * fixedDt;
    }

    private void OnDrawGizmos()
    {
        if (grapple != null)
        {
            Gizmos.color = Color.red;
            foreach (var n in grapple.nodes)
            {
                Gizmos.DrawSphere(n.position, 0.1f);
            }
        }
    }

    private void Start()
    {
        lineRenderer.enabled = false;
        cannonFulcrum.Initialize();
    }

    private void Update()
    {
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
                grappleReleaseInput = (Input.GetKey(KeyCode.W) && grapple.Length < maxLength ? 1 : 0)
                    + (Input.GetKey(KeyCode.S) && grapple.Length > minLength ? -1 : 0);
            }
        }
    }

    private void LateUpdate()
    {
        if (grapple != null)
        {
            UpdateAnchorPosition();
            grapple.SetLineRendererPositions(lineRenderer);
        }
    }

    private void FixedUpdate()
    {
        UpdateCannonFulcrum();

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
            }
        }
    }

    private Vector2 GrappleExtentFromFirstCollision(out int firstCollisionIndex)
    {
        firstCollisionIndex = grapple.AnchorPointer + 1;
        while (firstCollisionIndex < grapple.lastIndex && !grapple.nodes[firstCollisionIndex].CurrentCollision)
        {
            firstCollisionIndex++;
        }

        return grapple.nodes[firstCollisionIndex].position - SourcePosition;
    }

    public float Tension()
    {
        int nodesPerSeg = (int)Mathf.Ceil(tensionCalculationInterval / grapple.nodeSpacing);
        float total = 0;
        int i = grapple.AnchorPointer;
        int j = grapple.AnchorPointer;
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

        return total;
    }

    public float Tension(int lastIndex, out float length)
    {
        int nodesPerSeg = (int)Mathf.Ceil(tensionCalculationInterval / grapple.nodeSpacing);
        float total = 0;
        int i = grapple.AnchorPointer;
        int j = grapple.AnchorPointer;
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
        int i = grapple.AnchorPointer;
        int j = grapple.AnchorPointer;
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
        int i = grapple.AnchorPointer;
        int j = grapple.AnchorPointer;
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
            //length += d;

            //total += (grapple.nodes[j].position - grapple.nodes[i].position).magnitude - d;
            //if (total < 0 && i > 0)
            //{
            //    return 0;
            //}
            //length += d;
            var err = (grapple.nodes[j].position - grapple.nodes[i].position).magnitude - d;
            if (err > 0)
            {
                total += err;
                length += d;
            }
            else
            {
                return 0;
            }

            i = j;
        }

        return total;
    }

    public float MaxTension()
    {
        float max = -Mathf.Infinity;
        for (int i = grapple.AnchorPointer + 1; i < grapple.nodes.Length; i++)
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
    public float NormalizedStrictTension(int lastIndex) => StrictTension(lastIndex, out var length) / (length == 0 ? 1 : length);


    //RENDERING

    //private void UpdateLineRenderer()
    //{
    //    //if (grapple != null)
    //    //{
    //    //    if (!lineRenderer.enabled)
    //    //    {
    //    //        EnableLineRenderer();
    //    //    }
    //    //    grapple.SetLineRendererPositions(lineRenderer);
    //    //}
    //    //else if (lineRenderer.enabled)
    //    //{
    //    //    lineRenderer.enabled = false;
    //    //}
    //    grapple.SetLineRendererPositions(lineRenderer);
    //}

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
        //grapple.nodes[0].position = SourcePosition;
        grapple.SetAnchorPosition(SourcePosition);
    }

    private void UpdateCarrySpring()
    {
        LastCarryForceDirection = GrappleExtentFromFirstCollision(out int firstCollisionIndex).normalized;
        var t = NormalizedStrictTension(firstCollisionIndex);//NormalizedStrictTension(firstCollisionIndex);
        if (t > 0)
        {
            LastCarryForce = carrySpringForce * Mathf.Min(t, carryTensionMax) * LastCarryForceDirection;
            cannonFulcrum.ApplyForce(LastCarryForce, LastCarryForceDirection, shooterRb, FreeHanging);
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
                var p = (0.5f * shootTimer * Physics2D.gravity + ShootSpeed * lastShootDirection) * shootTimer + minLength * lastShootDirection;
                grapple.SetLength(Mathf.Clamp(p.magnitude, grapple.Length, maxLength), rescaleBuffer);
            }
            //2do: if grapple length stagnant for certain amount of time (i.e. we have reached max length or the dot > length fails for number of updates), then enable release input)
        }
    }

    private void AddGrappleLength(float l)
    {
        grapple.SetLength(Mathf.Clamp(grapple.Length + l, minLength, maxLength), rescaleBuffer);
    }


    //SPAWNING

    private void ShootGrapple()
    {
        //var nodeSpacing = minLength / (numNodes - 1);
        grapple = new Rope(SourcePosition, width, minLength, numNodes, minNodeSpacing, maxNodeSpacing,
                    drag, collisionMask, collisionSearchRadius, tunnelEscapeRadius, bounciness, grappleAnchorMask, 
                    constraintIterations, constraintIterationsPerCollisionCheck,
                    nodesPerRendererPosition);
        if (rescaleBuffer == null || rescaleBuffer.Length != numNodes)
        {
            Array.Resize(ref rescaleBuffer, numNodes);
        }
        //grapple.nodes[0].Anchor();
        grapple.nodes[grapple.lastIndex].mass = grappleMass;
        var shootSpeed = ShootSpeed;
        lastShootDirection = ShootDirection;
        Vector2 shootVelocity = shootSpeed * lastShootDirection;
        grapple.nodes[grapple.lastIndex].lastPosition -= fixedDt * shootVelocity;
        //var g = Physics2D.gravity.y;
        shootTimer = -minLength / shootSpeed;
        EnableLineRenderer();
        grapple.SetLineRendererPositions(lineRenderer);
        grapple.TerminusAnchored = GrappleBecameAnchored;
        GrappleShot.Invoke();
    }

    private void DestroyGrapple()
    {
        grapple = null;
        grappleReleaseInput = 0;
        shootSpeedPowerUp = 0;
        LastCarryForce = Vector2.zero;
        FreeHanging = false;
        lineRenderer.enabled = false;
    }
}