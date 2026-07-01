using UnityEngine;

public class Spider : MonoBehaviour, IProjectileTarget
{
#if UNITY_EDITOR
    [SerializeField] float timeScale;
#endif
    public SpiderInput spiderInput;
    public SpiderHealth health;
    public SpiderMover mover;
    public Grabber grabber;
    public GrappleShootPreview grappleShootPreview;
    public JumpPreviewArrow jumpPreviewArrow;
    public SpiderLighting lighting;

    public int ProjectileTargetID { get; private set; }

    public static Spider Player { get; private set; }

    public void HandleProjectileHit(ProjectileCollision hit)
    {
        health.AddHealth(-hit.projectile.damage);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            Time.timeScale = timeScale;
        }

        mover.OnValidate();
        grabber.OnValidate();
        grappleShootPreview.OnValidate();
    }

    private void OnDrawGizmos()
    {
        mover.OnDrawGizmos();
        grabber.OnDrawGizmos();
    }
#endif

    private void Awake()
    {
        Player = this;
    }

    private void Start()
    {
#if UNITY_EDITOR
        Time.timeScale = timeScale;
#endif
        ProjectileTargetID = ProjectileTargetRegistry.Register(this);

        spiderInput.Initialize();
        health.Start();
        mover.Initialize(transform, spiderInput, ProjectileTargetID);
        grabber.Initialize(spiderInput, mover.SpideyBody.head, mover.SpideyBody.abdomen, 
            new() { projectileTarget = ProjectileTargetID });
        mover.grabber = grabber;
        jumpPreviewArrow.Start();
        grappleShootPreview.Start(mover.World);
        lighting.Initialize(spiderInput);

        grabber.Disable(true);
        grabber.HideSprites();
    }

    private void OnEnable()
    {
        mover.Enable();
        grabber.Enable();

        health.Hurt += lighting.HurtFlash;
    }

    private void OnDisable()
    {
        mover.Disable();
        grabber.Disable(false);

        health.Hurt -= lighting.HurtFlash;
    }

    private void Update()
    {
        mover.Update();
        grabber.Update();
        lighting.Update();
    }

    private void LateUpdate()
    {
        mover.LateUpdate();
        jumpPreviewArrow.LateUpdate(mover);
        grappleShootPreview.LateUpdate(mover.Grapple);
    }

    private void FixedUpdate()
    {
        mover.FixedUpdate();
        grabber.FixedUpdate(Time.deltaTime);
    }

    private void OnDestroy()
    {
        mover.OnDestroy();
        jumpPreviewArrow.OnDestroy();
        grappleShootPreview.OnDestroy();
        lighting.OnDestroy();

        ProjectileTargetRegistry.Release(this);

        if (Player == this)
        {
            Player = null;
        }
    }
}