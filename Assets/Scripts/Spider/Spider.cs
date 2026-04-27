using UnityEngine;

public class Spider : MonoBehaviour
{
#if UNITY_EDITOR
    [SerializeField] float timeScale;
#endif
    public SpiderInput spiderInput;
    public Health health;
    public SpiderMover mover;
    public SGrabber grabber;
    public GrappleShootPreview grappleShootPreview;
    public JumpPreviewArrow jumpPreviewArrow;

    //public Health Health => health;
    //public SpiderMover Mover => mover;
    public Collider2D TriggerCollider { get; private set; }//was for fluid interaction; need to update fluid to new physics system

    public static Spider Player { get; private set; }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            Time.timeScale = timeScale;
        }

        mover.OnValidate();
        grabber.OnValidate();
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
        spiderInput.Initialize();
        health.Start();
        mover.Initialize(transform, spiderInput);
        grabber.Initialize(spiderInput, mover.SpideyBody.head, mover.SpideyBody.abdomen);
        jumpPreviewArrow.Start();
        grappleShootPreview.Start(mover.FacingRight);

        grabber.Disable(true);
        grabber.HideSprites();
    }

    private void OnEnable()
    {
        mover.Enable();
        grabber.Enable();
    }

    private void OnDisable()
    {
        mover.Disable();
        grabber.Disable(false);
    }

    private void Update()
    {
        mover.Update();
        grabber.Update(Time.deltaTime);
    }

    private void LateUpdate()
    {
        mover.LateUpdate();
        jumpPreviewArrow.LateUpdate(mover);
        grappleShootPreview.LateUpdate(mover.Grapple, mover.FacingRight);
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
        Player = null;
    }
}