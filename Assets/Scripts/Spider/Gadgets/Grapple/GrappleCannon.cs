using UnityEngine;
using UnityEngine.Events;
using Unity.U2D.Physics;

public class GrappleCannon : MonoBehaviour
{
    public PhysicsBody spiderBody;
    [System.NonSerialized] public SpiderInput spiderInput;

    [SerializeField] int numNodes;
    [SerializeField] float minLength;
    [SerializeField] float maxLength;
    [SerializeField] float baseShootSpeed;
    [SerializeField] float shootSpeedPowerUpRate;
    [SerializeField] float shootSpeedPowerUpMax;
    [SerializeField] float releaseRate;
    [SerializeField] float retractMaxTension;
    [SerializeField] float carrySpringForce;
    [SerializeField] float carryTensionMax;
    [SerializeField] float consecutiveFailuresBeforeBreaking;
    [SerializeField] RopeSettings grappleSettings;
    [SerializeField] CannonFulcrum cannonFulcrum;
    [SerializeField] RopeRenderer grappleRenderer;

    [System.NonSerialized]public float aimInput;
    bool poweringUp;
    float shootSpeedPowerUp;
    float shootTimer;
    Vector2 lastShootDirection;

    FastRope grapple;

    int failCounter;
    bool facingRight;
    bool freeHanging;

    public bool GrappleEnabled => grapple != null && grapple.Enabled;
    public bool GrappleAnchored => GrappleEnabled && grapple.TerminusAnchored;
    public float GrappleReleaseInput => GrappleAnchored ? spiderInput.SecondaryInput.y : 0;
    public Vector2 LastCarryForce { get; private set; }
    public Vector2 LastCarryForceDirection { get; private set; }
    public Vector2 GrappleExtent => grapple.GrappleExtent; //GrappleTerminusPosition - SourcePosition;
    //public Vector2 GrappleTerminusPosition => grapple.position[^1];
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

    private void OnValidate()
    {
        if (grapple != null)
        {
            UpdateGrappleSettings();
        }
    }

    private void Start()
    {
        cannonFulcrum.Initialize();

        grapple = new FastRope(grappleSettings, SourcePosition, minLength, numNodes);
        grapple.Disable();
    }

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
        else
        {
            if (spiderInput.ZAction.IsPressed())
            {
                DestroyGrapple();
            }
            else if (GrappleAnchored)
            {
                if (grapple.CollisionIsFailing)
                {
                    failCounter++;
                    if (failCounter > consecutiveFailuresBeforeBreaking)
                    {
                        failCounter = 0;
                        DestroyGrapple();
                    }
                    //in the future i'd like to have a cool snap effect or something other than the grapple just disappearing instantly.
                    //that could be easy just stop enforcing constraints for the tunneled nodes and stop rendering them
                    //so the rope appears to split in two halves that drift apart
                }
                else
                {
                    failCounter = 0;
                }
            }
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

    //private void UpdateGrappleStartPosition()
    //{
    //    grapple.SetStartPosition(SourcePosition);
    //}

    private void UpdateCarrySpring()
    {
        LastCarryForceDirection = grapple.CarryForceDirection;
        if (grapple.CarryForceMagnitude > 0)
        {
            LastCarryForce = carrySpringForce * Mathf.Min(grapple.CarryForceMagnitude, carryTensionMax) * LastCarryForceDirection;
            cannonFulcrum.ApplyForce(LastCarryForce, LastCarryForceDirection, spiderBody, FreeHanging);
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
            if (GrappleReleaseInput < 0 && grapple.MaxTension > retractMaxTension)
            {
                return;
            }
            if (GrappleReleaseInput != 0)
            {
                var l = GrappleReleaseInput * releaseRate * Time.deltaTime;
                grapple.RequestLengthChange(Mathf.Clamp(grapple.Length + l, minLength, maxLength));
            }
        }
        else if (grapple.Length < maxLength)
        {
            shootTimer += Time.deltaTime;//shoot timer starts negative so doesn't start growing until grapple has extended out it's initial length, ideally
            if (shootTimer > 0 && GrappleExtent.magnitude > grapple.Length)
            {
                var p = (0.5f * shootTimer * Physics2D.gravity + ShootSpeed * lastShootDirection) * shootTimer + minLength * lastShootDirection;
                grapple.RequestLengthChange(Mathf.Clamp(p.magnitude, grapple.Length, maxLength));
            }
            //2do: if grapple length stagnant for certain amount of time (i.e. we have reached max length or the dot > length fails for number of updates), then enable release input)
        }
    }


    //SPAWNING

    private void ShootGrapple()
    {
        //if (grapple == null)
        //{
        //    grapple = new FastRope(grappleSettings, SourcePosition, minLength, numNodes);
        //}
        //else
        //{
        //    UpdateGrappleSettings();
        //    grapple.Respawn(SourcePosition, minLength, numNodes);
        //}

        grapple.Respawn(SourcePosition, minLength, numNodes);

        var shootSpeed = ShootSpeed;
        lastShootDirection = ShootDirection;
        Vector2 shootVelocity = shootSpeed * lastShootDirection;
        grapple.Shoot(shootVelocity, Time.deltaTime);
        shootTimer = -minLength / shootSpeed;
        grappleRenderer.OnRopeSpawned(grapple, SourcePosition);

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