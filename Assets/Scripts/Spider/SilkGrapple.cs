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
    [SerializeField] float retractMaxTension;
    [SerializeField] float aimRotationMax;
    [SerializeField] float aimRotationMin;
    [SerializeField] float aimRotationSpeed;
    [SerializeField] int constraintIterations;
    [SerializeField] float carrySpringForce;
    [SerializeField] float carrySpringDamping;
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
    public Vector2 GrapplePosition => grapple.nodes[grapple.lastIndex].position;
    public bool SourceIsBelowGrapple => GrapplePosition.y > source.position.y;
    float ShootSpeed => (1 + shootSpeedPowerUp) * baseShootSpeed;
    Vector2 AnchorPosition => source.position;
    Vector2 FreeHangLeveragePoint => source.position;
    public Vector2 SmoothedFreeHangLeveragePoint
    {
        get
        {
            if (freeHangSmoothingTimer < freeHangSmoothTime)
            {
                return Vector2.Lerp(shooterRb.worldCenterOfMass, FreeHangLeveragePoint, freeHangSmoothingTimer / freeHangSmoothTime);
            }
            return FreeHangLeveragePoint;
        }
    }
    public Vector2 FreeHangUp => (SmoothedFreeHangLeveragePoint - shooterRb.centerOfMass).normalized;
    public bool FreeHanging
    {
        get => freeHanging;
        set
        {
            if (value != freeHanging)
            {
                freeHanging = value;
                freeHangSmoothingTimer = 0;
            }
        }
    }
    //public bool StronglyFreeHanging => FreeHanging && !(freeHangSmoothingTimer < freeHangSmoothTime);

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
            if (freeHanging && freeHangSmoothingTimer < freeHangSmoothTime)
            {
                freeHangSmoothingTimer += fixedDt;
            }
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

    public float Tension()
    {
        float total = 0;
        //float cap = 0.35f * grapple.nodeSpacing;
        for (int i = 1; i < grapple.nodes.Length; i++)
        {

            total += (grapple.nodes[i].position - grapple.nodes[i - 1].position).magnitude - grapple.nodeSpacing;
        }

        return total;
    }

    public float NonnegativeTension()
    {
        float total = 0;
        //float cap = 0.35f * grapple.nodeSpacing;
        for (int i = 1; i < grapple.nodes.Length; i++)
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

    public float NonnegativeNormalizedTension() => NonnegativeTension() / grapple.Length;

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

    private void UpdateCarrySpring()
    {
        var d = grapple.nodes[1].position - grapple.nodes[0].position;
        var l = d.magnitude;
        if (l > grapple.nodeSpacing)
        {
            var t = (l - grapple.nodeSpacing) / grapple.nodeSpacing;
            d /= l;
            LastCarryForceApplied = shooterRb.mass * carrySpringForce * t * d;
            if (FreeHanging)
            {
                shooterRb.AddForceAtPosition(LastCarryForceApplied - shooterRb.mass * carrySpringDamping * Vector2.Dot(shooterRb.linearVelocity, d) * d, 
                    SmoothedFreeHangLeveragePoint);
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
        grapple = new Rope(source.position, width, nodeSpacing, numNodes, drag,
                    collisionMask, collisionSearchRadiusBuffer, bounciness, grappleAnchorMask, constraintIterations);
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
        //releaseInputEnabled = false;
        //PositiveCarryForce = false;
    }
}