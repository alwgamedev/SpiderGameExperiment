using UnityEngine;
using UnityEngine.Events;
using Unity.U2D.Physics;

public class GrappleCannon : MonoBehaviour
{

    [SerializeField] int numNodes;
    [SerializeField] float minLength;
    [SerializeField] float maxLength;
    [SerializeField] float baseShootSpeed;
    [SerializeField] float shootSpeedPowerUpRate;
    [SerializeField] float shootSpeedPowerUpMax;
    [SerializeField] float releaseRate;
    [SerializeField] float retractMaxTension;
    [SerializeField] float carrySpringForce;
    [SerializeField] RopeSettings grappleSettings;
    [SerializeField] CannonFulcrum cannonFulcrum;
    [SerializeField] RopeRenderer grappleRenderer;

    [System.NonSerialized] public float aimInput;
    bool poweringUp;
    float shootSpeedPowerUp;
    float shootTimer;
    bool shootInProgress;
    Vector2 lastShootDirection;

    PhysicsBody spiderBody;
    SpiderInput spiderInput;
    FastRope grapple;

    int failCounter;
    bool facingRight;
    bool freeHanging;

    public bool GrappleEnabled => grapple != null && grapple.Enabled;
    public bool GrappleAnchored => GrappleEnabled && grapple.TerminusAnchored;
    public float GrappleReleaseInput => spiderInput.SecondaryInput.y;// GrappleAnchored ? spiderInput.SecondaryInput.y : 0;
    public Vector2 LastCarryForce { get; private set; }
    public Vector2 GrappleExtent => grapple.GrappleExtent;
    public bool PoweringUp => poweringUp;
    public float PowerUpFraction => shootSpeedPowerUp / shootSpeedPowerUpMax;
    public float ShootSpeed => (1 + shootSpeedPowerUp) * baseShootSpeed;
    public Vector2 ShootDirection => cannonFulcrum.LeverDirection;
    public Vector2 ShootVelocity => ShootSpeed * ShootDirection;
    public Vector2 SourcePosition => cannonFulcrum.LeveragePoint;
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
    public UnityEvent grappleBecameAnchored;

    //private void Awake()
    //{
    //    fixedDt = Time.fixedDeltaTime;
    //    fixedDt2 = fixedDt * fixedDt;
    //}

    //private void OnDrawGizmos()
    //{
    //    if (grapple != null)
    //    {
    //        grapple.DrawGizmos();
    //    }
    //}

    public void Initialize(SpiderInput spiderInput, PhysicsBody spiderbody, bool facingRight)
    {
        this.spiderInput = spiderInput;
        this.spiderBody = spiderbody;

        grapple = new FastRope(spiderbody, grappleSettings, SourcePosition, minLength, numNodes);
        grapple.Disable();

        cannonFulcrum.Initialize();

        SetOrientation(facingRight);
    }

    private void OnValidate()
    {
        if (grapple != null)
        {
            UpdateGrappleSettings();
        }
    }

    //private void Start()
    //{
    //    cannonFulcrum.Initialize();
    //}

    private void Update()
    {
        if (!GrappleEnabled)
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
            poweringUp = spiderInput.SecondaryInput.y > 0;
        }
        else if (spiderInput.ZAction.IsPressed())
        {
            DestroyGrapple();
        }
    }

    private void LateUpdate()
    {
        if (GrappleEnabled)
        {
            //UpdateGrappleStartPosition();
            //if (grapple.TerminusAnchored)
            //{
            //    grapple.SetTerminusToAnchorPosition();
            //}
            grappleRenderer.UpdateRenderPositions(grapple, SourcePosition);
        }
    }

    private void FixedUpdate()
    {
        UpdateCannonFulcrum();

        if (GrappleEnabled)
        {
            var grappleWasAnchored = grapple.TerminusAnchored;
            UpdateGrappleLength();
            grapple.Update(SourcePosition);
            if (GrappleAnchored)
            {
                UpdateCarrySpring();
                if (!grappleWasAnchored)
                {
                    grappleBecameAnchored.Invoke();
                }
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

    private void OnDestroy()
    {
        grapple?.Dispose();
    }

    public void SetOrientation(bool facingRight)
    {
        this.facingRight = facingRight;
        grappleRenderer.SetOrientation(facingRight);
    }

    //FIXED UPDATE FUNCTIONS

    private void UpdateCannonFulcrum()
    {
        if (GrappleAnchored)
        {
            cannonFulcrum.UpdateDynamic(Time.deltaTime);
        }
        else
        {
            cannonFulcrum.UpdateKinematic(Time.deltaTime, aimInput, spiderBody.transform, facingRight);
        }
    }

    private void UpdateCarrySpring()
    {
        if (Vector2.SqrMagnitude(grapple.CarryForce) > 0)
        {
            LastCarryForce = carrySpringForce * grapple.CarryForce;
            cannonFulcrum.ApplyForce(LastCarryForce, spiderBody, FreeHanging);
        }
        else
        {
            LastCarryForce = Vector2.zero;
        }
    }

    private void UpdateGrappleLength()
    {
        if ((GrappleReleaseInput > 0 && !shootInProgress) || (GrappleReleaseInput < 0 && grapple.MaxTension < retractMaxTension))
        {
            shootInProgress = false;

            var l = GrappleReleaseInput * releaseRate * Time.deltaTime;
            grapple.RequestLengthChange(Mathf.Clamp(grapple.Length + l, minLength, maxLength));
        }
        else if (shootInProgress)
        {
            if (GrappleAnchored)
            {
                shootInProgress = false;
                return;
            }

            if (grapple.Length < maxLength)
            {
                shootTimer += Time.deltaTime;//shoot timer starts negative so doesn't start growing until grapple has extended out it's initial length, ideally
                if (shootTimer > 0 && GrappleExtent.magnitude > grapple.Length)
                {
                    var p = (0.5f * shootTimer * Physics2D.gravity + ShootSpeed * lastShootDirection) * shootTimer + minLength * lastShootDirection;
                    grapple.RequestLengthChange(Mathf.Clamp(p.magnitude, grapple.Length, maxLength));
                }
            }
            else
            {
                shootInProgress = false;
            }
        }
    }


    //SPAWNING

    private void ShootGrapple()
    {
        grapple.Respawn(SourcePosition, minLength, numNodes);

        var shootSpeed = ShootSpeed;
        lastShootDirection = ShootDirection;
        Vector2 shootVelocity = shootSpeed * lastShootDirection;
        grapple.Shoot(shootVelocity, Time.deltaTime);
        shootTimer = -minLength / shootSpeed;
        grappleRenderer.OnRopeSpawned(grapple, SourcePosition);

        shootInProgress = true;

        GrappleShot.Invoke();
    }

    private void UpdateGrappleSettings()
    {
        grapple.settings = grappleSettings;
        grappleRenderer.SetRenderWidth(grappleSettings.width);
    }

    private void DestroyGrapple()
    {
        grapple.Disable();
        shootSpeedPowerUp = 0;
        LastCarryForce = Vector2.zero;
        FreeHanging = false;
        grappleRenderer.OnRopeDestroyed();
    }
}