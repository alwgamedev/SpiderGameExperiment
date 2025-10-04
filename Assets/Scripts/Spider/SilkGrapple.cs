using System;
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
    //[SerializeField] float stronglyPullingThreshold;
    [SerializeField] float carrySpringForce;
    [SerializeField] float carrySpringDamping;
    [SerializeField] float carryTensionPower;

    int grappleReleaseInput;//1 = release, -1 = retract, 0 = none
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

    public bool GrappleAnchored => grapple != null && grapple.nodes[grapple.lastIndex].Anchored;
    public int GrappleReleaseInput => grapple == null ? 0 : grappleReleaseInput;
    //public bool JustStoppedPullingRb { get; private set; }
    //public float LastTension { get; private set; }
    public bool PullingShooter { get; private set; }
    //public bool StronglyPullingRb { get; private set; }
    public bool ShooterMovingTowardsGrapple => Vector2.Dot(shooterRb.linearVelocity, GrappleExtent) > 0;
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
            //var t = NormalizedTension();
            //PositiveTension = t > 0;
            UpdateGrappleLength();
            if (GrappleAnchored)
            {
                UpdateCarrySpring();
            }
            //StronglyPullingRb = PullingRb && Vector2.Dot(shooterRb.linearVelocity, GrappleExtent) > 0;
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

    private void UpdateCarrySpring()
    {
        var d = grapple.nodes[1].position - grapple.nodes[0].position;
        var l = d.magnitude;
        PullingShooter = l > grapple.nodeSpacing;
        if (PullingShooter)
        {
            var t = (l - grapple.nodeSpacing) / grapple.nodeSpacing;
            d /= l;
            shooterRb.AddForce/*AtPosition*/(shooterRb.mass * (carrySpringForce * t - carrySpringDamping * Vector2.Dot(shooterRb.linearVelocity, d)) * d/*, source.position*/);
        }
        //var d = (grapple.nodes[5].position - grapple.nodes[0].position).normalized;//can use direction from anchor to grapple, but doesn't work well when grapple is wrapped tightly around a corner
        //var v = Vector2.Dot(shooterRb.linearVelocity, d);
        //shooterRb.AddForceAtPosition(shooterRb.mass * (carrySpringForce * tension - carrySpringDamping * v) * d, barrel.position);
    }

    //2do: should probably only sample the first few nodes near anchor(and average, so that changing how many are sampled won't throw things off)
    private float NormalizedTension()
    {
        float total = 0;
        //float cap = 0.35f * grapple.nodeSpacing;
        for (int i = 1; i < grapple.lastIndex; i++)
        {

            total += (grapple.nodes[i].position - grapple.nodes[i - 1].position).magnitude - grapple.nodeSpacing;
            if (total < 0)
            {
                //this makes sense bc we're counting from anchor up, so we've got net slack *near anchor*
                return 0;
            }
        }

        return total / grapple.Length;
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
        else if (grapple.Length < maxLength)
        {
            shootTimer += fixedDt;
            if (shootTimer > 0 && Vector2.Dot(GrappleExtent, shootDirection) > grapple.Length)
            {
                grapple.Length = Mathf.Clamp(0.5f * Physics2D.gravity.y * shootDirection.y * shootTimer * shootTimer + ShootSpeed * shootTimer + minLength, grapple.Length, maxLength);
            }
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
                    collisionMask, bounciness, grappleAnchorMask, constraintIterations);
        grapple.nodes[0].Anchor();
        var lastIndex = grapple.lastIndex;
        grapple.nodes[lastIndex].mass = grappleMass;
        var shootSpeed = ShootSpeed;
        shootDirection = barrel.up;
        Vector2 shootVelocity = shootSpeed * shootDirection;
        grapple.nodes[lastIndex].lastPosition -= fixedDt * shootVelocity;
        var g = Physics2D.gravity.y;
        //var t0 = - 2 * shootSpeed / g;
        shootTimer = - minLength / shootSpeed;//(-shootSpeed + Mathf.Sqrt(shootSpeed * shootSpeed + 2 * g * minLength)) / g;//-minLength / shootSpeed;
    }

    private void DestroyGrapple()
    {
        grapple = null;
        grappleReleaseInput = 0;
        shootSpeedPowerUp = 0;
        PullingShooter = false;
        //StronglyPullingRb = false;
        //JustStoppedPullingRb = false;
        //anchorIndex = 0;
        //releaseTimer = 0;
    }
}