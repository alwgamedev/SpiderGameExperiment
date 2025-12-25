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
    [SerializeField] float carrySpringForce;
    [SerializeField] float carryTensionMax;
    [SerializeField] float breakThreshold;
    [SerializeField] float consecutiveFailuresBeforeBreaking;
    [SerializeField] CannonFulcrum cannonFulcrum;
    [SerializeField] RopeRenderer grappleRenderer;

    int grappleReleaseInput;//1 = release, -1 = retract, 0 = none
    bool poweringUp;
    float shootSpeedPowerUp;
    float shootTimer;
    Vector2 lastShootDirection;

    public int aimInput;

    Rope grapple;

    float fixedDt;
    float fixedDt2;

    int failCounter;

    bool freeHanging;

    public bool GrappleAnchored => grapple != null && grapple.TerminusAnchored;
    public int GrappleReleaseInput => grapple == null ? 0 : grappleReleaseInput;
    public Vector2 LastCarryForce { get; private set; }
    public Vector2 LastCarryForceDirection { get; private set; }
    public Vector2 GrappleExtent => GrappleTerminusPosition - SourcePosition;
    public Vector2 GrappleTerminusPosition => grapple.position[^1];
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
    public UnityEvent GrappleBecameAnchored;

    private void Awake()
    {
        fixedDt = Time.fixedDeltaTime;
        fixedDt2 = fixedDt * fixedDt;
    }

    //private void OnDrawGizmos()
    //{
    //    if (grapple != null)
    //    {
    //        grapple.DrawGizmos();
    //    }
    //}

    private void Start()
    {
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
                if (grapple.CollisionIsFailing(breakThreshold))
                {
                    failCounter++;
                    if (failCounter > consecutiveFailuresBeforeBreaking)
                    {
                        failCounter = 0;
                        DestroyGrapple();
                    }
                    //in the future i'd like to have a cool snap effect or something other than the grapple just disappearing instantly
                    //that could be easy just stop enforcing constraints for the tunneled nodes and stop rendering them
                    //so the rope appears to split in two halves that drift apart
                }
                else
                {
                    failCounter = 0;
                    grappleReleaseInput = (Input.GetKey(KeyCode.W) && grapple.Length < maxLength ? 1 : 0)
                    + (Input.GetKey(KeyCode.S) && grapple.Length > minLength ? -1 : 0);
                }
            }
        }
    }

    private void LateUpdate()
    {
        if (grapple != null)
        {
            UpdateAnchorPosition();
            grappleRenderer.UpdateRenderPositions(grapple);
        }
    }

    private void FixedUpdate()
    {
        UpdateCannonFulcrum();

        if (grapple != null)
        {
            UpdateGrappleLength();
            UpdateAnchorPosition();
            grapple.Update(fixedDt, fixedDt2);
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
        while (firstCollisionIndex < grapple.TerminusIndex && !grapple.currentCollision[firstCollisionIndex])
        {
            firstCollisionIndex++;
        }

        return grapple.position[firstCollisionIndex] - SourcePosition;
    }

    public float Tension()
    {
        int nodesPerSeg = (int)Mathf.Ceil(tensionCalculationInterval / grapple.NodeSpacing);
        float total = 0;
        int i = grapple.AnchorPointer;
        int j = grapple.AnchorPointer;
        var d = nodesPerSeg * grapple.NodeSpacing;
        while (i < grapple.TerminusIndex)
        {
            j += nodesPerSeg;
            if (j > grapple.TerminusIndex)
            {
                j = grapple.TerminusIndex;
                d = (j - i) * grapple.NodeSpacing;
            }
            total += (grapple.position[j] - grapple.position[i]).magnitude - d;
            i = j;
        }

        return total;
    }

    public float Tension(int lastIndex, out float length)
    {
        int nodesPerSeg = (int)Mathf.Ceil(tensionCalculationInterval / grapple.NodeSpacing);
        float total = 0;
        int i = grapple.AnchorPointer;
        int j = grapple.AnchorPointer;
        var d = nodesPerSeg * grapple.NodeSpacing;
        length = 0;
        while (i < lastIndex)
        {
            j += nodesPerSeg;
            if (j > lastIndex)
            {
                j = lastIndex;
                d = (j - i) * grapple.NodeSpacing;
            }
            length += d;
            total += (grapple.position[j] - grapple.position[i]).magnitude - d;
            i = j;
        }

        return total;
    }

    public float StrictTension()
    {
        int nodesPerSeg = (int)Mathf.Ceil(tensionCalculationInterval / grapple.NodeSpacing);
        float total = 0;
        int i = grapple.AnchorPointer;
        int j = grapple.AnchorPointer;
        var d = nodesPerSeg * grapple.NodeSpacing;
        while (i < grapple.TerminusIndex)
        {
            j += nodesPerSeg;
            if (j > grapple.TerminusIndex)
            {
                j = grapple.TerminusIndex;
                d = (j - i) * grapple.NodeSpacing;
            }
            total += (grapple.position[j] - grapple.position[i]).magnitude - d;
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
        int nodesPerSeg = (int)Mathf.Ceil(tensionCalculationInterval / grapple.NodeSpacing);
        float total = 0;
        int i = grapple.AnchorPointer;
        int j = grapple.AnchorPointer;
        var d = nodesPerSeg * grapple.NodeSpacing;
        length = 0;
        while (i < lastIndex)
        {
            j += nodesPerSeg;
            if (j > lastIndex)
            {
                j = lastIndex;
                d = (j - i) * grapple.NodeSpacing;
            }

            var err = (grapple.position[j] - grapple.position[i]).magnitude - d;
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
        for (int i = grapple.AnchorPointer + 1; i < grapple.position.Length; i++)
        {
            var t = (grapple.position[i] - grapple.position[i - 1]).magnitude - grapple.NodeSpacing;
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

    //FIXED UPDATE FUNCTIONS

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
                grapple.SetLength(Mathf.Clamp(p.magnitude, grapple.Length, maxLength));
            }
            //2do: if grapple length stagnant for certain amount of time (i.e. we have reached max length or the dot > length fails for number of updates), then enable release input)
        }
    }

    private void AddGrappleLength(float l)
    {
        grapple.SetLength(Mathf.Clamp(grapple.Length + l, minLength, maxLength));
    }


    //SPAWNING

    private void ShootGrapple()
    {
        grapple = new Rope(SourcePosition, width, minLength, numNodes, minNodeSpacing, maxNodeSpacing,
                    1, grappleMass, drag, collisionMask, collisionSearchRadius, tunnelEscapeRadius, bounciness, grappleAnchorMask,
                    constraintIterations);
        var shootSpeed = ShootSpeed;
        lastShootDirection = ShootDirection;
        Vector2 shootVelocity = shootSpeed * lastShootDirection;
        grapple.lastPosition[^1] -= fixedDt * shootVelocity;
        shootTimer = -minLength / shootSpeed;
        grappleRenderer.OnRopeSpawned(grapple);
        
        grapple.TerminusBecameAnchored = GrappleBecameAnchored;
        GrappleShot.Invoke();
    }

    private void DestroyGrapple()
    {
        grapple = null;
        grappleReleaseInput = 0;
        shootSpeedPowerUp = 0;
        LastCarryForce = Vector2.zero;
        FreeHanging = false;
        grappleRenderer.OnRopeDestroyed();
    }
}