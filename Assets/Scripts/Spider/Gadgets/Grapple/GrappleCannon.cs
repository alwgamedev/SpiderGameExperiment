using UnityEngine;
using UnityEngine.Events;
using Unity.U2D.Physics;
using System;

[Serializable]
public class GrappleCannon
{
    [NonSerialized] public float aimInput;

    [SerializeField] bool drawGizmos;
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
    bool poweringUp;
    float shootSpeedPowerUp;
    float shootTimer;
    bool shootInProgress;
    Vector2 lastShootDirection;

    //PhysicsBody spiderBody;
    SpiderInput spiderInput;
    FastRope grapple;

    bool facingRight;
    bool freeHanging;

    public bool GrappleEnabled => grapple != null && grapple.Enabled;
    public bool GrappleAnchored => GrappleEnabled && grapple.TerminusAnchored;
    public float GrappleReleaseInput => spiderInput.SecondaryInput.y;
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

    public UnityEvent grappleShot;
    public UnityEvent grappleBecameAnchored;
    public UnityEvent grappleDestroyed;

    public void OnDrawGizmos()
    {
        if (drawGizmos && grapple != null && GrappleEnabled)
        {
            grapple.DrawGizmos();
        }
    }

    public void Initialize(SpiderInput spiderInput, PhysicsWorld ownerWorld, float ownerMass, bool facingRight)
    {
        this.spiderInput = spiderInput;
        //this.spiderBody = spiderBody;

        grapple = new FastRope(grappleSettings, ownerWorld, ownerMass, SourcePosition, minLength, numNodes);
        grapple.Disable();

        cannonFulcrum.Initialize();
        grappleRenderer.Initialize();
        SetOrientation(facingRight);
    }

    public void OnValidate()
    {
        if (grapple != null)
        {
            UpdateGrappleSettings();
        }
    }

    public void Update()
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

    public void LateUpdate()
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

    public void FixedUpdate(PhysicsTransform ownerTransform, PhysicsBody ownerBody)
    {
        ownerBody.SyncTransform();
        UpdateCannonFulcrum(ownerTransform);

        if (GrappleEnabled)
        {
            var grappleWasAnchored = grapple.TerminusAnchored;
            UpdateGrappleLength();
            grapple.Update(SourcePosition, Time.deltaTime);
            if (GrappleAnchored)
            {
                UpdateCarrySpring(ownerBody);
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
                ShootGrapple(Time.deltaTime);
            }
        }
    }

    public void OnDestroy()
    {
        grapple?.Dispose();
    }

    public void SetOrientation(bool facingRight)
    {
        this.facingRight = facingRight;
        grappleRenderer.SetOrientation(facingRight);
    }

    //FIXED UPDATE FUNCTIONS

    private void UpdateCannonFulcrum(PhysicsTransform ownerTransform)
    {
        if (GrappleAnchored)
        {
            cannonFulcrum.UpdateDynamic(Time.deltaTime);
        }
        else
        {
            cannonFulcrum.UpdateKinematic(Time.deltaTime, aimInput, ownerTransform, facingRight);
        }
    }

    private void UpdateCarrySpring(PhysicsBody ownerBody)
    {
        if (Vector2.SqrMagnitude(grapple.CarryForce) > 0)
        {
            LastCarryForce = carrySpringForce * grapple.CarryForce;
            cannonFulcrum.ApplyForce(LastCarryForce, ownerBody, FreeHanging);
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

    private void ShootGrapple(float dt)
    {
        grapple.Respawn(SourcePosition, minLength, numNodes);

        var shootSpeed = ShootSpeed;
        lastShootDirection = ShootDirection;
        Vector2 shootVelocity = shootSpeed * lastShootDirection;
        grapple.Shoot(shootVelocity, dt);
        shootTimer = -minLength / shootSpeed;
        grappleRenderer.OnRopeSpawned(grapple, SourcePosition);
        shootInProgress = true;

        grappleShot.Invoke();
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
        grappleDestroyed?.Invoke();
    }
}